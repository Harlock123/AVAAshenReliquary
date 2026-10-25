using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// The Choir of Whispers, Act 3's Herald (M4 step 7), on the boss framework:
/// <list type="bullet">
/// <item>Three sisters on one health bar: Sister Agnete is the boss; Brigid and Cille fight beside her
/// (<see cref="BossDefinition.Sisters"/>), and a blow at either lands on Agnete's health.</item>
/// <item>Reflections: below 60% the two withdraw into the bells and Agnete hides among images of herself, shuffled every
/// few seconds. An image shatters at a touch; a Perception check near her sees the true voice for a while.</item>
/// <item>The song: below 25% the three sing as one, and every 6 s the hero makes a Will save or is enthralled, walking to
/// the singer, able to do nothing else.</item>
/// <item>Three Candles (Act 3's Herald Remnant story): three candles in the tower's chapel, each on the wrong sister's
/// stand. Set right, they burn white (the Choir's Echo is gone, and the true voice is easier to find); snuffed out, the
/// Echo is weakened.</item>
/// <item>The Penitent: Brother Caddoc forgiven sings against the song (+2 to the saves); banished, he joins the Choir.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How near the hero must be to the true sister to try to pick out her voice, and how often.</summary>
    public const double TrueVoiceTiles = 10, TrueVoiceEverySeconds = 2;

    /// <summary>How long the true voice stays known once heard.</summary>
    public const double TrueVoiceSeconds = 6;

    /// <summary>What a candle set right adds to hearing the true voice; what Caddoc's counter-song adds to the Will saves.</summary>
    public const int CandlelightBonus = 4, CounterSongBonus = 2;

    /// <summary>The hero walks to the singer at this share of their speed, and stops this near.</summary>
    public const double EnthralledPace = 0.7, EnthralledStopTiles = 2.0;

    /// <summary>The XP for setting the three candles right, and for snuffing them.</summary>
    public const int CandlesXp = 900;

    /// <summary>The Choir's song holds the hero.</summary>
    public bool Enthralled => !Hero.IsDead && Time < Hero.EnthralledUntil;

    /// <summary>
    /// The Choir's turn, from <see cref="UpdateBoss"/>: the sisters come and go with the phases, the reflections shuffle,
    /// the hero listens for the true voice, the song is sung, and a late voice joins.
    /// </summary>
    private void UpdateChoir(MonsterActor m, BossDefinition boss, BossPhase phase, double dt)
    {
        // A Choir Echo half laid to rest (the candles snuffed) comes without her sisters.
        if (boss.Sisters is { Count: > 0 } sisters && !m.EchoAlone)
        {
            var standing = _monsters.Where(x => x.SharesWith == m && !x.IsReflection && !x.IsDead && x.Definition.Id != phase.Joins).ToList();
            if (phase.Withdraw && standing.Count > 0)
            {
                foreach (var sister in standing)
                {
                    Float(sister, "fades into the bells", FloatingTextKind.Info, 0.6);
                    _monsters.Remove(sister);
                    if (Hero.Order?.Target == sister)
                        Hero.Order = null;
                }
            }
            else if (!phase.Withdraw && standing.Count == 0)
            {
                for (var i = 0; i < sisters.Count; i++)
                {
                    var angle = Math.PI * (0.25 + 0.5 * i);
                    var sister = SpawnNear(sisters[i], CreatureRank.Champion, m.HomeX + Math.Cos(angle) * 3, m.HomeY + Math.Sin(angle) * 3, 0.5,
                        m.PackId, m.Creature.Stats.Level);
                    sister.SharesWith = m;
                    sister.SummonedBy = m;
                    Aggro(sister);
                }
                SyncChoir(m);
            }
        }

        if (phase.Reflections > 0 && phase.Reflection is { } reflection)
        {
            m.ShuffleIn -= dt;
            if (m.ShuffleIn <= 0)
            {
                m.ShuffleIn = phase.ReflectEverySeconds;
                Shuffle(m, boss, phase.Reflections, reflection);
            }
            ListenForTheTrueVoice(m, dt);
        }
        else if (_monsters.Any(x => x.IsReflection && x.SharesWith == m))
        {
            ShatterReflections(m);
        }

        if (phase.SongEverySeconds > 0)
        {
            m.SongIn -= dt;
            if (m.SongIn <= 0)
            {
                m.SongIn = phase.SongEverySeconds;
                Sing(m, phase);
            }
        }

        if (phase.Joins is { } joins && !m.Joined && (phase.JoinsIf is null || Holds(phase.JoinsIf)))
        {
            m.Joined = true;
            var late = SpawnNear(joins, CreatureRank.Champion, m.HomeX, m.HomeY + 5, 1.0, m.PackId, m.Creature.Stats.Level);
            late.SummonedBy = m;
            Aggro(late);
            Float(late, "joins the Choir", FloatingTextKind.Critical, 1.0);
            Say($"{late.Definition.Name} climbs into the belfry, singing with them.");
        }
    }

    /// <summary>The sisters' and reflections' health follows the Choir's one bar.</summary>
    private void SyncChoir(MonsterActor pool)
    {
        var share = (double)pool.Creature.HitPoints / pool.Creature.Stats.HitPoints;
        foreach (var x in _monsters.Where(x => x.SharesWith == pool && !x.IsDead))
            x.Creature.SetHitPoints(pool.IsDead ? 0 : Math.Max(1, (int)Math.Ceiling(share * x.Creature.Stats.HitPoints)));
    }

    /// <summary>
    /// The true sister goes somewhere new in her arena and her reflections stand at other spots, all alike; old
    /// reflections shatter, and whatever was known of her voice is lost.
    /// </summary>
    private void Shuffle(MonsterActor m, BossDefinition boss, int count, string reflection)
    {
        ShatterReflections(m, quiet: true);
        var spots = new List<(double X, double Y)>();
        for (var attempt = 0; attempt < 200 && spots.Count < count + 1; attempt++)
        {
            var angle = Random.Roll(0, 359) * Math.PI / 180;
            var r = boss.ArenaRadius * (0.25 + 0.45 * Random.Roll(0, 100) / 100.0);
            var (x, y) = (m.HomeX + Math.Cos(angle) * r, m.HomeY + Math.Sin(angle) * r);
            if (Map.IsWalkable(x, y) && spots.All(s => Math.Abs(s.X - x) + Math.Abs(s.Y - y) > 2.5) && Hero.DistanceTo(x, y) > 2.5)
                spots.Add((x, y));
        }
        if (spots.Count == 0)
            return;
        var real = Random.Roll(0, spots.Count - 1);
        (m.X, m.Y) = spots[real];
        m.Path.Clear();
        m.TrueVoiceUntil = 0;
        m.Play("spawn", restart: true);
        for (var i = 0; i < spots.Count; i++)
        {
            if (i == real)
                continue;
            var image = SpawnNear(reflection, m.Creature.Stats.Rank, spots[i].X, spots[i].Y, 0, m.PackId, m.Creature.Stats.Level);
            image.SharesWith = m;
            image.SummonedBy = m;
            image.IsReflection = true;
            image.Direction = m.Direction;
            image.Play("spawn", restart: true);
            Aggro(image);
        }
        SyncChoir(m);
        Sound(SoundCue.Ward, m, "monster");
    }

    private void ShatterReflections(MonsterActor m, bool quiet = false)
    {
        foreach (var image in _monsters.Where(x => x.IsReflection && x.SharesWith == m && !x.IsDead).ToList())
        {
            if (quiet)
                _monsters.Remove(image);
            else
                ShatterReflection(image);
        }
    }

    /// <summary>A reflection touched: it shatters, worth nothing.</summary>
    private void ShatterReflection(MonsterActor image)
    {
        image.Creature.TakeDamage(image.Creature.HitPoints);
        image.State = MonsterState.Dead;
        image.DeadFor = CorpseSeconds - 1.0;
        image.Path.Clear();
        Float(image, "a reflection", FloatingTextKind.Info, 0.4);
        Sound(SoundCue.Ward, image, "monster");
    }

    /// <summary>Near the true sister, every 2 s, the hero's Perception against her Deception (her Perception DC).</summary>
    private void ListenForTheTrueVoice(MonsterActor m, double dt)
    {
        if (Time < m.TrueVoiceUntil || m.DistanceTo(Hero) > TrueVoiceTiles)
            return;
        m.SpotIn -= dt;
        if (m.SpotIn > 0)
            return;
        m.SpotIn = TrueVoiceEverySeconds;
        var bonus = Flag("act3.candles") == "rekindled" ? CandlelightBonus : 0;
        if (Check.Roll(Random, Hero.Character.Perception().Total + bonus, m.Creature.PerceptionDc).Degree < DegreeOfSuccess.Success)
            return;
        m.TrueVoiceUntil = Time + TrueVoiceSeconds;
        Float(m, "the true voice", FloatingTextKind.Critical, 0.8);
        Say($"Among the reflections you hear the true voice: {m.Definition.Name}.");
    }

    /// <summary>The song: a Will save against the Choir's DC, or the hero is enthralled (twice as long on a critical failure).</summary>
    private void Sing(MonsterActor m, BossPhase phase)
    {
        if (Hero.IsDead || Enthralled)
            return;
        var singer = _monsters.Where(x => (x == m || x.SharesWith == m) && !x.IsDead && !x.IsReflection).MinBy(x => x.DistanceTo(Hero)) ?? m;
        Float(singer, "THE SONG", FloatingTextKind.Critical, 0.9);
        Sound(SoundCue.WitchCast, singer);
        if (Steadfast)
        {
            Float(Hero, "unmoved", FloatingTextKind.Miss, 0.6);
            return;
        }
        var bonus = Flag("act3.penitent") == "forgiven" ? CounterSongBonus : 0;
        var degree = Check.Roll(Random, Hero.Character.Save(StatisticKind.Will).Total + bonus, m.Creature.Stats.SaveDc).Degree;
        if (degree >= DegreeOfSuccess.Success)
        {
            Float(Hero, bonus > 0 ? "Caddoc sings against them" : "you hold", FloatingTextKind.Miss, 0.6);
            return;
        }
        Hero.EnthralledUntil = Time + phase.SongSeconds * (degree == DegreeOfSuccess.CriticalFailure ? 2 : 1);
        Hero.Enthraller = singer;
        Hero.Order = null;
        Hero.Action = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Float(Hero, "ENTHRALLED", FloatingTextKind.Critical, 1.0);
        Say("The song takes you: your feet carry you toward the singer.");
    }

    /// <summary>Enthralled, the hero walks to the singer and stands there. True while it holds (nothing else happens).</summary>
    private bool UpdateEnthralled(double dt)
    {
        if (!Enthralled)
            return false;
        var hero = Hero;
        hero.Order = null;
        hero.Action = null;
        if (hero.Enthraller is not { IsDead: false } singer || hero.DistanceTo(singer) <= EnthralledStopTiles)
        {
            hero.Path.Clear();
            hero.Play("stance");
            return true;
        }
        hero.RepathIn -= dt;
        if (hero.Path.Count == 0 || hero.RepathIn <= 0)
        {
            PlanPath(hero, singer.X, singer.Y);
            hero.RepathIn = 0.5;
        }
        if (FollowPath(hero, HeroSpeed() * EnthralledPace, dt, null))
            hero.Play("run");
        return true;
    }

    /// <summary>A command while enthralled does nothing (the hero is told why).</summary>
    private bool HeldBySong()
    {
        if (!Enthralled)
            return false;
        if (Time - _songToldAt > 0.6)
        {
            _songToldAt = Time;
            Float(Hero, "enthralled", FloatingTextKind.Miss, 0.4);
        }
        return true;
    }

    private double _songToldAt = double.NegativeInfinity;

    /// <summary>The Choir starts again (the hero fell): no song holds them, no late voice has come.</summary>
    private void ResetChoir(MonsterActor m)
    {
        m.Joined = false;
        m.TrueVoiceUntil = 0;
        m.ShuffleIn = 0;
        m.SongIn = 0;
        Hero.EnthralledUntil = 0;
        Hero.Enthraller = null;
    }

    // ----- Three Candles ---------------------------------------------------------------

    /// <summary>The three sisters, in their stands' order.</summary>
    public static readonly IReadOnlyList<string> CandleSisters = ["agnete", "brigid", "cille"];

    private static readonly Dictionary<string, string> TrueCandle = new() { ["agnete"] = "blue", ["brigid"] = "green", ["cille"] = "gold" };
    private static readonly Dictionary<string, string> FirstCandle = new() { ["agnete"] = "gold", ["brigid"] = "blue", ["cille"] = "green" };

    /// <summary>A candle stand's flag is this plus the sister's name; its value is the colour burning there.</summary>
    public const string CandleFlag = "act3.candle.";

    /// <summary>The stand whose candle the hero has lifted (a click on another swaps them), or null.</summary>
    public string? HeldCandle { get; private set; }

    /// <summary>The colour burning on a sister's stand ("blue", "green" or "gold").</summary>
    public string CandleOn(string sister) => Flag(CandleFlag + sister) ?? FirstCandle[sister];

    /// <summary>A candle stand's flame for the renderer and its label ("white" once set right), or null for anything else or a cold one.</summary>
    public string? CandleColour(Interactive who) =>
        who.Thing?.Action != "candle" ? null
        : Flag("act3.candles") switch { "rekindled" => "white", "snuffed" => null, _ => CandleOn(StandOf(who)) };

    private static string StandOf(Interactive who) => who.Id["candle_".Length..];

    private static string SisterName(string sister) => char.ToUpperInvariant(sister[0]) + sister[1..];

    /// <summary>
    /// A candle stand used: lift its candle, or with one in hand, swap the two (the same stand puts it back). When every
    /// sister's own candle is on her stand they burn white.
    /// </summary>
    private void TouchCandle(Interactive who)
    {
        var sister = StandOf(who);
        switch (Flag("act3.candles"))
        {
            case "rekindled":
                Say("The three candles burn white and still.");
                return;
            case "snuffed":
                Say("The candles are cold, and nothing will light them again.");
                return;
        }
        if (HeldCandle is null)
        {
            HeldCandle = sister;
            Float(Hero, $"{CandleOn(sister)} candle", FloatingTextKind.Info, 0.6);
            Say($"On {SisterName(sister)}'s stand burns a {CandleOn(sister)} candle. You lift it; the flame does not waver.");
            return;
        }
        var from = HeldCandle;
        HeldCandle = null;
        if (from == sister)
        {
            Say("You set the candle back on its stand.");
            return;
        }
        var (a, b) = (CandleOn(from), CandleOn(sister));
        SetFlag(CandleFlag + from, b);
        SetFlag(CandleFlag + sister, a);
        Sound(SoundCue.Ward, Hero, "monster");
        Say($"The {a} candle now burns on {SisterName(sister)}'s stand, the {b} on {SisterName(from)}'s.");
        if (CandleSisters.All(s => CandleOn(s) == TrueCandle[s]))
            Rekindle();
    }

    private void Rekindle()
    {
        SetFlag("act3.candles", "rekindled");
        GainXp(CandlesXp, Hero);
        WriteJournal(new JournalEntryDefinition("act3_candles", "Three Candles",
            "In the Bell Tower's chapel I set each sister's candle on her own stand: Agnete's blue, Brigid's green, Cille's gold. The three flames turned " +
            "white, and far above, for a moment, the singing stopped. Whatever Malachar meant to call back of the Choir, there is nothing left in those " +
            "candles to call."));
        Say("The three flames turn white, and far above, for a moment, the singing stops.");
    }
}
