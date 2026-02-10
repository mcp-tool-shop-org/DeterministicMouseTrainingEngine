namespace MouseTrainer.Domain.Runs;

/// <summary>
/// Complete immutable run identity. Protocol type: stable hashing, canonical serialization.
/// Contains no config data — identity only. The generator resolves config from Mode + Difficulty.
/// </summary>
public readonly record struct RunDescriptor
{
    public required ModeId Mode { get; init; }
    public required uint Seed { get; init; }
    public required DifficultyTier Difficulty { get; init; }
    public required int GeneratorVersion { get; init; }
    public required int RulesetVersion { get; init; }

    /// <summary>
    /// Ordered mutator specifications. Part of run identity — order matters for hashing.
    /// Empty list = no mutators applied.
    /// </summary>
    public required IReadOnlyList<MutatorSpec> Mutators { get; init; }

    /// <summary>
    /// Deterministic identity hash. Computed once at creation, never recomputed.
    /// </summary>
    public required RunId Id { get; init; }

    /// <summary>
    /// Canonical factory. Computes RunId via FNV-1a 64-bit over canonical byte representation.
    /// When mutators is null or empty, hash is identical to pre-4C1 behavior.
    /// </summary>
    public static RunDescriptor Create(
        ModeId mode,
        uint seed,
        DifficultyTier difficulty = DifficultyTier.Standard,
        int generatorVersion = 1,
        int rulesetVersion = 1,
        IReadOnlyList<MutatorSpec>? mutators = null)
    {
        var specs = mutators ?? Array.Empty<MutatorSpec>();
        var id = ComputeId(mode, seed, difficulty, generatorVersion, rulesetVersion, specs);
        return new RunDescriptor
        {
            Mode = mode,
            Seed = seed,
            Difficulty = difficulty,
            GeneratorVersion = generatorVersion,
            RulesetVersion = rulesetVersion,
            Mutators = specs,
            Id = id,
        };
    }

    /// <summary>
    /// FNV-1a 64-bit over canonical byte representation.
    /// Platform-stable: explicit byte layout, all integers little-endian.
    /// Mode/MutatorId strings encoded as UTF-8 bytes (ASCII-only in practice).
    /// Mutator data is only hashed when the list is non-empty (backward compat).
    /// </summary>
    private static RunId ComputeId(
        ModeId mode, uint seed, DifficultyTier difficulty,
        int generatorVersion, int rulesetVersion,
        IReadOnlyList<MutatorSpec> mutators)
    {
        const ulong FnvOffset = 14695981039346656037UL;

        ulong hash = FnvOffset;

        // Mode string as UTF-8 bytes (ASCII path: one byte per char)
        hash = FnvString(hash, mode.Value);

        // Seed: 4 bytes little-endian
        hash = FnvUInt32(hash, seed);

        // Difficulty: 4 bytes little-endian
        hash = FnvInt32(hash, (int)difficulty);

        // GeneratorVersion: 4 bytes little-endian
        hash = FnvInt32(hash, generatorVersion);

        // RulesetVersion: 4 bytes little-endian
        hash = FnvInt32(hash, rulesetVersion);

        // Mutators: only hashed when non-empty (preserves golden hash for empty list)
        if (mutators.Count > 0)
        {
            // Mutator count as sentinel/delimiter
            hash = FnvInt32(hash, mutators.Count);

            for (int m = 0; m < mutators.Count; m++)
            {
                var spec = mutators[m];

                // MutatorId string (same encoding as ModeId)
                hash = FnvString(hash, spec.Id.Value);

                // Version: 4 bytes LE
                hash = FnvInt32(hash, spec.Version);

                // Param count
                hash = FnvInt32(hash, spec.Params.Count);

                // Each param: key string + value float (IEEE 754 bits)
                for (int p = 0; p < spec.Params.Count; p++)
                {
                    var param = spec.Params[p];
                    hash = FnvString(hash, param.Key);

                    uint bits = BitConverter.SingleToUInt32Bits(param.Value);
                    hash = FnvUInt32(hash, bits);
                }
            }
        }

        return new RunId(hash);
    }

    private static ulong FnvByte(ulong hash, byte b)
    {
        hash ^= b;
        hash *= 1099511628211UL;
        return hash;
    }

    private static ulong FnvInt32(ulong hash, int value)
    {
        hash = FnvByte(hash, (byte)(value));
        hash = FnvByte(hash, (byte)(value >> 8));
        hash = FnvByte(hash, (byte)(value >> 16));
        hash = FnvByte(hash, (byte)(value >> 24));
        return hash;
    }

    private static ulong FnvUInt32(ulong hash, uint value)
    {
        hash = FnvByte(hash, (byte)(value));
        hash = FnvByte(hash, (byte)(value >> 8));
        hash = FnvByte(hash, (byte)(value >> 16));
        hash = FnvByte(hash, (byte)(value >> 24));
        return hash;
    }

    private static ulong FnvString(ulong hash, string value)
    {
        foreach (char c in value)
        {
            hash ^= (byte)c;
            hash *= 1099511628211UL;
            if (c > 0x7F)
            {
                hash ^= (byte)(c >> 8);
                hash *= 1099511628211UL;
            }
        }
        return hash;
    }
}
