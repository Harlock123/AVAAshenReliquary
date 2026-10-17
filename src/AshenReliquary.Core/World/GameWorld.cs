using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Progression;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// One zone in play: the hero, the monsters and everything that happens between them. It integrates in
/// fixed 60 Hz steps, so frame rate never changes gameplay, and every roll comes from one seed.
/// </summary>
public sealed class GameWorld
{
    public const double StepSeconds = 1.0 / 60;

    /// <summary>Converts the rules' tiles a second (25 ft = 5) to this map's larger 192x96 tiles.</summary>
    public const double MapSpeedScale = 0.7;

    public const double MeleeReach = 1.1;
    public const double ShrineRadius = 1.6;
    public const double RespawnSeconds = 3.0;
    public const double CorpseSeconds = 12.0;
    public const double PackAggroRadius = 6.0;
    public const double LeashDistance = 24.0;
    public const double FlourishLockoutSeconds = 3.0;
    public const double DashSpeed = 12.0;
    public const double ProjectileSpeed = 11.0;

    private readonly IReadOnlyDictionary<string, CreatureDefinition> _creatures;
    private readonly List<MonsterActor> _monsters = [];
    private readonly List<FloatingText> _texts = [];
    private readonly HashSet<string> _activatedShrines = [];
    private readonly List<Projectile> _projectiles = [];
    private readonly List<Impact> _impacts = [];
    private readonly List<GroundItem> _ground = [];
    private readonly LootGenerator? _loot;
    private readonly HashSet<int> _slain = [];
    private int _nextSpawnIndex;

    /// <summary>The placed monsters killed so far (their <see cref="MonsterActor.SpawnIndex"/>), for saving.</summary>
    public IReadOnlyCollection<int> Slain => _slain;

    /// <summary>Raised when a shrine is touched for the first time or becomes the respawn point again: an autosave moment.</summary>
    public event Action<ShrineDefinition>? ShrineActivated;
    private double _accumulator;

    public GameWorld(ZoneDefinition zone, IReadOnlyDictionary<string, CreatureDefinition> creatures, Character hero, int seed,
        IReadOnlyDictionary<string, AbilityDefinition>? bar = null, LootGenerator? loot = null)
    {
        _loot = loot;
        Bar = bar ?? new Dictionary<string, AbilityDefinition>();
        Zone = zone;
        _creatures = creatures;
        Map = new TileMap(zone);
        Random = new SeededRandom(seed);
        Hero = new HeroActor(hero) { X = zone.PlayerStart[0] + 0.5, Y = zone.PlayerStart[1] + 0.5 };
        RespawnPoint = (Hero.X, Hero.Y);
        hero.HeroPoints.GainForZone();
        Experience = new Experience();

        var packId = 0;
        foreach (var spawn in zone.Spawns)
        {
            packId++;
            foreach (var member in spawn.Pack)
            {
                for (var i = 0; i < member.Count; i++)
                    SpawnNear(member.Creature, member.Rank, spawn.At[0] + 0.5, spawn.At[1] + 0.5, spawn.Radius, packId).SpawnIndex = _nextSpawnIndex++;
            }
        }
    }

    public ZoneDefinition Zone { get; }
    public TileMap Map { get; }
    public IRandomSource Random { get; }
    public HeroActor Hero { get; }
    public Experience Experience { get; }
    public IReadOnlyList<MonsterActor> Monsters => _monsters;
    public IReadOnlyList<FloatingText> Texts => _texts;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public IReadOnlyList<Impact> Impacts => _impacts;
    public IReadOnlyList<GroundItem> GroundItems => _ground;

    public const double PickupRadius = 1.0;
    public const double GoldMagnetRadius = 1.3;
    public double Time { get; private set; }
    public (double X, double Y) RespawnPoint { get; private set; }
    public bool ZoneComplete { get; private set; }
    public string? LastMessage { get; private set; }
    public double LastMessageAt { get; private set; } = double.NegativeInfinity;
    public IReadOnlyCollection<string> ActivatedShrines => _activatedShrines;

    // ----- Commands from the player ------------------------------------------------

    /// <summary>The ability bar: "lmb", "rmb" and "1".."6" to abilities. The basic attack is the "lmb" slot.</summary>
    public IReadOnlyDictionary<string, AbilityDefinition> Bar { get; }

    /// <summary>The basic attack: the "lmb" slot, or a plain weapon Strike when the bar has none.</summary>
    public AbilityDefinition BasicAbility => Bar.GetValueOrDefault("lmb") ?? DefaultStrike;

    public static readonly AbilityDefinition DefaultStrike =
        new("strike", "Strike", "any", "lmb", AbilityKind.WeaponStrike, Repeat: true, Traits: ["Attack"]);

    /// <summary>Walk to a point (a left click on the ground). Cancels any attack order.</summary>
    public void MoveTo(double x, double y)
    {
        if (Hero.IsDead)
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.Goal = (x, y);
        PlanPath(Hero, x, y);
    }

    /// <summary>Basic attack on a monster (a left click on it): walk into range, then keep attacking until it dies.</summary>
    public void Attack(MonsterActor monster) => Use(BasicAbility, monster);

    /// <summary>Uses the ability in a bar slot on a monster (right click, keys 1 to 6). False when the slot is empty.</summary>
    public bool UseSlot(string slot, MonsterActor? target)
    {
        if (!Bar.TryGetValue(slot, out var ability))
            return false;
        Use(ability, target);
        return true;
    }

    private void Use(AbilityDefinition ability, MonsterActor? target)
    {
        if (Hero.IsDead)
            return;
        if (target is null || target.IsDead)
        {
            Say($"{ability.Name} needs a target.");
            return;
        }
        Hero.Order = new HeroOrder(ability, target);
        Hero.PickupTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
    }

    /// <summary>Walk to a piece of loot and pick it up (a click on its label).</summary>
    public void PickUp(GroundItem item)
    {
        if (Hero.IsDead || !_ground.Contains(item))
            return;
        Hero.Order = null;
        Hero.PickupTarget = item;
        Hero.Goal = (item.X, item.Y);
        PlanPath(Hero, item.X, item.Y);
    }

    /// <summary>Drops an item from the bag at the hero's feet.</summary>
    public bool DropFromBag(int index)
    {
        if (Hero.Equipment.TakeFromBag(index) is not { } item)
            return false;
        _ground.Add(new GroundItem(Hero.X + 0.4, Hero.Y + 0.4, new LootDrop(item), Time));
        return true;
    }

    public void Stop()
    {
        Hero.Order = null;
        Hero.Goal = null;
        Hero.Path.Clear();
    }

    /// <summary>Seconds until <paramref name="ability"/> can be used again (cooldown or Flourish lockout).</summary>
    public double CooldownRemaining(AbilityDefinition ability)
    {
        var ready = Hero.ReadyAt.GetValueOrDefault(ability.Id);
        if (ability.IsFlourish)
            ready = Math.Max(ready, Hero.FlourishReadyAt);
        return Math.Max(0, ready - Time);
    }

    /// <summary>Drinks a potion charge if one is ready. The healing arrives over two seconds.</summary>
    public bool DrinkPotion()
    {
        var c = Hero.Character;
        if (Hero.IsDead || c.Potion.Drink(Time, c.Level, Random, c.Conditions) is not { } heal)
            return false;
        Hero.HealRemaining = heal;
        Hero.HealPerSecond = heal / HealingPotion.HealDurationSeconds;
        Say($"Potion: +{heal} HP over 2 s");
        return true;
    }

    /// <summary>The living monster under a map point, if any (generous radius for clicking).</summary>
    public MonsterActor? MonsterAt(double x, double y, double radius = 0.8) =>
        _monsters.Where(m => !m.IsDead && m.DistanceTo(x, y) <= radius)
            .OrderBy(m => m.DistanceTo(x, y))
            .FirstOrDefault();

    // ----- Simulation --------------------------------------------------------------

    /// <summary>Advances real time; the world steps in fixed 1/60 s increments.</summary>
    public void Update(double seconds)
    {
        _accumulator += Math.Min(seconds, 0.25);
        while (_accumulator >= StepSeconds)
        {
            _accumulator -= StepSeconds;
            Step(StepSeconds);
        }
    }

    private void Step(double dt)
    {
        Time += dt;
        UpdateHero(dt);
        UpdateProjectiles(dt);
        foreach (var monster in _monsters.ToList())
            UpdateMonster(monster, dt);
        Separate();
        _monsters.RemoveAll(m => m.IsDead && m.DeadFor > CorpseSeconds);
        _texts.RemoveAll(t => Time - t.BornAt > FloatingText.Lifetime);
        _impacts.RemoveAll(i => Time - i.At > Impact.Lifetime);
        Hero.Character.Taint.Update(dt);
    }

    private void UpdateHero(double dt)
    {
        var hero = Hero;
        var c = hero.Character;
        hero.Advance(dt);
        c.Conditions.Update(dt);
        hero.Resource.Update(dt);

        if (hero.IsDead)
        {
            hero.RespawnIn -= dt;
            if (hero.RespawnIn <= 0)
                Respawn();
            return;
        }

        if (hero.HealRemaining > 0)
        {
            var amount = Math.Min(hero.HealRemaining, hero.HealPerSecond * dt);
            hero.HealRemaining -= amount;
            _healCarry += amount;
            var whole = (int)_healCarry;
            if (whole > 0)
            {
                c.Heal(whole);
                _healCarry -= whole;
            }
        }

        foreach (var shrine in Zone.Shrines)
        {
            if (hero.DistanceTo(shrine.At[0] + 0.5, shrine.At[1] + 0.5) <= ShrineRadius)
                ActivateShrine(shrine);
        }
        foreach (var exit in Zone.Exits)
        {
            if (!ZoneComplete && hero.DistanceTo(exit.At[0] + 0.5, exit.At[1] + 0.5) <= exit.Radius)
            {
                ZoneComplete = true;
                Say($"You reach {exit.Label}.");
            }
        }

        CollectLoot();

        if (hero.StaggerRemaining > 0)
        {
            hero.StaggerRemaining -= dt;
            return;
        }
        if (!c.Conditions.CanAct)
            return;

        if (hero.Action is { } action)
        {
            if (AdvanceAction(action, dt))
                return;
            hero.Action = null;
        }

        if (hero.Order is { } order)
        {
            var target = order.Target;
            if (target.IsDead)
            {
                hero.Order = null;
                hero.Play("stance");
                return;
            }
            var ability = order.Ability;
            var reach = ReachFor(ability, target);
            var startDistance = ability.Kind == AbilityKind.ChargeStrike ? reach + ability.Range : reach;
            var losOk = !ability.IsSpell || Map.HasLineOfSight(hero.X, hero.Y, target.X, target.Y);
            if (hero.DistanceTo(target) > startDistance || !losOk)
            {
                hero.RepathIn -= dt;
                if (hero.Path.Count == 0 || hero.RepathIn <= 0)
                {
                    PlanPath(hero, target.X, target.Y);
                    hero.RepathIn = 0.3;
                }
                if (FollowPath(hero, c.MoveSpeedTilesPerSecond() * MapSpeedScale, dt, null))
                    hero.Play("run");
                return;
            }
            hero.Path.Clear();

            if (WhyNot(ability) is { } problem)
            {
                if (ability.Repeat)
                {
                    hero.Face(target.X, target.Y);
                    hero.Play("stance");
                    return;
                }
                Say(problem);
                hero.Order = new HeroOrder(BasicAbility, target);
                return;
            }
            StartAction(ability, target);
            if (!ability.Repeat)
                hero.Order = new HeroOrder(BasicAbility, target);
            return;
        }

        var speed = c.MoveSpeedTilesPerSecond() * MapSpeedScale;
        if (FollowPath(hero, speed, dt, hero.Goal))
            hero.Play("run");
        else
        {
            hero.Goal = null;
            hero.Play("stance");
        }
    }

    private double _healCarry;

    /// <summary>Gold within reach is always picked up; the item the player clicked is picked up on arrival.</summary>
    private void CollectLoot()
    {
        var hero = Hero;
        foreach (var g in _ground.ToList())
        {
            var near = hero.DistanceTo(g.X, g.Y);
            var wanted = hero.PickupTarget == g && near <= PickupRadius;
            var magnet = g.Drop.Gold > 0 && near <= GoldMagnetRadius && Time - g.DroppedAt > 0.6;
            if (!wanted && !magnet)
                continue;
            if (hero.PickupTarget == g)
            {
                hero.PickupTarget = null;
                hero.Goal = null;
                hero.Path.Clear();
            }
            if (g.Drop.Item is { } item)
            {
                if (!hero.Equipment.TryAdd(item))
                {
                    Say("Your bag is full.");
                    continue;
                }
                Say($"Picked up {item.Name}.");
            }
            else if (g.Drop.Draught)
            {
                if (!hero.Character.Potion.AddCharge())
                {
                    if (wanted)
                        Say("Your potion charges are already full.");
                    continue;
                }
                Float(hero, "+1 potion", FloatingTextKind.Heal, 0.6);
            }
            else
            {
                hero.Equipment.AddGold(g.Drop.Gold);
                Float(hero, $"+{g.Drop.Gold} gold", FloatingTextKind.Info, 0.3);
            }
            _ground.Remove(g);
        }
    }

    private void DropLoot(MonsterActor monster)
    {
        if (_loot is null)
            return;
        var drops = _loot.Roll(monster.Creature.Stats.Rank, monster.Creature.Stats.EffectiveLevel, Random, monster.Definition.IsSpawner);
        foreach (var drop in drops)
        {
            double x = monster.X, y = monster.Y;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var angle = Random.Roll(0, 359) * Math.PI / 180;
                var r = 0.4 + Random.Roll(0, 80) / 100.0;
                double px = monster.X + Math.Cos(angle) * r, py = monster.Y + Math.Sin(angle) * r;
                if (Map.IsWalkable(px, py))
                {
                    (x, y) = (px, py);
                    break;
                }
            }
            _ground.Add(new GroundItem(x, y, drop, Time));
        }
    }

    /// <summary>Weapon abilities need melee reach; spells their range; reach grows with the target's size.</summary>
    private static double ReachFor(AbilityDefinition ability, MonsterActor target) =>
        ability.IsSpell ? ability.Range : MeleeReach + target.Radius;

    /// <summary>Why an ability can't start right now, or null if it can.</summary>
    private string? WhyNot(AbilityDefinition ability)
    {
        var cooldown = CooldownRemaining(ability);
        if (cooldown > 0)
            return ability.IsFlourish && Hero.FlourishReadyAt > Time && Hero.ReadyAt.GetValueOrDefault(ability.Id) <= Time
                ? $"{ability.Name}: another Flourish was used ({cooldown:0.0} s)."
                : $"{ability.Name} is not ready ({cooldown:0.0} s).";
        if (!Hero.Resource.CanSpend(ability.Cost))
            return $"Not enough {Hero.Resource.Name} for {ability.Name} ({ability.Cost:0}).";
        return null;
    }

    private void StartAction(AbilityDefinition ability, MonsterActor target)
    {
        var hero = Hero;
        var c = hero.Character;
        hero.Resource.TrySpend(ability.Cost);
        if (ability.Cooldown > 0)
            hero.ReadyAt[ability.Id] = Time + ability.Cooldown;
        if (ability.IsFlourish)
            hero.FlourishReadyAt = Time + FlourishLockoutSeconds;

        var slowed = c.Conditions.ActionTimeMultiplier;
        var swing = ActionTiming.AttackInterval(c.Weapon.Definition.AttacksPerSecond, c.Gear.AttackSpeed, slowed);
        var duration = ability.UsesWeapon
            ? Math.Max(swing, ability.Actions > 1 ? ActionTiming.CastTime(ability.Actions, c.Gear.AttackSpeed, slowed) : 0)
            : ActionTiming.CastTime(ability.Actions, c.Gear.CastSpeed, slowed);
        var dashing = ability.Kind == AbilityKind.ChargeStrike && hero.DistanceTo(target) > ReachFor(ability, target);
        hero.Action = new HeroAction(ability, target, duration) { Dashing = dashing };
        hero.Face(target.X, target.Y);
        if (dashing)
            hero.Play("run", 2.0, restart: true);
        else
            hero.Play(ability.IsSpell ? "cast" : "swing", AnimationRate(ability, duration), restart: true);
    }

    private static double AnimationRate(AbilityDefinition ability, double duration) =>
        (ability.IsSpell ? 0.4 : 0.4) / Math.Max(0.1, duration);

    /// <summary>Runs the current action one step; false once it has finished.</summary>
    private bool AdvanceAction(HeroAction action, double dt)
    {
        var hero = Hero;
        var target = action.Target;
        if (action.Dashing)
        {
            if (target.IsDead)
                return false;
            var reach = ReachFor(action.Ability, target);
            var d = hero.DistanceTo(target);
            var step = Math.Min(DashSpeed * dt, Math.Max(0, d - reach * 0.9));
            var nx = hero.X + (target.X - hero.X) / d * step;
            var ny = hero.Y + (target.Y - hero.Y) / d * step;
            action.DashTime += dt;
            if (step <= 1e-6 || !Map.IsWalkable(nx, ny) || action.DashTime > 1.5 || d <= reach)
            {
                action.Dashing = false;
                hero.Face(target.X, target.Y);
                hero.Play("swing", AnimationRate(action.Ability, action.Duration), restart: true);
                return true;
            }
            hero.X = nx;
            hero.Y = ny;
            hero.Face(target.X, target.Y);
            return true;
        }

        action.Elapsed += dt;
        var landsAt = action.Ability.IsSpell ? 0.6 : 0.5;
        if (!action.Landed && action.Elapsed >= action.Duration * landsAt)
        {
            action.Landed = true;
            ApplyAbility(action.Ability, target);
        }
        return action.Elapsed < action.Duration;
    }

    private void ApplyAbility(AbilityDefinition ability, MonsterActor target)
    {
        var rank = AbilityDefinition.SpellRank(Hero.Character.Level);
        switch (ability.Kind)
        {
            case AbilityKind.WeaponStrike or AbilityKind.ChargeStrike:
                if (!target.IsDead && Hero.DistanceTo(target) <= ReachFor(ability, target) + 0.3)
                    HeroStrike(target, ability.ExtraDiceAt(Hero.Character.Level), ability.CadenceAttacks);
                break;
            case AbilityKind.SpellBolt:
                _projectiles.Add(new Projectile(ability, target, rank, Hero.X, Hero.Y, Time, 0));
                break;
            case AbilityKind.Missiles:
                var count = ability.MissilesAt(rank);
                for (var i = 0; i < count; i++)
                    _projectiles.Add(new Projectile(ability, target, rank, Hero.X, Hero.Y, Time, i));
                break;
        }
    }

    private void UpdateProjectiles(double dt)
    {
        foreach (var p in _projectiles.ToList())
        {
            if (Time < p.LaunchAt)
                continue;
            var target = p.Target;
            if (target.IsDead)
            {
                _projectiles.Remove(p);
                continue;
            }
            var (tx, ty) = (target.X, target.Y);
            var d = Math.Sqrt((tx - p.X) * (tx - p.X) + (ty - p.Y) * (ty - p.Y));
            var step = ProjectileSpeed * dt;
            if (d > step + 0.25)
            {
                // Missiles weave a little so a volley doesn't fly as one dot.
                var weave = p.Index == 0 ? 0 : Math.Sin((Time - p.LaunchAt) * 12 + p.Index) * 0.04 * (p.Index % 2 == 0 ? 1 : -1);
                p.X += (tx - p.X) / d * step - (ty - p.Y) / d * weave;
                p.Y += (ty - p.Y) / d * step + (tx - p.X) / d * weave;
                continue;
            }
            _projectiles.Remove(p);
            ProjectileHits(p);
        }
    }

    private void ProjectileHits(Projectile p)
    {
        var target = p.Target;
        var ability = p.Ability;
        var dice = ability.DamageAt(p.Rank);
        var critical = false;
        int damage;
        if (ability.Kind == AbilityKind.SpellBolt)
        {
            var result = Strike.ResolveSpellAttack(Hero.Character, target.Creature, dice, ability.DamageType, Random);
            critical = result.Degree == DegreeOfSuccess.CriticalSuccess;
            damage = result.Damage;
            if (damage == 0)
            {
                Float(target, result.Label, FloatingTextKind.Miss);
                Aggro(target);
                return;
            }
            if (critical && ability.CritPersistent is { } burn)
            {
                var persistent = Dice.Parse(burn);
                target.Creature.Persistent.Add(ability.DamageType, persistent with { Count = persistent.Count + (p.Rank - 1) });
            }
        }
        else
        {
            damage = DamageCalculator.ApplyDefenses(dice.Roll(Random), ability.DamageType, target.Creature.Defenses);
        }
        target.Creature.TakeDamage(damage);
        Float(target, critical ? $"{damage}!" : damage.ToString(), critical ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
        target.HitFlinch = 0.15;
        Aggro(target);
        AddImpact(target.X, target.Y, ability.DamageType);
        if (target.IsDead)
            Kill(target, critical);
    }

    private void AddImpact(double x, double y, DamageType type) => _impacts.Add(new Impact(x, y, type, Time));

    private void HeroStrike(MonsterActor target, int extraDice = 0, int cadenceAttacks = 1)
    {
        var result = Strike.Resolve(Hero.Character, target.Creature, Hero.Cadence, Time, Random,
            attackSpeedBonus: Hero.Character.Gear.AttackSpeed, extraDice: extraDice, cadenceAttacks: cadenceAttacks);
        var critical = result.Degree == DegreeOfSuccess.CriticalSuccess;
        if (result.Damage > 0)
        {
            target.Creature.TakeDamage(result.Damage);
            Hero.Resource.OnCombat(critical);
            Float(target, critical ? $"{result.Damage}!" : result.Damage.ToString(), critical ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
            target.HitFlinch = 0.15;
            Aggro(target);
        }
        else
        {
            Float(target, result.Label, FloatingTextKind.Miss);
        }
        if (result.AttackerStaggered)
            Hero.StaggerRemaining = ActionTiming.CritFailStaggerSeconds;
        if (target.IsDead)
            Kill(target, critical);
    }


    private void Kill(MonsterActor monster, bool critical)
    {
        if (monster.SpawnIndex >= 0)
            _slain.Add(monster.SpawnIndex);
        monster.State = MonsterState.Dead;
        monster.Path.Clear();
        monster.Play(critical && HasAnimation(monster, "critdie") ? "critdie" : "die", restart: true);
        if (monster.SummonedBy is null || monster.Creature.Stats.Rank != CreatureRank.Minion)
        {
            var xp = (int)Math.Round(Encounter.CreatureXp(monster.Creature.Stats.Level, Hero.Character.Level)
                * RankXpWeight(monster.Creature.Stats.Rank));
            if (xp > 0)
                GainXp(xp, monster);
        }
        if (Hero.Order?.Target == monster)
            Hero.Order = null;
        if (monster.SummonedBy is null || monster.Creature.Stats.Rank != CreatureRank.Minion)
            DropLoot(monster);
    }

    /// <summary>Set by the app so a death can pick "critdie" only for sprites that have it.</summary>
    public Func<MonsterActor, string, bool> HasAnimation { get; set; } = (_, _) => false;

    private static double RankXpWeight(CreatureRank rank) => rank switch
    {
        CreatureRank.Herald or CreatureRank.Prophet => 10.0,
        _ => Encounter.RankWeight(rank),
    };

    private void GainXp(int xp, Actor at)
    {
        Float(at, $"+{xp} XP", FloatingTextKind.Xp, 0.6);
        var gained = Experience.Add(xp);
        for (var i = 0; i < gained; i++)
        {
            var next = Hero.Character.Level + 1;
            // The vertical slice has no level-up screen yet: boost levels pick the key attribute, CON, DEX and WIS.
            IReadOnlyCollection<AttributeKind>? boosts = AttributeSet.BoostLevels.Contains(next)
                ? new[] { Hero.Character.KeyAttribute, AttributeKind.Constitution, AttributeKind.Dexterity, AttributeKind.Wisdom }
                    .Concat(Enum.GetValues<AttributeKind>()).Distinct().Take(4).ToArray()
                : null;
            Hero.Character.LevelUp(boosts);
            Say($"Level {Hero.Character.Level}!");
            Float(Hero, "LEVEL UP", FloatingTextKind.Info, 1.2);
        }
    }

    private void UpdateMonster(MonsterActor m, double dt)
    {
        m.Advance(dt);
        if (m.IsDead)
        {
            m.DeadFor += dt;
            return;
        }
        m.Creature.Conditions.Update(dt);
        if (m.HitFlinch > 0)
            m.HitFlinch -= dt;
        foreach (var (type, amount) in m.Creature.Persistent.Update(dt, Random))
        {
            var dealt = DamageCalculator.ApplyDefenses(amount, type, m.Creature.Defenses);
            m.Creature.TakeDamage(dealt);
            Float(m, dealt.ToString(), FloatingTextKind.DamageDealt);
            if (m.IsDead)
            {
                Kill(m, false);
                return;
            }
        }

        var hero = Hero;
        var def = m.Definition;
        var distance = m.DistanceTo(hero);

        if (m.State == MonsterState.Idle)
        {
            if (!hero.IsDead && distance <= def.AggroRadius && Map.HasLineOfSight(m.X, m.Y, hero.X, hero.Y))
                Aggro(m);
            else
            {
                m.Play("stance");
                return;
            }
        }

        if (hero.IsDead || distance > LeashDistance)
        {
            m.State = MonsterState.Idle;
            m.Path.Clear();
            m.AttackElapsed = -1;
            m.Play("stance");
            return;
        }
        if (!m.Creature.Conditions.CanAct)
            return;

        if (def.IsSpawner)
        {
            UpdateSpawner(m, dt);
            return;
        }

        if (m.AttackCooldown > 0)
            m.AttackCooldown -= dt;

        if (m.AttackElapsed >= 0)
        {
            var interval = 1.0 / def.AttacksPerSecond;
            var windup = Math.Min(0.6, interval * 0.6);
            m.AttackElapsed += dt;
            if (!m.AttackLanded && m.AttackElapsed >= windup * 0.6)
            {
                m.AttackLanded = true;
                if (distance <= def.AttackRange + hero.Radius + 0.4)
                    MonsterStrike(m);
            }
            if (m.AttackElapsed >= windup)
            {
                m.AttackElapsed = -1;
                m.AttackCooldown = interval - windup;
            }
            return;
        }

        var inRange = distance <= def.AttackRange + hero.Radius
            && (!def.Ranged || Map.HasLineOfSight(m.X, m.Y, hero.X, hero.Y));
        if (inRange)
        {
            m.Path.Clear();
            m.Face(hero.X, hero.Y);
            if (m.AttackCooldown <= 0)
            {
                m.State = MonsterState.Attack;
                m.AttackElapsed = 0;
                m.AttackLanded = false;
                var anim = def.Ranged ? (def.Sprite.Contains("mage") ? "cast" : "shoot") : "swing";
                var windup = Math.Min(0.6, 0.6 / def.AttacksPerSecond);
                m.Play(anim, 0.5 / windup, restart: true);
            }
            else if (m.Animation is not ("swing" or "shoot" or "cast") || m.AnimationTime > 1)
            {
                m.Play("stance");
            }
            return;
        }

        if (def.Stationary)
            return;
        m.State = MonsterState.Chase;
        m.RepathIn -= dt;
        if (m.Path.Count == 0 || m.RepathIn <= 0)
        {
            PlanPath(m, hero.X, hero.Y);
            m.RepathIn = 0.5 + Random.Roll(0, 20) / 100.0;
        }
        if (FollowPath(m, def.MoveSpeed * m.Creature.Conditions.ActionTimeMultiplier switch { > 1 => 1 / m.Creature.Conditions.ActionTimeMultiplier, _ => 1 }, dt, null))
            m.Play("run");
        else
            m.Play("stance");
    }

    private void UpdateSpawner(MonsterActor m, double dt)
    {
        var def = m.Definition;
        m.SummonIn -= dt;
        if (m.SummonIn > 0 || m.Summoned >= def.SummonCap || def.Summons is null)
        {
            if (m.Animation != "spawn" || m.AnimationTime > 1.2)
                m.Play("stance");
            return;
        }
        m.SummonIn = def.SummonEverySeconds;
        var alive = _monsters.Count(x => x.SummonedBy == m && !x.IsDead);
        if (alive >= 3)
            return;
        m.Summoned++;
        var child = SpawnNear(def.Summons, CreatureRank.Minion, m.X, m.Y, 1.5, m.PackId);
        child.SummonedBy = m;
        child.Play("spawn", restart: true);
        Aggro(child);
        m.Play("spawn", restart: true);
    }

    private void MonsterStrike(MonsterActor m)
    {
        var result = Strike.Resolve(m.Creature, Hero.Character, m.Cadence, Time, Random);
        if (result.Damage > 0)
        {
            var outcome = Hero.Character.TakeDamage(result.Damage);
            Hero.Resource.OnCombat(taken: true);
            Float(Hero, result.Damage.ToString(), FloatingTextKind.DamageTaken);
            if (outcome == DeathOutcome.RevivedByHeroPoint)
            {
                Say("A Hero Point pulls you back from death!");
                Float(Hero, "HERO POINT", FloatingTextKind.Info, 1.0);
            }
            else if (outcome == DeathOutcome.Died)
            {
                HeroDied();
            }
            else if (!Hero.IsSwinging && Hero.Path.Count == 0)
            {
                Hero.Play("hit", restart: true);
            }
        }
        else
        {
            Float(Hero, "miss", FloatingTextKind.Miss);
        }
    }

    private void HeroDied()
    {
        Hero.Play("die", restart: true);
        Hero.RespawnIn = RespawnSeconds;
        Hero.Order = null;
        Hero.Action = null;
        Hero.PickupTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        _projectiles.Clear();
        Say("You have fallen. The Ember Heart drags you back to the shrine...");
    }

    private void Respawn()
    {
        Experience.ApplyDeathPenalty();
        Hero.Character.RestAtShrine();
        Hero.X = RespawnPoint.X;
        Hero.Y = RespawnPoint.Y;
        Hero.HealRemaining = 0;
        Hero.Resource.Reset();
        Hero.ReadyAt.Clear();
        Hero.FlourishReadyAt = 0;
        Hero.Play("stance", restart: true);
        foreach (var m in _monsters.Where(m => !m.IsDead))
        {
            m.State = MonsterState.Idle;
            m.Path.Clear();
            m.AttackElapsed = -1;
        }
    }

    private void ActivateShrine(ShrineDefinition shrine)
    {
        var at = (shrine.At[0] + 0.5, shrine.At[1] + 1.5);
        var first = _activatedShrines.Add(shrine.Id);
        if (!first && RespawnPoint == at)
            return;
        RespawnPoint = at;
        ShrineActivated?.Invoke(shrine);
        var c = Hero.Character;
        c.Potion.Refill();
        c.Taint.Cleanse();
        if (first)
        {
            c.Heal(c.MaxHitPoints);
            Say($"{shrine.Name} awakens. Potions refilled; you will return here if you fall.");
            Float(Hero, "SHRINE", FloatingTextKind.Info, 1.0);
        }
    }

    private void Aggro(MonsterActor m)
    {
        if (m.IsDead || m.State != MonsterState.Idle)
            return;
        m.State = MonsterState.Chase;
        foreach (var other in _monsters.Where(o => o.PackId == m.PackId && o != m && !o.IsDead && o.State == MonsterState.Idle))
        {
            if (other.DistanceTo(m) <= PackAggroRadius)
                Aggro(other);
        }
    }

    /// <summary>Kills a monster outright through the normal death path (XP, loot, the slain list). Tests and the debug menu.</summary>
    internal void Slay(MonsterActor monster)
    {
        if (monster.IsDead)
            return;
        monster.Creature.TakeDamage(monster.Creature.HitPoints);
        Kill(monster, false);
    }

    // ----- Restoring a save -------------------------------------------------------------

    /// <summary>
    /// Puts a saved world back: the hero's place, the respawn point and shrines, the monsters already killed, the
    /// loot left lying around and whether the zone was finished. Called right after construction.
    /// </summary>
    internal void Restore(double heroX, double heroY, (double X, double Y) respawn, IEnumerable<string> shrines,
        IEnumerable<int> slain, IEnumerable<GroundItem> ground, bool complete, int level, int xp)
    {
        Hero.X = heroX;
        Hero.Y = heroY;
        RespawnPoint = respawn;
        foreach (var id in shrines)
            _activatedShrines.Add(id);
        foreach (var index in slain)
            _slain.Add(index);
        _monsters.RemoveAll(m => m.SpawnIndex >= 0 && _slain.Contains(m.SpawnIndex));
        _ground.AddRange(ground);
        ZoneComplete = complete;
        Experience.Restore(level, xp);
    }

    // ----- Movement ------------------------------------------------------------------

    private void PlanPath(Actor actor, double x, double y)
    {
        actor.Path.Clear();
        if (WalkableLine(actor.X, actor.Y, x, y))
        {
            actor.Path.Add((x, y));
            return;
        }
        var path = Pathfinder.Find(Map, ((int)actor.X, (int)actor.Y), ((int)Math.Floor(x), (int)Math.Floor(y)));
        if (path is null)
            return;
        if (path.Count > 0 && Map.IsWalkable(x, y))
            path[^1] = (x, y);
        // Drop waypoints we can already see past, so movement isn't stuck to the grid.
        var smoothed = new List<(double X, double Y)>();
        var from = (actor.X, actor.Y);
        for (var i = 0; i < path.Count; i++)
        {
            if (i + 1 < path.Count && WalkableLine(from.Item1, from.Item2, path[i + 1].X, path[i + 1].Y))
                continue;
            smoothed.Add(path[i]);
            from = path[i];
        }
        actor.Path.AddRange(smoothed);
    }

    private bool WalkableLine(double x0, double y0, double x1, double y1)
    {
        var distance = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        var steps = Math.Max(1, (int)(distance * 5));
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            double px = x0 + (x1 - x0) * t, py = y0 + (y1 - y0) * t;
            if (!Map.IsWalkable(px, py) || !Map.IsWalkable(px + 0.25, py) || !Map.IsWalkable(px - 0.25, py)
                || !Map.IsWalkable(px, py + 0.25) || !Map.IsWalkable(px, py - 0.25))
                return false;
        }
        return true;
    }

    /// <summary>Moves along the path; returns false once there is nowhere left to go.</summary>
    private static bool FollowPath(Actor actor, double speed, double dt, (double X, double Y)? _)
    {
        var budget = speed * dt;
        var moved = false;
        while (budget > 0 && actor.Path.Count > 0)
        {
            var (tx, ty) = actor.Path[0];
            var d = actor.DistanceTo(tx, ty);
            if (d < 1e-4)
            {
                actor.Path.RemoveAt(0);
                continue;
            }
            actor.Face(tx, ty);
            var step = Math.Min(budget, d);
            actor.X += (tx - actor.X) / d * step;
            actor.Y += (ty - actor.Y) / d * step;
            budget -= step;
            moved = true;
            if (step >= d)
                actor.Path.RemoveAt(0);
        }
        return moved || actor.Path.Count > 0;
    }

    /// <summary>Soft separation so monsters crowd around the hero instead of stacking on one spot.</summary>
    private void Separate()
    {
        var living = _monsters.Where(m => !m.IsDead).ToList();
        for (var i = 0; i < living.Count; i++)
        {
            var a = living[i];
            Push(a, Hero, onlyFirst: true);
            for (var j = i + 1; j < living.Count; j++)
                Push(a, living[j], onlyFirst: false);
        }

        void Push(Actor a, Actor b, bool onlyFirst)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var min = a.Radius + b.Radius;
            if (d >= min || d < 1e-6)
                return;
            var push = (min - d) * (onlyFirst ? 1.0 : 0.5);
            double nx = dx / d * push, ny = dy / d * push;
            TryShift(a, nx, ny);
            if (!onlyFirst)
                TryShift(b, -nx, -ny);
        }

        void TryShift(Actor actor, double dx, double dy)
        {
            if (actor is MonsterActor { Definition.Stationary: true })
                return;
            if (Map.IsWalkable(actor.X + dx, actor.Y + dy))
            {
                actor.X += dx;
                actor.Y += dy;
            }
        }
    }

    // ----- Helpers -------------------------------------------------------------------

    private MonsterActor SpawnNear(string creatureId, CreatureRank rank, double x, double y, double radius, int packId)
    {
        var def = _creatures.TryGetValue(creatureId, out var d) ? d : throw new InvalidDataException($"Unknown creature '{creatureId}'.");
        var stats = CreatureStats.For(Zone.Level, rank);
        var dice = def.Damage is { } text ? Dice.Parse(text) : stats.DefaultStrikeDamage;
        var title = rank switch
        {
            CreatureRank.Champion => $"{def.Name} Champion",
            CreatureRank.Elite => $"{def.Name}, the Elite",
            _ => def.Name,
        };
        var creature = new Creature(title, stats, dice, def.DamageType, def.BuildDefenses(), def.AttacksPerSecond);
        double px = x, py = y;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var angle = Random.Roll(0, 359) * Math.PI / 180;
            var r = radius * Random.Roll(0, 100) / 100.0;
            px = x + Math.Cos(angle) * r;
            py = y + Math.Sin(angle) * r;
            if (Map.IsWalkable(px, py))
                break;
            (px, py) = (x, y);
        }
        var monster = new MonsterActor(def, creature, packId)
        {
            X = px,
            Y = py,
            Direction = Random.Roll(0, 7),
            Radius = def.Stationary ? 0.6 : 0.35 * def.Scale,
        };
        if (def.IsSpawner)
            monster.SummonIn = 1.0;
        _monsters.Add(monster);
        return monster;
    }

    private static double SwingRate(double swingSeconds) => 0.4 / Math.Max(0.1, swingSeconds);

    private void Float(Actor at, string text, FloatingTextKind kind, double lift = 0) =>
        _texts.Add(new FloatingText(at.X - lift * 0.3, at.Y - lift * 0.3, text, kind, Time));

    /// <summary>Shows a message at the top of the screen (saving, loading).</summary>
    public void Announce(string message) => Say(message);

    private void Say(string message)
    {
        LastMessage = message;
        LastMessageAt = Time;
    }
}
