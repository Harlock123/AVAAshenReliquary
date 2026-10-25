using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// The Heart's Pull (M5 step 3), Malachar's last phase, and his daughter's story:
/// <list type="bullet">
/// <item>The pull (<see cref="HeartsPull"/>): rooted at the Throne, he tears at the Ember Heart. The hero is held while it
/// lasts; every check is a Fortitude save against his DC, each press of the pull key (Space) since the last adding 1. The
/// hero who holds staggers him; the one who doesn't falls. He and his court still strike while he pulls.</item>
/// <item>The parley: the hero who carries his daughter's toy (a carved lantern-bird from the Gallows Road's refugee
/// camp) into the phase can show it to him first. The fight stops while they talk; the memories of her found on the way
/// (in Ironhold, Saint Oriel's Rest, the Ember Gate camp and his own journal in the Sanctum) make him readier to listen. A
/// <c>spare</c> ends it with him spared, kneeling by the Throne, and his court in ash; anything else and the pull begins.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>The flags of his daughter's story: <c>daughter.toy</c> (carried, shown) and one per memory found.</summary>
    public const string DaughterFlag = "daughter.";

    /// <summary>The memories of her, by place: each sets <c>daughter.&lt;place&gt;</c>; their count is <c>daughter.memories</c>.</summary>
    public static readonly IReadOnlyList<string> DaughterMemories = ["ironhold", "oriel", "ember_gate", "journal"];

    /// <summary>The Heart's Pull under way, or null.</summary>
    public HeartsPull? Pull { get; private set; }

    /// <summary>Whether a boss has stayed its hand to talk (the fight holds while the parley lasts).</summary>
    public bool Parleying => _parley is not null;

    private MonsterActor? _parley;

    /// <summary>One press of the pull key: the next check is a little stronger.</summary>
    public void PullHarder()
    {
        if (Pull is not { } pull || Hero.IsDead)
            return;
        pull.Presses++;
        pull.LastPressAt = Time;
    }

    /// <summary>
    /// A pulling phase's turn, from <see cref="UpdateBoss"/>: the parley holding the fight, a stagger, the pull's checks,
    /// a new pull when it comes round. True while the boss makes no attack (staggered, or talking).
    /// </summary>
    private bool UpdatePull(MonsterActor m, BossPhase phase, double dt)
    {
        if (!phase.Pull)
            return false;
        if (_parley == m)
        {
            m.Play("stance");
            return true;
        }
        if (Time < m.StaggeredUntil)
        {
            m.Play("stance");
            return true;
        }
        if (Pull is { } pull && pull.Puller == m)
        {
            if (Time >= pull.NextCheckAt)
                PullCheck(m, phase, pull);
            return false;
        }
        m.PullIn -= dt;
        if (m.PullIn > 0 || Hero.IsDead)
            return false;
        Pull = new HeartsPull(m, Time + phase.PullCheckSeconds);
        Hero.Order = null;
        Hero.Action = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Float(Hero, "THE HEART'S PULL", FloatingTextKind.Critical, 1.2);
        Say("Malachar reaches into your chest for the Ember Heart. Hold on to it: press Space, quickly!");
        Sound(SoundCue.WitchCast, m);
        return false;
    }

    /// <summary>A check of the pull: a Fortitude save, the presses since the last added (to a cap).</summary>
    private void PullCheck(MonsterActor m, BossPhase phase, HeartsPull pull)
    {
        var bonus = Math.Min(pull.Presses, phase.PullPressCap);
        var degree = Check.Roll(Random, Hero.Character.Save(StatisticKind.Fortitude).Total + bonus, m.Creature.Stats.SaveDc).Degree;
        pull.Last = degree;
        pull.Presses = 0;
        pull.Meter = Math.Clamp(pull.Meter + HeartsPull.Shift(degree), 0, 100);
        pull.NextCheckAt = Time + phase.PullCheckSeconds;
        Float(Hero, degree >= DegreeOfSuccess.Success ? "you hold" : "it slips", degree >= DegreeOfSuccess.Success ? FloatingTextKind.Info : FloatingTextKind.Miss, 0.5);
        if (pull.Meter >= 100)
        {
            Pull = null;
            m.StaggeredUntil = Time + phase.PullStaggerSeconds;
            m.PullIn = phase.PullEverySeconds;
            Float(m, "STAGGERED", FloatingTextKind.Critical, 1.0);
            Say("The Heart holds. Malachar reels back from it, open to you.");
            Sound(SoundCue.Crit, m);
        }
        else if (pull.Meter <= 0)
        {
            Pull = null;
            m.PullIn = phase.PullEverySeconds;
            Say("Malachar tears the Ember Heart half out of you, and the world goes dark.");
            Float(Hero, "THE HEART TORN", FloatingTextKind.Critical, 1.2);
            HurtHero(Hero.Character.HitPoints + Hero.Character.MaxHitPoints);
        }
    }

    /// <summary>
    /// As a pulling phase begins: the boss's parley, if its flags hold. The fight holds while the dialogue is open; it
    /// stays open however far off the hero stands.
    /// </summary>
    private void BeginParley(MonsterActor m, BossPhase phase)
    {
        var boss = m.Definition.Boss!;
        if (!phase.Pull || boss.Parley is not { } id || !_dialogues.ContainsKey(id) || !Holds(boss.ParleyIf))
            return;
        _parley = m;
        Pull = null;
        Hero.Order = null;
        Hero.Action = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        StartDialogue(new Interactive(m.Definition.Id, m.Definition.Name, boss.BarName, [(int)Math.Floor(m.X), (int)Math.Floor(m.Y)], id, null, null));
    }

    /// <summary>The parley over without a <c>spare</c>: the fight goes on, and the pull comes.</summary>
    private void EndParley()
    {
        if (_parley is not { } m)
            return;
        _parley = null;
        m.PullIn = 0.5;
        Say("Malachar's face closes like a door. He reaches for the Heart.");
    }

    /// <summary>A <c>spare</c>: the boss is spared, its court goes to ash, and it kneels where it stood (an NPC now).</summary>
    private void SpareParley()
    {
        if (_parley is not { } m)
            return;
        _parley = null;
        Pull = null;
        var boss = m.Definition.Boss!;
        foreach (var other in _monsters.Where(x => x != m && !x.IsDead).ToList())
        {
            other.Creature.TakeDamage(other.Creature.HitPoints);
            other.State = MonsterState.Dead;
            other.Play("die", restart: true);
            Float(other, "ash", FloatingTextKind.Info, 0.5);
        }
        _monsters.Remove(m);
        ResetBurning(m);
        ClearBossAftermath();
        if (boss.DeathFlag is { } flag)
            SetFlag(flag, "spared");
        Arrival = new Banner($"{m.Definition.Name} is spared", "He kneels by the Throne he built, and the god's bones behind him go dark.", Time);
        // Whoever is there only once he is spared (Malachar, kneeling) comes in.
        foreach (var npc in (Zone.Npcs ?? []).Where(n => n.Requires is not null && Holds(n.Requires) && _interactives.All(i => i.Id != n.Id)))
            _interactives.Add(new Interactive(npc.Id, npc.Name, npc.Title, npc.At, npc.Dialogue, npc, null));
        BossSlain?.Invoke(m);
    }

    /// <summary>A memory of his daughter found: the count of them, which the parley's words hang on.</summary>
    private void CountMemories() =>
        SetFlag(DaughterFlag + "memories", DaughterMemories.Count(p => Flag(DaughterFlag + p) is not null).ToString());

    /// <summary>The hero fell: no pull, no parley, no stagger (the fight starts again).</summary>
    private void ResetPull(MonsterActor m)
    {
        if (Pull?.Puller == m)
            Pull = null;
        if (_parley == m)
            _parley = null;
        m.PullIn = 0;
        m.StaggeredUntil = 0;
    }
}
