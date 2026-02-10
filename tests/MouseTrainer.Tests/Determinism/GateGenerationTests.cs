using MouseTrainer.Simulation.Modes.ReflexGates;
using Xunit;

namespace MouseTrainer.Tests.Determinism;

/// <summary>
/// Verify gate generation is fully deterministic from seed + config.
/// If gate positions change, all replay assertions downstream will break.
/// </summary>
public class GateGenerationTests
{
    // ─────────────────────────────────────────────────────
    //  1. Seed → gates determinism
    // ─────────────────────────────────────────────────────

    [Fact]
    public void SameSeed_SameConfig_ProducesIdenticalGates()
    {
        var cfg = new ReflexGateConfig();

        var sim1 = new ReflexGateSimulation(cfg);
        sim1.Reset(0xC0FFEEu);

        var sim2 = new ReflexGateSimulation(cfg);
        sim2.Reset(0xC0FFEEu);

        Assert.Equal(sim1.Gates.Count, sim2.Gates.Count);

        for (int i = 0; i < sim1.Gates.Count; i++)
        {
            var g1 = sim1.Gates[i];
            var g2 = sim2.Gates[i];

            Assert.Equal(g1.WallX, g2.WallX);
            Assert.Equal(g1.RestCenterY, g2.RestCenterY);
            Assert.Equal(g1.ApertureHeight, g2.ApertureHeight);
            Assert.Equal(g1.Amplitude, g2.Amplitude);
            Assert.Equal(g1.Phase, g2.Phase);
            Assert.Equal(g1.FreqHz, g2.FreqHz);
        }
    }

    // ─────────────────────────────────────────────────────
    //  2. Gate count matches config
    // ─────────────────────────────────────────────────────

    [Fact]
    public void DefaultConfig_Produces12Gates()
    {
        var sim = new ReflexGateSimulation(new ReflexGateConfig());
        sim.Reset(0xC0FFEEu);

        Assert.Equal(12, sim.Gates.Count);
    }

    [Fact]
    public void CustomGateCount_Respected()
    {
        var cfg = new ReflexGateConfig { GateCount = 5 };
        var sim = new ReflexGateSimulation(cfg);
        sim.Reset(42u);

        Assert.Equal(5, sim.Gates.Count);
    }

    // ─────────────────────────────────────────────────────
    //  3. Gates are strictly ordered by WallX
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Gates_StrictlyAscending_WallX()
    {
        var sim = new ReflexGateSimulation(new ReflexGateConfig());
        sim.Reset(0xC0FFEEu);

        for (int i = 1; i < sim.Gates.Count; i++)
        {
            Assert.True(sim.Gates[i].WallX > sim.Gates[i - 1].WallX,
                $"Gate {i} WallX ({sim.Gates[i].WallX}) should be > Gate {i - 1} WallX ({sim.Gates[i - 1].WallX})");
        }
    }

    [Fact]
    public void Gates_WallX_MatchesFirstGatePlusSpacing()
    {
        var cfg = new ReflexGateConfig();
        var sim = new ReflexGateSimulation(cfg);
        sim.Reset(0xC0FFEEu);

        for (int i = 0; i < sim.Gates.Count; i++)
        {
            float expected = cfg.FirstGateX + i * cfg.GateSpacingX;
            Assert.Equal(expected, sim.Gates[i].WallX);
        }
    }

    // ─────────────────────────────────────────────────────
    //  4. Oscillation determinism
    // ─────────────────────────────────────────────────────

    [Fact]
    public void CurrentCenterY_SameTimeInput_SameOutput()
    {
        var sim = new ReflexGateSimulation(new ReflexGateConfig());
        sim.Reset(0xC0FFEEu);

        // Test first and last gate at multiple time points
        float[] times = { 0f, 1.0f, 5.0f, 10.0f, 50.0f };

        foreach (float t in times)
        {
            float y0a = sim.Gates[0].CurrentCenterY(t);
            float y0b = sim.Gates[0].CurrentCenterY(t);
            Assert.Equal(y0a, y0b);

            float y11a = sim.Gates[11].CurrentCenterY(t);
            float y11b = sim.Gates[11].CurrentCenterY(t);
            Assert.Equal(y11a, y11b);
        }
    }

    [Fact]
    public void LastGate_Oscillates_MoreThan_FirstGate()
    {
        var sim = new ReflexGateSimulation(new ReflexGateConfig());
        sim.Reset(0xC0FFEEu);

        // First gate has small amplitude, last gate has large amplitude
        Assert.True(sim.Gates[11].Amplitude > sim.Gates[0].Amplitude,
            "Last gate should oscillate with larger amplitude than first gate.");
    }

    // ─────────────────────────────────────────────────────
    //  5. Different seed → different gates
    // ─────────────────────────────────────────────────────

    [Fact]
    public void DifferentSeed_DifferentGatePositions()
    {
        var cfg = new ReflexGateConfig();

        var sim1 = new ReflexGateSimulation(cfg);
        sim1.Reset(0xC0FFEEu);

        var sim2 = new ReflexGateSimulation(cfg);
        sim2.Reset(0xDEADBEEFu);

        // WallX is deterministic from config (not seed), so compare seeded fields
        bool anyDiffer = false;
        for (int i = 0; i < sim1.Gates.Count; i++)
        {
            if (Math.Abs(sim1.Gates[i].RestCenterY - sim2.Gates[i].RestCenterY) > 0.001f ||
                Math.Abs(sim1.Gates[i].Phase - sim2.Gates[i].Phase) > 0.001f)
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.True(anyDiffer,
            "Different seeds should produce different gate positions (RestCenterY / Phase).");
    }

    // ─────────────────────────────────────────────────────
    //  6. Aperture shrinks from first to last gate
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Aperture_ShrinksWith_Difficulty()
    {
        var cfg = new ReflexGateConfig();
        var sim = new ReflexGateSimulation(cfg);
        sim.Reset(0xC0FFEEu);

        Assert.Equal(cfg.BaseApertureHeight, sim.Gates[0].ApertureHeight);

        // Last gate should have minimum aperture
        Assert.Equal(cfg.MinApertureHeight, sim.Gates[^1].ApertureHeight);

        // Aperture should be non-increasing
        for (int i = 1; i < sim.Gates.Count; i++)
        {
            Assert.True(sim.Gates[i].ApertureHeight <= sim.Gates[i - 1].ApertureHeight,
                $"Gate {i} aperture ({sim.Gates[i].ApertureHeight}) should be <= Gate {i - 1} ({sim.Gates[i - 1].ApertureHeight})");
        }
    }

    // ─────────────────────────────────────────────────────
    //  7. Reset produces clean state
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Reset_SameSeed_IdenticalGates()
    {
        var sim = new ReflexGateSimulation(new ReflexGateConfig());

        sim.Reset(0xC0FFEEu);
        var firstRun = sim.Gates.Select(g => (g.WallX, g.RestCenterY, g.Phase)).ToList();

        sim.Reset(0xC0FFEEu);
        var secondRun = sim.Gates.Select(g => (g.WallX, g.RestCenterY, g.Phase)).ToList();

        Assert.Equal(firstRun, secondRun);
    }
}
