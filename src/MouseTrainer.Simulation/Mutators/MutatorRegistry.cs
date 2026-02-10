using MouseTrainer.Domain.Runs;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Maps (MutatorId, version) to IBlueprintMutator factory.
/// Factories receive MutatorSpec to access parameters at construction time.
/// Same permanent-protocol rules as LevelGeneratorRegistry: old versions stay forever.
/// </summary>
public sealed class MutatorRegistry
{
    private readonly Dictionary<(string Id, int Version), Func<MutatorSpec, IBlueprintMutator>> _factories = new();

    /// <summary>
    /// Register a mutator factory for a specific id and version.
    /// </summary>
    public void Register(MutatorId id, int version, Func<MutatorSpec, IBlueprintMutator> factory)
    {
        _factories[(id.Value, version)] = factory;
    }

    /// <summary>
    /// Resolve a spec to a configured mutator instance.
    /// Throws if no factory is registered for the id+version.
    /// </summary>
    public IBlueprintMutator Resolve(MutatorSpec spec)
    {
        var key = (spec.Id.Value, spec.Version);
        if (_factories.TryGetValue(key, out var factory))
            return factory(spec);

        throw new InvalidOperationException(
            $"No mutator registered for '{spec.Id.Value}' version {spec.Version}.");
    }
}
