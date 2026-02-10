using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Applies mutators in declared order. Pure pipeline — no state between calls.
/// The pipeline is a fold: blueprint = specs.Aggregate(bp, (b, s) => resolve(s).Apply(b)).
/// </summary>
public sealed class MutatorPipeline
{
    private readonly MutatorRegistry _registry;

    public MutatorPipeline(MutatorRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Apply all mutator specs in order. Returns the final transformed blueprint.
    /// If specs is empty, returns the input blueprint unmodified.
    /// </summary>
    public LevelBlueprint Apply(LevelBlueprint blueprint, IReadOnlyList<MutatorSpec> specs)
    {
        if (specs.Count == 0) return blueprint;

        var current = blueprint;
        for (int i = 0; i < specs.Count; i++)
        {
            var mutator = _registry.Resolve(specs[i]);
            current = mutator.Apply(current);
        }
        return current;
    }
}
