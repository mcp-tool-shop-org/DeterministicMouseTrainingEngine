using MouseTrainer.Domain.Runs;
using Xunit;

namespace MouseTrainer.Tests.Runs;

/// <summary>
/// Golden-value tests for RunDescriptor identity hashing.
/// If any golden value breaks, all existing RunIds become invalid —
/// PB bucketing, replay integrity, and challenge identity all fail.
/// </summary>
public class RunDescriptorTests
{
    // ─────────────────────────────────────────────────────
    //  1. Golden value — the canonical hash contract
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DefaultReflex_ProducesStableRunId()
    {
        var run = RunDescriptor.Create(
            ModeId.ReflexGates,
            seed: 0xC0FFEEu,
            DifficultyTier.Standard,
            generatorVersion: 1,
            rulesetVersion: 1);

        // FNV-1a 64-bit golden value. Computed once, frozen forever.
        // If this breaks, the hash implementation changed — ALL RunIds are invalid.
        Assert.Equal(0xA185D3974C936996UL, run.Id.Value);
    }

    // ─────────────────────────────────────────────────────
    //  2. Same inputs → same RunId
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_SameInputs_SameRunId()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);

        Assert.Equal(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  3. Different seed → different RunId
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DifferentSeed_DifferentRunId()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xDEADBEEFu);

        Assert.NotEqual(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  4. Different mode → different RunId
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DifferentMode_DifferentRunId()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var b = RunDescriptor.Create(new ModeId("OtherMode"), 0xC0FFEEu);

        Assert.NotEqual(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  5. Different generator version → different RunId
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DifferentGeneratorVersion_DifferentRunId()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, generatorVersion: 1);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, generatorVersion: 2);

        Assert.NotEqual(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  6. Different difficulty tier → different RunId
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DifferentDifficultyTier_DifferentRunId()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, difficulty: DifficultyTier.Standard);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, difficulty: (DifficultyTier)1);

        Assert.NotEqual(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  7. Different ruleset version → different RunId
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DifferentRulesetVersion_DifferentRunId()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, rulesetVersion: 1);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, rulesetVersion: 2);

        Assert.NotEqual(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  8. RunId.ToString format
    // ─────────────────────────────────────────────────────

    [Fact]
    public void RunId_ToString_MatchesExpectedFormat()
    {
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);

        string s = run.Id.ToString();
        Assert.StartsWith("R-", s);
        Assert.Equal(18, s.Length); // "R-" + 16 hex digits
    }

    // ─────────────────────────────────────────────────────
    //  9. Record equality
    // ─────────────────────────────────────────────────────

    [Fact]
    public void RunDescriptor_RecordEquality()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RunDescriptor_RecordInequality()
    {
        var a = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var b = RunDescriptor.Create(ModeId.ReflexGates, 0xDEADBEEFu);

        Assert.NotEqual(a, b);
    }

    // ─────────────────────────────────────────────────────
    //  10. ModeId well-known values
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ModeId_ReflexGates_HasExpectedValue()
    {
        Assert.Equal("ReflexGates", ModeId.ReflexGates.Value);
        Assert.Equal("ReflexGates", ModeId.ReflexGates.ToString());
    }

    // ─────────────────────────────────────────────────────
    //  11. Create fills all fields correctly
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_FillsAllFields()
    {
        var run = RunDescriptor.Create(
            ModeId.ReflexGates,
            seed: 42u,
            difficulty: DifficultyTier.Standard,
            generatorVersion: 3,
            rulesetVersion: 2);

        Assert.Equal(ModeId.ReflexGates, run.Mode);
        Assert.Equal(42u, run.Seed);
        Assert.Equal(DifficultyTier.Standard, run.Difficulty);
        Assert.Equal(3, run.GeneratorVersion);
        Assert.Equal(2, run.RulesetVersion);
        Assert.NotEqual(default, run.Id);
    }

    // ─────────────────────────────────────────────────────
    //  12. Empty mutator list = same golden hash (backward compat)
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyMutators_SameGoldenHash()
    {
        var run = RunDescriptor.Create(
            ModeId.ReflexGates,
            seed: 0xC0FFEEu,
            DifficultyTier.Standard,
            generatorVersion: 1,
            rulesetVersion: 1,
            mutators: Array.Empty<MutatorSpec>());

        Assert.Equal(0xA185D3974C936996UL, run.Id.Value);
    }

    [Fact]
    public void Create_NullMutators_SameGoldenHash()
    {
        var run = RunDescriptor.Create(
            ModeId.ReflexGates,
            seed: 0xC0FFEEu,
            DifficultyTier.Standard,
            generatorVersion: 1,
            rulesetVersion: 1,
            mutators: null);

        Assert.Equal(0xA185D3974C936996UL, run.Id.Value);
    }

    // ─────────────────────────────────────────────────────
    //  13. Mutators change hash
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_WithMutators_DifferentHash()
    {
        var noMutators = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var withMutators = RunDescriptor.Create(
            ModeId.ReflexGates, 0xC0FFEEu,
            mutators: new[] { MutatorSpec.Create(MutatorId.NarrowMargin) });

        Assert.NotEqual(noMutators.Id, withMutators.Id);
    }

    // ─────────────────────────────────────────────────────
    //  14. Mutator order affects hash
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_DifferentMutatorOrder_DifferentHash()
    {
        var ab = RunDescriptor.Create(
            ModeId.ReflexGates, 0xC0FFEEu,
            mutators: new[]
            {
                MutatorSpec.Create(MutatorId.NarrowMargin),
                MutatorSpec.Create(MutatorId.DifficultyCurve),
            });

        var ba = RunDescriptor.Create(
            ModeId.ReflexGates, 0xC0FFEEu,
            mutators: new[]
            {
                MutatorSpec.Create(MutatorId.DifficultyCurve),
                MutatorSpec.Create(MutatorId.NarrowMargin),
            });

        Assert.NotEqual(ab.Id, ba.Id);
    }

    // ─────────────────────────────────────────────────────
    //  15. Same mutators = same hash (deterministic)
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_SameMutators_SameHash()
    {
        var a = RunDescriptor.Create(
            ModeId.ReflexGates, 0xC0FFEEu,
            mutators: new[]
            {
                MutatorSpec.Create(MutatorId.NarrowMargin,
                    parameters: new[] { new MutatorParam("factor", 0.8f) }),
            });

        var b = RunDescriptor.Create(
            ModeId.ReflexGates, 0xC0FFEEu,
            mutators: new[]
            {
                MutatorSpec.Create(MutatorId.NarrowMargin,
                    parameters: new[] { new MutatorParam("factor", 0.8f) }),
            });

        Assert.Equal(a.Id, b.Id);
    }

    // ─────────────────────────────────────────────────────
    //  16. Golden hash with known mutators (frozen)
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_KnownMutators_ProducesStableHash()
    {
        // NarrowMargin v1, no params
        var run = RunDescriptor.Create(
            ModeId.ReflexGates,
            seed: 0xC0FFEEu,
            DifficultyTier.Standard,
            generatorVersion: 1,
            rulesetVersion: 1,
            mutators: new[] { MutatorSpec.Create(MutatorId.NarrowMargin) });

        // Frozen golden value — if this breaks, ALL mutator-bearing RunIds are invalid.
        Assert.Equal(0x855A5B939E8ABC2FUL, run.Id.Value);
    }

    [Fact]
    public void Create_KnownMutatorsWithParam_ProducesStableHash()
    {
        // NarrowMargin v1, factor=0.8
        var run = RunDescriptor.Create(
            ModeId.ReflexGates,
            seed: 0xC0FFEEu,
            DifficultyTier.Standard,
            generatorVersion: 1,
            rulesetVersion: 1,
            mutators: new[]
            {
                MutatorSpec.Create(MutatorId.NarrowMargin,
                    parameters: new[] { new MutatorParam("factor", 0.8f) }),
            });

        // Frozen golden value.
        Assert.Equal(0x4A7FB94F7D50A251UL, run.Id.Value);
    }

    // ─────────────────────────────────────────────────────
    //  17. Mutators field is populated
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Create_WithMutators_PopulatesMutatorsField()
    {
        var specs = new[]
        {
            MutatorSpec.Create(MutatorId.NarrowMargin,
                parameters: new[] { new MutatorParam("factor", 0.8f) }),
        };

        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, mutators: specs);

        Assert.Single(run.Mutators);
        Assert.Equal(MutatorId.NarrowMargin, run.Mutators[0].Id);
    }

    [Fact]
    public void Create_NoMutators_EmptyList()
    {
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        Assert.Empty(run.Mutators);
    }
}
