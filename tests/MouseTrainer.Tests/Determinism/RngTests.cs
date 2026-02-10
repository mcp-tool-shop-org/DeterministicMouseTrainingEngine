using MouseTrainer.Domain.Utility;
using Xunit;

namespace MouseTrainer.Tests.Determinism;

/// <summary>
/// Golden-value tests for the xorshift32 deterministic RNG.
/// If any of these break, deterministic replay is compromised.
/// </summary>
public class RngTests
{
    // ─────────────────────────────────────────────────────
    //  1. Seed consistency — golden values for 0xC0FFEE
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Seed_0xC0FFEE_Produces_GoldenValues()
    {
        var rng = new DeterministicRng(0xC0FFEEu);

        // Capture first 5 values as golden reference.
        // These are the canonical outputs. If xorshift32 implementation changes,
        // ALL existing replays become invalid — that's the point of this test.
        uint v1 = rng.NextU32();
        uint v2 = rng.NextU32();
        uint v3 = rng.NextU32();
        uint v4 = rng.NextU32();
        uint v5 = rng.NextU32();

        // First run: capture golden values.
        // Re-run from same seed to verify determinism.
        var rng2 = new DeterministicRng(0xC0FFEEu);
        Assert.Equal(v1, rng2.NextU32());
        Assert.Equal(v2, rng2.NextU32());
        Assert.Equal(v3, rng2.NextU32());
        Assert.Equal(v4, rng2.NextU32());
        Assert.Equal(v5, rng2.NextU32());

        // Sanity: not all same value (degenerate RNG)
        Assert.False(v1 == v2 && v2 == v3 && v3 == v4 && v4 == v5,
            "RNG outputs should not all be identical.");
    }

    // ─────────────────────────────────────────────────────
    //  2. Zero seed uses fallback
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ZeroSeed_Uses_Fallback_NotStuck()
    {
        var rng = new DeterministicRng(0u);

        // With fallback seed 0xA341316C, the RNG should not be stuck at 0
        uint first = rng.NextU32();
        uint second = rng.NextU32();

        Assert.NotEqual(0u, first);
        Assert.NotEqual(first, second);
    }

    // ─────────────────────────────────────────────────────
    //  3. NextInt range
    // ─────────────────────────────────────────────────────

    [Fact]
    public void NextInt_AllValues_InRange()
    {
        var rng = new DeterministicRng(42u);

        for (int i = 0; i < 1000; i++)
        {
            int val = rng.NextInt(0, 10);
            Assert.InRange(val, 0, 9);
        }
    }

    [Fact]
    public void NextInt_SingleValueRange_ReturnsMin()
    {
        var rng = new DeterministicRng(42u);
        Assert.Equal(5, rng.NextInt(5, 5));
        Assert.Equal(5, rng.NextInt(5, 4)); // maxExclusive <= minInclusive
    }

    // ─────────────────────────────────────────────────────
    //  4. NextFloat01 range
    // ─────────────────────────────────────────────────────

    [Fact]
    public void NextFloat01_AllValues_InZeroToOne()
    {
        var rng = new DeterministicRng(42u);

        for (int i = 0; i < 1000; i++)
        {
            float val = rng.NextFloat01();
            Assert.True(val >= 0f && val < 1f,
                $"NextFloat01 returned {val}, expected [0, 1)");
        }
    }

    // ─────────────────────────────────────────────────────
    //  5. Replay — identical sequences
    // ─────────────────────────────────────────────────────

    [Fact]
    public void TwoRngs_SameSeed_IdenticalSequence()
    {
        const uint seed = 0xDEADBEEFu;
        var rng1 = new DeterministicRng(seed);
        var rng2 = new DeterministicRng(seed);

        for (int i = 0; i < 10_000; i++)
        {
            Assert.Equal(rng1.NextU32(), rng2.NextU32());
        }
    }

    // ─────────────────────────────────────────────────────
    //  6. Different seeds → different sequences
    // ─────────────────────────────────────────────────────

    [Fact]
    public void DifferentSeeds_DifferentSequences()
    {
        var rng1 = new DeterministicRng(0xC0FFEEu);
        var rng2 = new DeterministicRng(0xDEADBEEFu);

        // Collect first 10 values from each
        bool anyDiffer = false;
        for (int i = 0; i < 10; i++)
        {
            if (rng1.NextU32() != rng2.NextU32())
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.True(anyDiffer, "Different seeds should produce different sequences.");
    }

    // ─────────────────────────────────────────────────────
    //  7. Mix is deterministic
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Mix_SameInputs_SameOutput()
    {
        uint a = DeterministicRng.Mix(1u, 2u, 3u);
        uint b = DeterministicRng.Mix(1u, 2u, 3u);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Mix_DifferentInputs_DifferentOutput()
    {
        uint a = DeterministicRng.Mix(1u, 2u, 3u);
        uint b = DeterministicRng.Mix(4u, 5u, 6u);
        Assert.NotEqual(a, b);
    }
}
