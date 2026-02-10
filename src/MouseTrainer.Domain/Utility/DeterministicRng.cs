namespace MouseTrainer.Domain.Utility;

/// <summary>
/// Small deterministic RNG (xorshift32). Good for repeatable variation (e.g., audio).
/// Not cryptographically secure.
/// </summary>
public struct DeterministicRng
{
    private uint _state;

    public DeterministicRng(uint seed)
    {
        _state = seed == 0 ? 0xA341316Cu : seed;
    }

    public uint NextU32()
    {
        // xorshift32
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        var range = (uint)(maxExclusive - minInclusive);
        return (int)(NextU32() % range) + minInclusive;
    }

    public float NextFloat01()
    {
        return (NextU32() & 0x00FFFFFF) / 16777216f;
    }

    /// <summary>
    /// Deterministic seed mixer for stable variation.
    /// </summary>
    public static uint Mix(uint a, uint b, uint c)
    {
        // simple mixing
        uint x = a ^ (b + 0x9e3779b9u) ^ (c << 6) ^ (c >> 2);
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
}
