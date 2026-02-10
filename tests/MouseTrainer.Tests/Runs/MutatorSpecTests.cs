using MouseTrainer.Domain.Runs;
using Xunit;

namespace MouseTrainer.Tests.Runs;

/// <summary>
/// Validation tests for MutatorParam and MutatorSpec domain types.
/// Covers parameter canonicalization, validation, and construction invariants.
/// </summary>
public class MutatorSpecTests
{
    // ─────────────────────────────────────────────────────
    //  1. MutatorParam validation
    // ─────────────────────────────────────────────────────

    [Fact]
    public void MutatorParam_NaN_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MutatorParam("key", float.NaN));
    }

    [Fact]
    public void MutatorParam_PositiveInfinity_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MutatorParam("key", float.PositiveInfinity));
    }

    [Fact]
    public void MutatorParam_NegativeInfinity_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MutatorParam("key", float.NegativeInfinity));
    }

    [Fact]
    public void MutatorParam_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MutatorParam("", 1.0f));
    }

    [Fact]
    public void MutatorParam_NullKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MutatorParam(null!, 1.0f));
    }

    [Fact]
    public void MutatorParam_ValidValues_Succeeds()
    {
        var p = new MutatorParam("factor", 0.75f);
        Assert.Equal("factor", p.Key);
        Assert.Equal(0.75f, p.Value);
    }

    [Fact]
    public void MutatorParam_Zero_Succeeds()
    {
        var p = new MutatorParam("curve", 0f);
        Assert.Equal(0f, p.Value);
    }

    [Fact]
    public void MutatorParam_Negative_Succeeds()
    {
        var p = new MutatorParam("curve", -1.5f);
        Assert.Equal(-1.5f, p.Value);
    }

    // ─────────────────────────────────────────────────────
    //  2. MutatorSpec canonicalization
    // ─────────────────────────────────────────────────────

    [Fact]
    public void MutatorSpec_SortsParamsByKey()
    {
        var spec = MutatorSpec.Create(
            MutatorId.NarrowMargin,
            parameters: new[] { new MutatorParam("z_param", 1f), new MutatorParam("a_param", 2f) });

        Assert.Equal("a_param", spec.Params[0].Key);
        Assert.Equal("z_param", spec.Params[1].Key);
    }

    [Fact]
    public void MutatorSpec_DuplicateKeys_Throws()
    {
        Assert.Throws<ArgumentException>(() => MutatorSpec.Create(
            MutatorId.NarrowMargin,
            parameters: new[] { new MutatorParam("factor", 1f), new MutatorParam("factor", 2f) }));
    }

    [Fact]
    public void MutatorSpec_NoParams_EmptyList()
    {
        var spec = MutatorSpec.Create(MutatorId.NarrowMargin);
        Assert.Empty(spec.Params);
    }

    [Fact]
    public void MutatorSpec_NullParams_EmptyList()
    {
        var spec = MutatorSpec.Create(MutatorId.NarrowMargin, parameters: null);
        Assert.Empty(spec.Params);
    }

    // ─────────────────────────────────────────────────────
    //  3. MutatorSpec validation
    // ─────────────────────────────────────────────────────

    [Fact]
    public void MutatorSpec_VersionMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MutatorSpec.Create(MutatorId.NarrowMargin, version: 0));
    }

    [Fact]
    public void MutatorSpec_NegativeVersion_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MutatorSpec.Create(MutatorId.NarrowMargin, version: -1));
    }

    [Fact]
    public void MutatorSpec_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MutatorSpec.Create(new MutatorId(""), version: 1));
    }

    // ─────────────────────────────────────────────────────
    //  4. MutatorSpec field population
    // ─────────────────────────────────────────────────────

    [Fact]
    public void MutatorSpec_Fields_PopulatedCorrectly()
    {
        var spec = MutatorSpec.Create(
            MutatorId.DifficultyCurve,
            version: 2,
            parameters: new[] { new MutatorParam("curve", 1.5f) });

        Assert.Equal(MutatorId.DifficultyCurve, spec.Id);
        Assert.Equal(2, spec.Version);
        Assert.Single(spec.Params);
        Assert.Equal("curve", spec.Params[0].Key);
        Assert.Equal(1.5f, spec.Params[0].Value);
    }

    // ─────────────────────────────────────────────────────
    //  5. MutatorId well-known values
    // ─────────────────────────────────────────────────────

    [Fact]
    public void MutatorId_WellKnownValues()
    {
        Assert.Equal("NarrowMargin", MutatorId.NarrowMargin.Value);
        Assert.Equal("WideMargin", MutatorId.WideMargin.Value);
        Assert.Equal("DifficultyCurve", MutatorId.DifficultyCurve.Value);
    }

    [Fact]
    public void MutatorId_ToString_ReturnsValue()
    {
        Assert.Equal("NarrowMargin", MutatorId.NarrowMargin.ToString());
    }
}
