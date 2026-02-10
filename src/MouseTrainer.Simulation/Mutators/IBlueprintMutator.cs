using MouseTrainer.Simulation.Levels;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Pure function over a LevelBlueprint. No state. No RNG. No side effects.
/// Always returns a new LevelBlueprint — the input is never modified.
/// A mutator may only read the input blueprint and its own parameters.
/// </summary>
public interface IBlueprintMutator
{
    LevelBlueprint Apply(LevelBlueprint blueprint);
}
