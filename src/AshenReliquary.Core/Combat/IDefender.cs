using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Combat;

/// <summary>Anything that can be attacked: player characters and creatures alike.</summary>
public interface IDefender
{
    string Name { get; }
    ConditionSet Conditions { get; }
    DamageProfile Defenses { get; }
    ModifierTotal ArmorClass();
}
