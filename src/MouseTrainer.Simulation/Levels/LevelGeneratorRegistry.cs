using MouseTrainer.Domain.Runs;

namespace MouseTrainer.Simulation.Levels;

/// <summary>
/// Maps (ModeId, GeneratorVersion) → ILevelGenerator instance.
/// Old versions are registered forever — generators are immutable contracts.
/// </summary>
public sealed class LevelGeneratorRegistry
{
    private readonly Dictionary<(string Mode, int Version), ILevelGenerator> _generators = new();

    /// <summary>
    /// Register a generator for a specific mode and version.
    /// Replaces any existing registration for the same key.
    /// </summary>
    public void Register(ModeId mode, int generatorVersion, ILevelGenerator generator)
    {
        _generators[(mode.Value, generatorVersion)] = generator;
    }

    /// <summary>
    /// Resolve the generator for the run's mode and version.
    /// Throws if no generator is registered for the combination.
    /// </summary>
    public ILevelGenerator Resolve(RunDescriptor run)
    {
        var key = (run.Mode.Value, run.GeneratorVersion);
        if (_generators.TryGetValue(key, out var gen))
            return gen;

        throw new InvalidOperationException(
            $"No generator registered for mode '{run.Mode.Value}' version {run.GeneratorVersion}.");
    }
}
