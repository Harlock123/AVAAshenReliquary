using AshenReliquary.Core.Abilities;

namespace AshenReliquary.Core.World;

/// <summary>
/// Multiclass dedications (M3 plan, step 6): from level 2 a class feat slot can take another class's dedication, one
/// per character, which grants that class's ability. When the ability costs a resource the hero's own class doesn't
/// spend, the hero gets a small pool of it (<see cref="HeroActor.Dedication"/>), fed by the same blows, steps and
/// seconds as their own. A Necromancer's dedication brings Grave Charges and a single skeleton; a Ranger's, Hunt Prey's d8.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>
    /// The pool the dedication calls for, made when it is taken and dropped on a respec. None when the dedication's
    /// abilities cost nothing, or its class spends the same kind as the hero's.
    /// </summary>
    private void RefreshDedication()
    {
        var c = Hero.Character;
        var other = c.DedicationClass;
        var needs = other is not null && ClassResource.KindOf(other) != Hero.Resource.Kind
            && c.GrantedAbilities.Any(id => _abilities.TryGetValue(id, out var a) && a.Class == other.Id && a.Cost > 0);
        if (!needs)
        {
            Hero.Dedication = null;
            Hero.Resource.Partner = null;
            return;
        }
        if (Hero.Dedication?.Source == other)
            return;
        Hero.Dedication = new ClassResource(c, other!, dedication: true);
        Hero.Resource.Partner = Hero.Dedication;
    }

    /// <summary>The pool an ability is paid from: the dedication's for its class's abilities, else the hero's own.</summary>
    public ClassResource ResourceFor(AbilityDefinition ability) =>
        Hero.Dedication is { } pool && ability.Class == pool.Source.Id ? pool : Hero.Resource;

    /// <summary>Whether the hero gathers Grave Charges: a Necromancer, or one dedicated to it.</summary>
    private bool GathersGraveCharges => Hero.Character.Class.GraveCharges || Hero.Character.DedicationClass?.GraveCharges == true;

    /// <summary>A save's dedication pool.</summary>
    internal void RestoreDedication(double value)
    {
        RefreshDedication();
        Hero.Dedication?.Restore(value);
    }
}
