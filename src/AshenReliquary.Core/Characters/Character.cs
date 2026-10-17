using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Characters;

public enum DeathOutcome
{
    Alive,

    /// <summary>Hit 0 HP (dying 1), but a Hero Point was spent and the character got up at half HP.</summary>
    RevivedByHeroPoint,

    Died,
}

/// <summary>
/// A player character: ancestry, background and class plus attributes, level, gear and conditions.
/// Every derived statistic comes back as a <see cref="ModifierTotal"/>, so the UI can show the formula
/// behind each number (AC 24 = 10 + DEX 3 + Expert 9 + item 2).
/// </summary>
public sealed class Character : IDefender
{
    public const int MaxLevel = 20;
    public const double BaseDetectionTiles = 4.0;
    public const double DetectionTilesPerPerception = 0.25;

    public static readonly WeaponDefinition Fist = new("fist", "Fist", 4, DamageType.Bludgeoning, 1.6,
        Traits: new HashSet<WeaponTrait> { WeaponTrait.Agile, WeaponTrait.Finesse });

    private readonly List<(StatisticKind Statistic, Modifier Modifier)> _extraModifiers = [];

    internal Character(string name, AncestryDefinition ancestry, BackgroundDefinition background,
        ClassDefinition characterClass, AttributeKind keyAttribute, AttributeSet attributes, HeritageDefinition? heritage = null)
    {
        Name = name;
        Heritage = heritage;
        Ancestry = ancestry;
        Background = background;
        Class = characterClass;
        KeyAttribute = keyAttribute;
        Attributes = attributes;
        var taintRate = ancestry.Id == "ashborn" ? 0.75 : 1.0;
        Taint = new TaintMeter(TaintMeter.RateFor(taintRate, attributes[AttributeKind.Constitution]));
        if (heritage is { Will: > 0 })
            AddModifier(StatisticKind.Will, new Modifier(heritage.Name, heritage.Will));
        RefreshDefenses();
        HitPoints = MaxHitPoints;
    }

    public string Name { get; }
    public AncestryDefinition Ancestry { get; }
    public HeritageDefinition? Heritage { get; }
    public BackgroundDefinition Background { get; }
    public ClassDefinition Class { get; }
    public AttributeKind KeyAttribute { get; }
    public AttributeSet Attributes { get; }
    public int Level { get; private set; } = 1;

    public Weapon Weapon { get; set; } = new(Fist);
    public Armor Armor { get; set; } = Armor.None;

    /// <summary>What the equipped items add on top of the rules: affixes and implicit bonuses (see <see cref="ApplyGear"/>).</summary>
    public GearBonuses Gear { get; private set; } = GearBonuses.None;

    /// <summary>
    /// Replaces the gear bonuses: max HP, resistances and untyped save bonuses take effect at once. Hit points keep
    /// their damage taken when the maximum changes.
    /// </summary>
    public void ApplyGear(GearBonuses gear)
    {
        var missing = MaxHitPoints - HitPoints;
        Gear = gear;
        RemoveModifiers(GearSource);
        foreach (var (save, value) in gear.Saves)
            AddModifier(save, new Modifier(GearSource, value));
        RefreshDefenses();
        if (!IsDead)
            HitPoints = Math.Clamp(MaxHitPoints - missing, 1, MaxHitPoints);
    }

    public const string GearSource = "Gear";

    /// <summary>
    /// Resistances from gear, heritage and ancestry together. They add up, as untyped bonuses from different sources
    /// in this game do; the ancestry's share grows with level (Ashborn: fire resistance of half the level).
    /// </summary>
    private void RefreshDefenses()
    {
        Defenses.Resistances.Clear();
        var total = new Dictionary<DamageType, int>();
        foreach (var (type, value) in Gear.Resistances)
            total[type] = total.GetValueOrDefault(type) + value;
        foreach (var (type, value) in Heritage?.Resistances ?? new Dictionary<DamageType, int>())
            total[type] = total.GetValueOrDefault(type) + value;
        if (Ancestry.FireResistancePerTwoLevels > 0)
            total[DamageType.Fire] = total.GetValueOrDefault(DamageType.Fire) + Math.Max(1, Level / 2) * Ancestry.FireResistancePerTwoLevels;
        foreach (var (type, value) in total)
            Defenses.Resist(type, value);
    }

    public ConditionSet Conditions { get; } = new();
    public DamageProfile Defenses { get; } = new();
    public HeroPoints HeroPoints { get; } = new();
    public HealingPotion Potion { get; } = new();
    public TaintMeter Taint { get; }

    public int HitPoints { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Gear affixes, feats and buffs. They go through the normal stacking rule with everything else.</summary>
    public void AddModifier(StatisticKind statistic, Modifier modifier) => _extraModifiers.Add((statistic, modifier));

    public void RemoveModifiers(string source) => _extraModifiers.RemoveAll(m => m.Modifier.Source == source);

    // ----- Levels ----------------------------------------------------------------

    /// <summary>Gains a level. Levels 5, 10, 15 and 20 need four boosts to different attributes.</summary>
    public void LevelUp(IReadOnlyCollection<AttributeKind>? boosts = null)
    {
        if (Level >= MaxLevel)
            throw new RulesException($"Level {MaxLevel} is the cap.");
        var next = Level + 1;
        var needsBoosts = AttributeSet.BoostLevels.Contains(next);
        if (needsBoosts && boosts is null)
            throw new RulesException($"Level {next} needs {AttributeSet.BoostsPerLevelUp} attribute boosts.");
        if (!needsBoosts && boosts is not null)
            throw new RulesException($"Level {next} does not grant attribute boosts.");

        var before = MaxHitPoints;
        if (boosts is not null)
            Attributes.ApplyLevelUpBoosts(next, boosts);
        Level = next;
        RefreshDefenses();
        HitPoints += MaxHitPoints - before;
    }

    public ProficiencyRank Rank(ProficiencyTrack track) => Class.RankAt(track, Level);

    // ----- Hit points ------------------------------------------------------------

    /// <summary>Ancestry HP + (class HP + CON) x level, less drained N x level.</summary>
    public int MaxHitPoints =>
        Math.Max(1, Ancestry.HitPoints + (Class.HitPointsPerLevel + Attributes[AttributeKind.Constitution]) * Level
            - Conditions.MaxHitPointLoss(Level) + Gear.MaxHitPoints + (Heritage?.HitPointsPerLevel ?? 0) * Level);

    /// <summary>
    /// Takes damage. At 0 HP the character is dying; a held Hero Point is spent at once to revive at half HP,
    /// otherwise the character dies.
    /// </summary>
    public DeathOutcome TakeDamage(int amount)
    {
        if (IsDead || amount <= 0)
            return IsDead ? DeathOutcome.Died : DeathOutcome.Alive;
        HitPoints = Math.Max(0, HitPoints - amount);
        if (HitPoints > 0)
            return DeathOutcome.Alive;
        if (HeroPoints.TrySpend())
        {
            HitPoints = Math.Max(1, MaxHitPoints / 2);
            return DeathOutcome.RevivedByHeroPoint;
        }
        IsDead = true;
        return DeathOutcome.Died;
    }

    /// <summary>Puts hit points back to an exact value (after an equip preview), within the current maximum.</summary>
    internal void RestoreHitPoints(int hitPoints)
    {
        if (!IsDead)
            HitPoints = Math.Clamp(hitPoints, 1, MaxHitPoints);
    }

    public void Heal(int amount)
    {
        if (!IsDead && amount > 0)
            HitPoints = Math.Min(MaxHitPoints, HitPoints + amount);
    }

    /// <summary>Respawning at a shrine: full HP, potion charges refilled, Taint cleansed, drained removed.</summary>
    public void RestAtShrine()
    {
        IsDead = false;
        Conditions.Remove(ConditionKind.Drained);
        Taint.Cleanse();
        Potion.Refill();
        HitPoints = MaxHitPoints;
    }

    // ----- Derived statistics ----------------------------------------------------

    /// <summary>10 + DEX (up to the armour's cap) + armour proficiency + armour item bonus.</summary>
    public ModifierTotal ArmorClass()
    {
        var dex = Math.Min(Attributes[AttributeKind.Dexterity], Armor.Definition.DexCap);
        return Total(StatisticKind.ArmorClass, AttributeKind.Dexterity,
            new Modifier("Base", 10),
            Attr(AttributeKind.Dexterity, dex),
            Class.IsTrainedIn(Armor.Definition.Category) ? Prof(ProficiencyTrack.Armor) : new Modifier("Untrained in armour", 0),
            new Modifier(Armor.Definition.Name, Armor.ItemAcBonus, BonusType.Item));
    }

    /// <summary>The attribute a weapon attacks with: DEX for ranged, the better of STR and DEX for finesse, otherwise STR.</summary>
    public AttributeKind AttackAttribute(Weapon weapon)
    {
        if (weapon.Definition.IsRanged)
            return AttributeKind.Dexterity;
        if (weapon.Definition.Has(WeaponTrait.Finesse) && Attributes[AttributeKind.Dexterity] > Attributes[AttributeKind.Strength])
            return AttributeKind.Dexterity;
        return AttributeKind.Strength;
    }

    /// <summary>STR or DEX + weapon proficiency + potency (item) + status and circumstance modifiers.</summary>
    public ModifierTotal AttackModifier()
    {
        var attribute = AttackAttribute(Weapon);
        return Total(StatisticKind.Attack, attribute,
            Attr(attribute, Attributes[attribute]),
            Prof(ProficiencyTrack.Attack),
            new Modifier("Potency rune", Weapon.Potency, BonusType.Item));
    }

    /// <summary>The flat damage added to the weapon's dice: STR for melee and thrown weapons, nothing for other ranged weapons.</summary>
    public ModifierTotal DamageBonus()
    {
        var addsStrength = !Weapon.Definition.IsRanged || Weapon.Definition.Has(WeaponTrait.Thrown);
        return Total(StatisticKind.Damage, addsStrength ? AttributeKind.Strength : null,
            Attr(AttributeKind.Strength, addsStrength ? Attributes[AttributeKind.Strength] : 0),
            new Modifier(GearSource, Gear.WeaponDamage));
    }

    /// <summary>Fortitude (CON), Reflex (DEX) or Will (WIS) + proficiency + resilient rune (item).</summary>
    public ModifierTotal Save(StatisticKind save)
    {
        var (attribute, track) = save switch
        {
            StatisticKind.Fortitude => (AttributeKind.Constitution, ProficiencyTrack.Fortitude),
            StatisticKind.Reflex => (AttributeKind.Dexterity, ProficiencyTrack.Reflex),
            StatisticKind.Will => (AttributeKind.Wisdom, ProficiencyTrack.Will),
            _ => throw new ArgumentOutOfRangeException(nameof(save), save, "Not a save."),
        };
        var taint = Taint.SavePenalty;
        return Total(save, attribute,
            Attr(attribute, Attributes[attribute]),
            Prof(track),
            new Modifier("Resilient rune", Armor.Resilient, BonusType.Item),
            taint ?? default);
    }

    public ModifierTotal Perception() => Total(StatisticKind.Perception, AttributeKind.Wisdom,
        Attr(AttributeKind.Wisdom, Attributes[AttributeKind.Wisdom]),
        Prof(ProficiencyTrack.Perception));

    /// <summary>Traps, hidden doors and stealthy enemies are spotted within 4 tiles + 0.25 per point of Perception.</summary>
    public double DetectionRangeTiles => BaseDetectionTiles + DetectionTilesPerPerception * Perception().Total;

    /// <summary>10 + key attribute + class DC proficiency.</summary>
    public ModifierTotal ClassDc() => Total(StatisticKind.ClassDc, KeyAttribute,
        new Modifier("Base", 10),
        Attr(KeyAttribute, Attributes[KeyAttribute]),
        Prof(ProficiencyTrack.ClassDc));

    public ModifierTotal SpellAttack() => Total(StatisticKind.SpellAttack, KeyAttribute,
        Attr(KeyAttribute, Attributes[KeyAttribute]),
        Prof(ProficiencyTrack.Spell));

    /// <summary>10 + key attribute + spell proficiency.</summary>
    public ModifierTotal SpellDc() => Total(StatisticKind.SpellDc, KeyAttribute,
        new Modifier("Base", 10),
        Attr(KeyAttribute, Attributes[KeyAttribute]),
        Prof(ProficiencyTrack.Spell));

    /// <summary>Ancestry speed less the armour's speed penalty, in feet.</summary>
    public int SpeedFeet => Ancestry.SpeedFeet + (Heritage?.SpeedFeet ?? 0) - Armor.SpeedPenaltyFeet(Attributes[AttributeKind.Strength]);

    public double MoveSpeedTilesPerSecond(double moveSpeedBonus = 0) =>
        Conditions.CanMove ? ActionTiming.MoveSpeedTilesPerSecond(SpeedFeet, moveSpeedBonus + Gear.MoveSpeed) : 0;

    // ----- Helpers ---------------------------------------------------------------

    private static Modifier Attr(AttributeKind kind, int value) => new(kind.Abbreviation(), value);

    private Modifier Prof(ProficiencyTrack track)
    {
        var rank = Rank(track);
        return new Modifier(rank.ToString(), Proficiency.Bonus(rank, Level));
    }

    private ModifierTotal Total(StatisticKind statistic, AttributeKind? attribute, params Modifier[] core) =>
        ModifierStack.Combine(core
            .Concat(_extraModifiers.Where(m => m.Statistic == statistic).Select(m => m.Modifier))
            .Concat(Conditions.PenaltiesFor(statistic, attribute)));
}
