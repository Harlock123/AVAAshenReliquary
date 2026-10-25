using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Act 4's dangers (M4 step 8):
/// <list type="bullet">
/// <item>Ash storms (the Ash Wastes, a zone with <see cref="ZoneDefinition.AshStorms"/>): every so often the ash comes
/// down and nothing more than <see cref="AshStormSightTiles"/> off can be seen, by the hero or by the monsters: what is
/// beyond is out of the light, the hero can't pick it out, a monster doesn't notice the hero beyond it and an archer
/// must close in to shoot.</item>
/// <item>Embers (the Lava Rivers): a crust over the lava that can be walked but burns, a basic Fortitude save each second
/// against fire (the hero's fire resistance counts); lava itself can't be walked but on a bridge.</item>
/// <item>Pack leaders (the AI's fourth behaviour): a pack with a leader fights harder while it stands within
/// <see cref="LeaderTiles"/> (+1 to hit, a tenth more damage); in a fight it cries them on every
/// <see cref="LeaderCryEverySeconds"/>, quickening them and putting back a little of their health; when it falls, those
/// it led are frightened.</item>
/// <item>Wards (the Reliquary): a passage sealed until its flags hold.</item>
/// <item>The Herald Echoes (the Reliquary's gauntlet, the spec's Echo rule): a Herald whose relic wasn't destroyed comes
/// back as an Echo, at <see cref="EchoHealth"/> of its health and without its last phase; its fall sets its pack's flag,
/// which opens the ward to the next.</item>
/// <item>A pet (the drakeling hatched from the wyrm's egg): an ally that follows any hero, beside a companion.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>Ash storms: one every so often, lasting a while, and how far can be seen in one.</summary>
    public const double AshStormEverySeconds = 60, AshStormSeconds = 20, AshStormSightTiles = 5;

    /// <summary>How often embers burn the hero standing on them.</summary>
    public const double EmberBurnSeconds = 1.0;

    /// <summary>How far a pack leader's command reaches, and how often it cries its pack on (and for how long).</summary>
    public const double LeaderTiles = 8, LeaderCryEverySeconds = 10, LeaderCrySeconds = 4;

    /// <summary>What a war cry gives back of each one's health; how long its pack is frightened when the leader falls.</summary>
    public const double LeaderCryHeal = 0.1, LeaderFallSeconds = 6;

    /// <summary>A Herald's Echo has this share of its health.</summary>
    public const double EchoHealth = 0.6;

    private double _ashStormAt = AshStormEverySeconds;
    private readonly HashSet<string> _openGates = [];

    /// <summary>An ash storm blowing (a zone with storms): sight shrinks, and the renderer greys the air.</summary>
    public bool AshStorm => Zone.AshStorms && Time >= _ashStormAt && Time < _ashStormAt + AshStormSeconds;

    /// <summary>How far can be seen through the ash: <see cref="AshStormSightTiles"/> in a storm, else without end.</summary>
    public double SightTiles => AshStorm ? AshStormSightTiles : double.PositiveInfinity;

    /// <summary>Lost in the ash: farther from the hero than can be seen in a storm.</summary>
    private bool LostInTheAsh(double x, double y) =>
        AshStorm && (x - Hero.X) * (x - Hero.X) + (y - Hero.Y) * (y - Hero.Y) > AshStormSightTiles * AshStormSightTiles;

    private void UpdateAshStorm(double dt)
    {
        if (!Zone.AshStorms)
            return;
        if (Time >= _ashStormAt + AshStormSeconds)
            _ashStormAt += AshStormEverySeconds + AshStormSeconds;
        if (Time >= _ashStormAt && Time - dt < _ashStormAt)
            Say("The ash comes down like snow, and the world shrinks to a few paces round you.");
    }

    /// <summary>How near a monster must be to attack in a storm: an archer closes in to see its mark.</summary>
    private double ReachOf(MonsterActor m) => Math.Min(m.Definition.AttackRange, SightTiles);

    // ----- Embers --------------------------------------------------------------------

    /// <summary>The hero standing on embers: a basic Fortitude save each second against fire, as the zone's level.</summary>
    private void UpdateEmbers(double dt)
    {
        var hero = Hero;
        if (hero.IsDead || hero.IsDodging || !Map.IsEmbers(hero.X, hero.Y))
        {
            hero.BurnIn = Math.Min(hero.BurnIn, 0.25);
            return;
        }
        hero.BurnIn -= dt;
        if (hero.BurnIn > 0)
            return;
        hero.BurnIn = EmberBurnSeconds;
        var degree = Check.Roll(Random, hero.Character.Save(StatisticKind.Fortitude).Total, 14 + Zone.Level).Degree;
        var dealt = DamageCalculator.BasicSave(new Dice(1 + Zone.Level / 4, 6).Roll(Random), degree, DamageType.Fire, hero.Character.Defenses);
        if (dealt <= 0)
        {
            Float(hero, "resisted", FloatingTextKind.Miss, 0.3);
            return;
        }
        Float(hero, $"burning {dealt}", FloatingTextKind.DamageTaken, 0.3);
        AddImpact(hero.X, hero.Y, DamageType.Fire);
        HurtHero(dealt);
    }

    // ----- Pack leaders --------------------------------------------------------------

    /// <summary>Driven by a leader that stands near it (its leader does not drive itself).</summary>
    internal bool Commanded(MonsterActor m) =>
        m.Leader is { IsDead: false } leader && leader != m && leader.DistanceTo(m) <= LeaderTiles;

    /// <summary>Quickened by its leader's war cry.</summary>
    internal bool Cried(MonsterActor m) => Time < m.CriedUntil;

    /// <summary>A leader in a fight cries its pack on: quicker for a while, and a little of their health back.</summary>
    private void UpdateLeader(MonsterActor m, double dt)
    {
        if (!m.IsLeader || m.State == MonsterState.Idle)
            return;
        m.CryIn -= dt;
        if (m.CryIn > 0)
            return;
        m.CryIn = LeaderCryEverySeconds;
        var led = _monsters.Where(o => !o.IsDead && o.Leader == m && o.DistanceTo(m) <= LeaderTiles).ToList();
        if (led.Count == 0)
            return;
        foreach (var o in led)
        {
            o.CriedUntil = Time + LeaderCrySeconds;
            o.Creature.Heal(Math.Max(1, (int)(o.Creature.Stats.HitPoints * LeaderCryHeal)));
            if (o.State == MonsterState.Idle)
                Aggro(o);
        }
        Float(m, "RALLY!", FloatingTextKind.Critical, 0.8);
        Sound(SoundCue.MonsterAggro, m, m.Definition.Voice);
    }

    /// <summary>A leader falls: those it led, near it, are frightened and lose their drive.</summary>
    private void LeaderFalls(MonsterActor leader)
    {
        if (!leader.IsLeader)
            return;
        var broken = false;
        foreach (var o in _monsters.Where(o => !o.IsDead && o != leader && o.Leader == leader))
        {
            o.CriedUntil = 0;
            if (o.DistanceTo(leader) > LeaderTiles)
                continue;
            o.Creature.Conditions.Apply(ConditionKind.Frightened, 1, LeaderFallSeconds);
            broken = true;
        }
        if (broken)
            Float(leader, "the pack breaks", FloatingTextKind.Info, 0.8);
    }

    // ----- Wards ---------------------------------------------------------------------

    /// <summary>Whether a ward has burned out (its flags hold).</summary>
    public bool GateOpen(GateDefinition gate) => _openGates.Contains(gate.Id);

    /// <summary>The zone's wards: closed ones block their cells; one whose flags now hold burns out.</summary>
    private void UpdateGates(bool quiet = false)
    {
        foreach (var gate in Zone.Gates ?? [])
        {
            var open = Holds(gate.Requires);
            if (open == _openGates.Contains(gate.Id))
                continue;
            if (open)
                _openGates.Add(gate.Id);
            else
                _openGates.Remove(gate.Id);
            foreach (var cell in gate.Cells)
                Map.SetGate(cell[0], cell[1], !open);
            if (open && !quiet)
            {
                Say($"{gate.Name} burns out, and the way is open.");
                var (x, y) = (gate.Cells.Average(c => c[0]) + 0.5, gate.Cells.Average(c => c[1]) + 0.5);
                AddImpact(x, y, DamageType.Fire);
                Sound(SoundCue.Ward, x, y);
            }
        }
    }

    /// <summary>Wards closed again for a new zone (they open at once if their flags already hold).</summary>
    private void PopulateGates()
    {
        _openGates.Clear();
        foreach (var gate in Zone.Gates ?? [])
        {
            foreach (var cell in gate.Cells)
                Map.SetGate(cell[0], cell[1], true);
        }
        UpdateGates(quiet: true);
    }

    // ----- Spawns that wait on flags, leaders and Echoes -------------------------------

    /// <summary>Whether a pack comes: its <see cref="SpawnDefinition.Requires"/> hold and its <see cref="SpawnDefinition.Unless"/> don't.</summary>
    private bool Comes(SpawnDefinition spawn) =>
        (spawn.Requires is null || Holds(spawn.Requires)) && (spawn.Unless is not { Count: > 0 } unless || !Holds(unless));

    /// <summary>
    /// Packs that didn't come count as fallen: each one's flag is set, unless another pack that came sets the same
    /// flag (the Choir's Echo comes whole, or alone).
    /// </summary>
    private void SettleAbsentPacks(ZoneDefinition zone)
    {
        var present = new HashSet<string>(zone.Spawns.Where(Comes).Select(s => s.Flag).OfType<string>());
        foreach (var spawn in zone.Spawns.Where(s => !Comes(s) && s.Flag is not null && !present.Contains(s.Flag!)))
        {
            if (Flag(spawn.Flag!) is null)
                SetFlag(spawn.Flag!, spawn.Value ?? "cleared");
        }
    }

    /// <summary>
    /// After a load brings the flags back (the zone was set up before them): the packs that wait on flags are placed
    /// again as the flags now say, those already slain left out, and the wards set to match.
    /// </summary>
    private void SettleFlaggedPacks()
    {
        var index = 0;
        var packId = 0;
        foreach (var spawn in Zone.Spawns)
        {
            packId++;
            var first = index;
            index += spawn.Pack.Sum(p => p.Count);
            if (spawn.Requires is null && spawn.Unless is null)
                continue;
            _monsters.RemoveAll(m => m.PackId == packId);
            if (!Comes(spawn))
                continue;
            var at = first;
            var pack = new List<MonsterActor>();
            foreach (var member in spawn.Pack)
            {
                for (var i = 0; i < member.Count; i++, at++)
                {
                    if (_slain.Contains(at))
                        continue;
                    var m = SpawnNear(member.Creature, member.Rank, spawn.At[0] + 0.5, spawn.At[1] + 0.5, spawn.Radius, packId, spawn.Level,
                        member.Affixes, member);
                    m.SpawnIndex = at;
                    pack.Add(m);
                }
            }
            Muster(pack);
        }
        SettleAbsentPacks(Zone);
        PopulateGates();
    }

    /// <summary>A pack's members once all are placed: each one led by the pack's leader, if it has one.</summary>
    private void Muster(IReadOnlyList<MonsterActor> pack)
    {
        if (pack.FirstOrDefault(m => m.IsLeader) is not { } leader)
            return;
        foreach (var m in pack)
            m.Leader = leader;
        leader.CryIn = LeaderCryEverySeconds / 2;
    }

    /// <summary>The last phase an Echo enters (never its Herald's last, but the first if that is all there is).</summary>
    internal static int LastPhase(MonsterActor m) =>
        m.Definition.Boss is not { } boss ? 0
        : m.EchoLastPhase is { } held ? Math.Min(held, boss.Phases.Count - 1)
        : Math.Max(0, boss.Phases.Count - 1 - (m.IsEcho && boss.Phases.Count > 1 ? 1 : 0));

    // ----- The pet -------------------------------------------------------------------

    /// <summary>The hero's pet, or null.</summary>
    public AllyActor? Pet => _allies.FirstOrDefault(a => a.IsPet);

    /// <summary>The pet a boon calls for: brought to the hero's side when first gained, its numbers following their level.</summary>
    private void RefreshPet()
    {
        var c = Hero.Character;
        if (c.Pet is not { } id || !_creatures.TryGetValue(id, out var definition))
        {
            _allies.RemoveAll(a => a.IsPet);
            return;
        }
        if (Pet is not { } pet || pet.Definition.Id != id)
        {
            _allies.RemoveAll(a => a.IsPet);
            _allies.Add(new AllyActor(definition, CompanionStats(definition, c.Level, 0), c.Level, 0) { IsPet = true });
            PlaceAllies();
            return;
        }
        if (pet.Level == c.Level)
            return;
        var share = (double)pet.Creature.HitPoints / pet.Creature.Stats.HitPoints;
        pet.Creature = CompanionStats(definition, c.Level, 0);
        pet.Creature.SetHitPoints(pet.IsDead && pet.ReturnIn > 0 ? 0 : (int)Math.Ceiling(share * pet.Creature.Stats.HitPoints));
        pet.Level = c.Level;
    }
}
