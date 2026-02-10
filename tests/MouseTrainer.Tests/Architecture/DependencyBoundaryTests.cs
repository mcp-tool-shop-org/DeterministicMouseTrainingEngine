using NetArchTest.Rules;
using Xunit;

namespace MouseTrainer.Tests.Architecture;

/// <summary>
/// Verify the modular monolith layer boundaries stay clean.
/// Domain (leaf) -> Simulation -> Audio -> MauiHost (top).
/// No illegal cross-layer references allowed.
/// </summary>
public class DependencyBoundaryTests
{
    // ── Assembly anchors ──

    private static readonly System.Reflection.Assembly DomainAsm =
        typeof(Domain.Events.GameEvent).Assembly;

    private static readonly System.Reflection.Assembly SimAsm =
        typeof(Simulation.Core.IGameSimulation).Assembly;

    private static readonly System.Reflection.Assembly AudioAsm =
        typeof(Audio.Core.AudioDirector).Assembly;

    // ─────────────────────────────────────────────────────
    //  1. Domain is the leaf — no upstream references
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Domain_DoesNot_Reference_Simulation()
    {
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.Simulation")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must NOT depend on Simulation. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Domain_DoesNot_Reference_Audio()
    {
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.Audio")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must NOT depend on Audio. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Domain_DoesNot_Reference_MauiHost()
    {
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.MauiHost")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must NOT depend on MauiHost. Offending types: " +
            FormatFailures(result));
    }

    // ─────────────────────────────────────────────────────
    //  2. Simulation only depends on Domain
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Simulation_DoesNot_Reference_Audio()
    {
        var result = Types.InAssembly(SimAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.Audio")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Simulation must NOT depend on Audio. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Simulation_DoesNot_Reference_MauiHost()
    {
        var result = Types.InAssembly(SimAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.MauiHost")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Simulation must NOT depend on MauiHost. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Simulation_DoesNot_Reference_Maui()
    {
        var result = Types.InAssembly(SimAsm)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Maui")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Simulation must NOT depend on MAUI platform. Offending types: " +
            FormatFailures(result));
    }

    // ─────────────────────────────────────────────────────
    //  3. Audio only depends on Domain
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Audio_DoesNot_Reference_Simulation()
    {
        var result = Types.InAssembly(AudioAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.Simulation")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Audio must NOT depend on Simulation. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Audio_DoesNot_Reference_MauiHost()
    {
        var result = Types.InAssembly(AudioAsm)
            .ShouldNot()
            .HaveDependencyOn("MouseTrainer.MauiHost")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Audio must NOT depend on MauiHost. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Audio_DoesNot_Reference_Maui()
    {
        var result = Types.InAssembly(AudioAsm)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Maui")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Audio must NOT depend on MAUI platform. Offending types: " +
            FormatFailures(result));
    }

    // ─────────────────────────────────────────────────────
    //  4. Domain has no I/O or platform types
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Domain_HasNo_FileIO()
    {
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOn("System.IO.File")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must NOT use System.IO.File. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Domain_HasNo_Networking()
    {
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOn("System.Net")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain must NOT use System.Net. Offending types: " +
            FormatFailures(result));
    }

    // ─────────────────────────────────────────────────────
    //  5. Simulation has no I/O
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Simulation_HasNo_FileIO()
    {
        var result = Types.InAssembly(SimAsm)
            .ShouldNot()
            .HaveDependencyOn("System.IO.File")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Simulation must NOT use System.IO.File. Offending types: " +
            FormatFailures(result));
    }

    [Fact]
    public void Simulation_HasNo_Networking()
    {
        var result = Types.InAssembly(SimAsm)
            .ShouldNot()
            .HaveDependencyOn("System.Net")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Simulation must NOT use System.Net. Offending types: " +
            FormatFailures(result));
    }

    // ─────────────────────────────────────────────────────
    //  Helper
    // ─────────────────────────────────────────────────────

    private static string FormatFailures(TestResult result)
    {
        if (result.FailingTypes is null || !result.FailingTypes.Any())
            return "(none)";
        return string.Join(", ", result.FailingTypes.Select(t => t.FullName));
    }
}
