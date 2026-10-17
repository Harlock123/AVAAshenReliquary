using System.Text;

namespace AshenReliquary.Core.Items;

/// <summary>
/// One item: a base, a rarity, an item level, fundamental runes and rolled affixes. Weapons take potency (+1 to +3
/// to attack) and striking (extra damage dice); body armour takes potency (AC) and resilient (saves), each gated by
/// item level as in the spec's rune table.
/// </summary>
public sealed class Item
{
    private static int _nextId;

    public Item(ItemDefinition definition, Rarity rarity, int itemLevel, IReadOnlyList<AffixRoll>? affixes = null,
        int potency = 0, int striking = 0, int resilient = 0, string? name = null)
    {
        Definition = definition;
        Rarity = rarity;
        ItemLevel = itemLevel;
        Affixes = affixes ?? [];
        Potency = potency;
        Striking = striking;
        Resilient = resilient;
        Name = name ?? DefaultName();
    }

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public ItemDefinition Definition { get; }
    public Rarity Rarity { get; }
    public int ItemLevel { get; }
    public IReadOnlyList<AffixRoll> Affixes { get; }
    public int Potency { get; }
    public int Striking { get; }
    public int Resilient { get; }
    public string Name { get; }

    public Combat.DamageType? DamageType => Definition.IsWeapon ? Definition.DamageType : null;

    public Weapon ToWeapon() => new(Definition.AsWeapon(), Potency, Striking);

    public Armor ToArmor() => new(Definition.AsArmor(), Potency, Resilient);

    private string DefaultName()
    {
        var name = new StringBuilder();
        if (Potency > 0)
            name.Append($"+{Potency} ");
        if (Striking > 0)
            name.Append(Striking switch { 1 => "Striking ", 2 => "Greater Striking ", _ => "Major Striking " });
        if (Rarity == Rarity.Uncommon && Affixes.FirstOrDefault(a => a.Definition.Prefix) is { } prefix)
            name.Append(prefix.Definition.Text).Append(' ');
        name.Append(Definition.Name);
        if (Rarity == Rarity.Uncommon && Affixes.FirstOrDefault(a => !a.Definition.Prefix) is { } suffix)
            name.Append(' ').Append(suffix.Definition.Text);
        return name.ToString();
    }

    /// <summary>The tooltip lines after the name: base, stats, runes, affixes.</summary>
    public IEnumerable<string> Describe()
    {
        var d = Definition;
        yield return Rarity == Rarity.Rare ? $"{d.Name} · rare · item level {ItemLevel}" : $"{Rarity} {d.Category.ToString().ToLowerInvariant()} · item level {ItemLevel}";
        if (d.IsWeapon)
        {
            var dice = $"{1 + Striking}d{d.DamageDie}";
            yield return $"{dice} {d.DamageType.ToString().ToLowerInvariant()} · {d.AttacksPerSecond:0.0#} attacks/s";
            if (d.Traits is { Count: > 0 } traits)
                yield return string.Join(", ", traits);
            if (Potency > 0)
                yield return $"+{Potency} item bonus to attack (potency rune)";
            if (Striking > 0)
                yield return $"{1 + Striking} damage dice (striking rune)";
        }
        else if (d.IsBodyArmor)
        {
            yield return $"{d.ArmorCategory} armour · AC +{d.AcBonus + Potency} · DEX cap {(d.DexCap >= 99 ? "none" : $"+{d.DexCap}")}";
            if (d.CheckPenalty != 0 || d.SpeedPenalty != 0)
                yield return $"Check penalty {d.CheckPenalty} · speed -{d.SpeedPenalty} ft · STR {d.Strength}";
            if (Resilient > 0)
                yield return $"+{Resilient} item bonus to saves (resilient rune)";
        }
        if (d.Implicit is { } imp)
        {
            if (imp.MaxHitPoints != 0) yield return $"+{imp.MaxHitPoints} maximum HP";
            if (imp.CastSpeed != 0) yield return $"+{imp.CastSpeed * 100:0}% cast speed";
            if (imp.ResourceRegen != 0) yield return $"+{imp.ResourceRegen * 100:0}% resource regeneration";
            if (imp.MoveSpeed != 0) yield return $"+{imp.MoveSpeed * 100:0}% movement speed";
        }
        foreach (var affix in Affixes)
            yield return affix.Describe();
    }
}
