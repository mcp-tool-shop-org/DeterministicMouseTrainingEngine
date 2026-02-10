namespace MouseTrainer.Domain.Runs;

/// <summary>
/// Immutable specification of a single mutator application.
/// Params are always sorted by Key (ordinal) for canonical hashing.
/// Private constructor enforces the sorted invariant — use Create() factory.
/// </summary>
public sealed class MutatorSpec
{
    public MutatorId Id { get; }
    public int Version { get; }
    public IReadOnlyList<MutatorParam> Params { get; }

    private MutatorSpec(MutatorId id, int version, IReadOnlyList<MutatorParam> sortedParams)
    {
        Id = id;
        Version = version;
        Params = sortedParams;
    }

    /// <summary>
    /// Canonical factory. Sorts params by key (ordinal). Throws on duplicate keys.
    /// </summary>
    public static MutatorSpec Create(
        MutatorId id,
        int version = 1,
        IReadOnlyList<MutatorParam>? parameters = null)
    {
        if (string.IsNullOrEmpty(id.Value))
            throw new ArgumentException("MutatorId must not be empty.", nameof(id));
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be >= 1.");

        if (parameters is null || parameters.Count == 0)
            return new MutatorSpec(id, version, Array.Empty<MutatorParam>());

        // Sort by key, detect duplicates
        var sorted = parameters.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
        for (int i = 1; i < sorted.Length; i++)
        {
            if (string.Equals(sorted[i].Key, sorted[i - 1].Key, StringComparison.Ordinal))
                throw new ArgumentException($"Duplicate parameter key: '{sorted[i].Key}'.");
        }

        return new MutatorSpec(id, version, sorted);
    }
}
