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
    /// Deterministic identity hash. Computed once at creation, never recomputed.
    /// </summary>
    public required RunId Id { get; init; }

    /// <summary>
    /// Canonical factory. Computes RunId via FNV-1a 64-bit over canonical byte representation.
    /// </summary>
    public static RunDescriptor Create(
        ModeId mode,
        uint seed,
        DifficultyTier difficulty = DifficultyTier.Standard,
        int generatorVersion = 1,
        int rulesetVersion = 1)
    {
        var id = ComputeId(mode, seed, difficulty, generatorVersion, rulesetVersion);
        return new RunDescriptor
        {
            Mode = mode,
            Seed = seed,
            Difficulty = difficulty,
            GeneratorVersion = generatorVersion,
            RulesetVersion = rulesetVersion,
            Id = id,
        };
    }

    /// <summary>
    /// FNV-1a 64-bit over canonical byte representation.
    /// Platform-stable: explicit byte layout, all integers little-endian.
    /// Mode string encoded as UTF-8 bytes (ASCII-only in practice).
    /// </summary>
    private static RunId ComputeId(
        ModeId mode, uint seed, DifficultyTier difficulty,
        int generatorVersion, int rulesetVersion)
    {
        const ulong FnvOffset = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        ulong hash = FnvOffset;

        // Mode string as UTF-8 bytes (ASCII path: one byte per char)
        foreach (char c in mode.Value)
        {
            hash ^= (byte)c;
            hash *= FnvPrime;
            if (c > 0x7F)
            {
                hash ^= (byte)(c >> 8);
                hash *= FnvPrime;
            }
        }

        // Seed: 4 bytes little-endian
        hash = FnvByte(hash, (byte)(seed));
        hash = FnvByte(hash, (byte)(seed >> 8));
        hash = FnvByte(hash, (byte)(seed >> 16));
        hash = FnvByte(hash, (byte)(seed >> 24));

        // Difficulty: 4 bytes little-endian
        int diff = (int)difficulty;
        hash = FnvByte(hash, (byte)(diff));
        hash = FnvByte(hash, (byte)(diff >> 8));
        hash = FnvByte(hash, (byte)(diff >> 16));
        hash = FnvByte(hash, (byte)(diff >> 24));

        // GeneratorVersion: 4 bytes little-endian
        hash = FnvByte(hash, (byte)(generatorVersion));
        hash = FnvByte(hash, (byte)(generatorVersion >> 8));
        hash = FnvByte(hash, (byte)(generatorVersion >> 16));
        hash = FnvByte(hash, (byte)(generatorVersion >> 24));

        // RulesetVersion: 4 bytes little-endian
        hash = FnvByte(hash, (byte)(rulesetVersion));
        hash = FnvByte(hash, (byte)(rulesetVersion >> 8));
        hash = FnvByte(hash, (byte)(rulesetVersion >> 16));
        hash = FnvByte(hash, (byte)(rulesetVersion >> 24));

        return new RunId(hash);
    }

    private static ulong FnvByte(ulong hash, byte b)
    {
        hash ^= b;
        hash *= 1099511628211UL;
        return hash;
    }
}
