using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class PersistentDamageTests
{
    [Fact]
    public void Ticks_once_a_second_then_ends_on_a_dc_15_flat_check()
    {
        var bleed = new PersistentDamageSet();
        bleed.Add(DamageType.Bleed, new Dice(1, 6));

        Assert.Empty(bleed.Update(0.5, new ScriptedRandom()));
        var tick = Assert.Single(bleed.Update(0.5, new ScriptedRandom(4, 14))); // 4 damage, flat 14: keeps going
        Assert.Equal((DamageType.Bleed, 4), tick);
        Assert.Contains(DamageType.Bleed, bleed.Types);

        Assert.Single(bleed.Update(1.0, new ScriptedRandom(2, 15))); // flat 15: ends
        Assert.Empty(bleed.Types);
    }

    [Fact]
    public void A_weaker_effect_of_the_same_type_does_not_replace_a_stronger_one()
    {
        var burning = new PersistentDamageSet();
        burning.Add(DamageType.Fire, new Dice(2, 6));
        burning.Add(DamageType.Fire, new Dice(1, 4));
        var tick = Assert.Single(burning.Update(1.0, new ScriptedRandom(3, 3, 1)));
        Assert.Equal(6, tick.Amount);
    }

    [Fact]
    public void Flat_check_end_chance_is_thirty_percent() =>
        Assert.Equal(0.30, Enumerable.Range(1, 20).Count(n => n >= PersistentDamageSet.EndFlatCheckDc) / 20.0, 3);
}
