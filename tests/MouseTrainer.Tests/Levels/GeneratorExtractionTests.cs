using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;
using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Simulation.Modes.ReflexGates;
using Xunit;

namespace MouseTrainer.Tests.Levels;

/// <summary>
/// Proves the extracted ReflexGateGenerator produces bitwise-identical output
/// to the original ReflexGateSimulation.GenerateGates().
/// This is the zero-behavior-change proof for the extraction.
/// </summary>
public class GeneratorExtractionTests
{
    private const float Dt = 1f / 60f;

    private static readonly PointerInput CenterInput =
        new(960f, 540f, false, false, 0L);

    // ─────────────────────────────────────────────────────
    //  1. Generator produces identical gates to simulation
    // ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0xC0FFEEu)]
    [InlineData(0xDEADBEEFu)]
    [InlineData(1u)]
    [InlineData(42u)]
    [InlineData(0xFFFFFFFFu)]
    public void ReflexGateGenerator_ProducesIdenticalGates_ToSimulation(uint seed)
    {
        var cfg = new ReflexGateConfig();
        var generator = new ReflexGateGenerator(cfg);
        var sim = new ReflexGateSimulation(cfg);

        var run = RunDescriptor.Create(ModeId.ReflexGates, seed);
        var blueprint = generator.Generate(run);

        sim.Reset(seed);

        Assert.Equal(sim.Gates.Count, blueprint.Gates.Count);

        for (int i = 0; i < sim.Gates.Count; i++)
        {
            var sg = sim.Gates[i];
            var bg = blueprint.Gates[i];

            Assert.Equal(sg.WallX, bg.WallX);
            Assert.Equal(sg.RestCenterY, bg.RestCenterY);
            Assert.Equal(sg.ApertureHeight, bg.ApertureHeight);
            Assert.Equal(sg.Amplitude, bg.Amplitude);
            Assert.Equal(sg.Phase, bg.Phase);
            Assert.Equal(sg.FreqHz, bg.FreqHz);
        }
    }

    // ─────────────────────────────────────────────────────
    //  2. Custom config equivalence
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ReflexGateGenerator_CustomConfig_IdenticalToSimulation()
    {
        var cfg = new ReflexGateConfig
        {
            GateCount = 5,
            BaseApertureHeight = 300f,
            MinApertureHeight = 150f,
            ScrollSpeed = 100f,
        };

        var generator = new ReflexGateGenerator(cfg);
        var sim = new ReflexGateSimulation(cfg);

        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var blueprint = generator.Generate(run);

        sim.Reset(0xC0FFEEu);

        Assert.Equal(5, blueprint.Gates.Count);
        Assert.Equal(sim.Gates.Count, blueprint.Gates.Count);

        for (int i = 0; i < sim.Gates.Count; i++)
        {
            Assert.Equal(sim.Gates[i].WallX, blueprint.Gates[i].WallX);
            Assert.Equal(sim.Gates[i].RestCenterY, blueprint.Gates[i].RestCenterY);
            Assert.Equal(sim.Gates[i].ApertureHeight, blueprint.Gates[i].ApertureHeight);
            Assert.Equal(sim.Gates[i].Amplitude, blueprint.Gates[i].Amplitude);
            Assert.Equal(sim.Gates[i].Phase, blueprint.Gates[i].Phase);
            Assert.Equal(sim.Gates[i].FreqHz, blueprint.Gates[i].FreqHz);
        }
    }

    // ─────────────────────────────────────────────────────
    //  3. Same descriptor → same blueprint
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ReflexGateGenerator_SameDescriptor_SameBlueprint()
    {
        var generator = new ReflexGateGenerator();
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);

        var a = generator.Generate(run);
        var b = generator.Generate(run);

        Assert.Equal(a.Gates.Count, b.Gates.Count);

        for (int i = 0; i < a.Gates.Count; i++)
        {
            Assert.Equal(a.Gates[i].WallX, b.Gates[i].WallX);
            Assert.Equal(a.Gates[i].RestCenterY, b.Gates[i].RestCenterY);
            Assert.Equal(a.Gates[i].ApertureHeight, b.Gates[i].ApertureHeight);
            Assert.Equal(a.Gates[i].Amplitude, b.Gates[i].Amplitude);
            Assert.Equal(a.Gates[i].Phase, b.Gates[i].Phase);
            Assert.Equal(a.Gates[i].FreqHz, b.Gates[i].FreqHz);
        }
    }

    // ─────────────────────────────────────────────────────
    //  4. Blueprint contains correct metadata
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Blueprint_ContainsCorrectMetadata()
    {
        var cfg = new ReflexGateConfig();
        var generator = new ReflexGateGenerator(cfg);
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);

        var blueprint = generator.Generate(run);

        Assert.Equal(cfg.PlayfieldWidth, blueprint.PlayfieldWidth);
        Assert.Equal(cfg.PlayfieldHeight, blueprint.PlayfieldHeight);
        Assert.Equal(cfg.ScrollSpeed, blueprint.ScrollSpeed);
        Assert.Equal(cfg.GateCount, blueprint.GateCount);
        Assert.Equal(12, blueprint.GateCount);
    }

    // ─────────────────────────────────────────────────────
    //  5. CRITICAL: Blueprint path produces identical events
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Simulation_ResetFromBlueprint_ProducesIdenticalEvents()
    {
        var cfg = new ReflexGateConfig();
        var generator = new ReflexGateGenerator(cfg);
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        var blueprint = generator.Generate(run);

        // Path A: Reset via seed (original path)
        var simA = new ReflexGateSimulation(cfg);
        simA.Reset(0xC0FFEEu);
        var eventsA = RunSimToCompletion(simA);

        // Path B: Reset via blueprint (new path)
        var simB = new ReflexGateSimulation(cfg);
        simB.Reset(blueprint);
        var eventsB = RunSimToCompletion(simB);

        Assert.Equal(eventsA.Count, eventsB.Count);

        for (int i = 0; i < eventsA.Count; i++)
        {
            Assert.Equal(eventsA[i].Type, eventsB[i].Type);
            Assert.Equal(eventsA[i].Intensity, eventsB[i].Intensity);
            Assert.Equal(eventsA[i].Arg0, eventsB[i].Arg0);
            Assert.Equal(eventsA[i].Arg1, eventsB[i].Arg1);
        }
    }

    [Theory]
    [InlineData(0xC0FFEEu)]
    [InlineData(0xDEADBEEFu)]
    [InlineData(42u)]
    public void Simulation_BlueprintPath_ProducesIdenticalEvents_MultiSeed(uint seed)
    {
        var cfg = new ReflexGateConfig();
        var generator = new ReflexGateGenerator(cfg);
        var run = RunDescriptor.Create(ModeId.ReflexGates, seed);
        var blueprint = generator.Generate(run);

        var simA = new ReflexGateSimulation(cfg);
        simA.Reset(seed);
        var eventsA = RunSimToCompletion(simA);

        var simB = new ReflexGateSimulation(cfg);
        simB.Reset(blueprint);
        var eventsB = RunSimToCompletion(simB);

        Assert.Equal(eventsA.Count, eventsB.Count);

        for (int i = 0; i < eventsA.Count; i++)
        {
            Assert.Equal(eventsA[i].Type, eventsB[i].Type);
            Assert.Equal(eventsA[i].Intensity, eventsB[i].Intensity);
            Assert.Equal(eventsA[i].Arg0, eventsB[i].Arg0);
            Assert.Equal(eventsA[i].Arg1, eventsB[i].Arg1);
        }
    }

    // ─────────────────────────────────────────────────────
    //  6. Registry
    // ─────────────────────────────────────────────────────

    [Fact]
    public void LevelGeneratorRegistry_RegisterAndResolve()
    {
        var registry = new LevelGeneratorRegistry();
        var generator = new ReflexGateGenerator();

        registry.Register(ModeId.ReflexGates, 1, generator);

        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu, generatorVersion: 1);
        var resolved = registry.Resolve(run);

        Assert.Same(generator, resolved);
    }

    [Fact]
    public void LevelGeneratorRegistry_ResolveUnknownMode_Throws()
    {
        var registry = new LevelGeneratorRegistry();

        var run = RunDescriptor.Create(new ModeId("Unknown"), 42u);

        Assert.Throws<InvalidOperationException>(() => registry.Resolve(run));
    }

    [Fact]
    public void LevelGeneratorRegistry_ResolveWrongVersion_Throws()
    {
        var registry = new LevelGeneratorRegistry();
        registry.Register(ModeId.ReflexGates, 1, new ReflexGateGenerator());

        var run = RunDescriptor.Create(ModeId.ReflexGates, 42u, generatorVersion: 2);

        Assert.Throws<InvalidOperationException>(() => registry.Resolve(run));
    }

    // ─────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────

    private static List<GameEvent> RunSimToCompletion(ReflexGateSimulation sim)
    {
        var allEvents = new List<GameEvent>(capacity: 256);
        var frameEvents = new List<GameEvent>(capacity: 16);

        for (int tick = 0; tick < 8000; tick++)
        {
            frameEvents.Clear();
            sim.FixedUpdate(tick, Dt, CenterInput, frameEvents);

            foreach (var ev in frameEvents)
            {
                if (ev.Type != GameEventType.Tick)
                    allEvents.Add(ev);
            }

            if (frameEvents.Any(e => e.Type == GameEventType.LevelComplete))
                break;
        }

        return allEvents;
    }
}
