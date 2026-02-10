using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Input;
using MouseTrainer.Simulation.Modes.ReflexGates;
using Xunit;

namespace MouseTrainer.Tests.Determinism;

/// <summary>
/// Full session replay regression.
/// Proves: same seed + same input sequence = identical event sequence, bit-for-bit.
/// This is the critical determinism guarantee for the entire game.
/// </summary>
public class ReplayRegressionTests
{
    private const uint TestSeed = 0xC0FFEEu;
    private const int FixedHz = 60;
    private const float Dt = 1f / FixedHz;

    // Cursor parked at playfield center — some gates will pass, some will miss
    private static readonly PointerInput CenterInput =
        new(960f, 540f, false, false, 0L);

    // ─────────────────────────────────────────────────────
    //  1. Full session replay — two identical runs produce
    //     event-by-event identical output
    // ─────────────────────────────────────────────────────

    [Fact]
    public void FullSession_SameSeed_SameInput_IdenticalEvents()
    {
        var events1 = RunFullSession(TestSeed);
        var events2 = RunFullSession(TestSeed);

        Assert.Equal(events1.Count, events2.Count);

        for (int i = 0; i < events1.Count; i++)
        {
            Assert.Equal(events1[i].Type, events2[i].Type);
            Assert.Equal(events1[i].Intensity, events2[i].Intensity);
            Assert.Equal(events1[i].Arg0, events2[i].Arg0);
            Assert.Equal(events1[i].Arg1, events2[i].Arg1);
        }
    }

    // ─────────────────────────────────────────────────────
    //  2. Anchor assertions — deterministic outcome values
    // ─────────────────────────────────────────────────────

    [Fact]
    public void FullSession_Produces_LevelComplete()
    {
        var events = RunFullSession(TestSeed);

        // Must have at least one LevelComplete event
        var levelComplete = events.Where(e => e.Type == GameEventType.LevelComplete).ToList();
        Assert.Single(levelComplete);

        // Score from LevelComplete.Arg0 must match accumulated EnteredGate scores
        int expectedScore = events
            .Where(e => e.Type == GameEventType.EnteredGate)
            .Sum(e => e.Arg1);

        Assert.Equal(expectedScore, levelComplete[0].Arg0);
    }

    [Fact]
    public void FullSession_GateEvents_Match_GateCount()
    {
        var events = RunFullSession(TestSeed);

        int enteredCount = events.Count(e => e.Type == GameEventType.EnteredGate);
        int hitWallCount = events.Count(e => e.Type == GameEventType.HitWall);

        // Every gate produces exactly one event: either EnteredGate or HitWall
        Assert.Equal(12, enteredCount + hitWallCount);
    }

    [Fact]
    public void FullSession_GateIndices_Sequential()
    {
        var events = RunFullSession(TestSeed);

        // Gate events should fire in index order 0..11
        var gateEvents = events
            .Where(e => e.Type == GameEventType.EnteredGate || e.Type == GameEventType.HitWall)
            .ToList();

        Assert.Equal(12, gateEvents.Count);

        for (int i = 0; i < gateEvents.Count; i++)
        {
            Assert.Equal(i, gateEvents[i].Arg0);
        }
    }

    // ─────────────────────────────────────────────────────
    //  3. Reset produces clean replay
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Reset_ThenReplay_IdenticalEvents()
    {
        var cfg = new ReflexGateConfig();
        var sim = new ReflexGateSimulation(cfg);

        // Run 1
        sim.Reset(TestSeed);
        var events1 = RunSimToCompletion(sim);

        // Reset same seed
        sim.Reset(TestSeed);
        var events2 = RunSimToCompletion(sim);

        Assert.Equal(events1.Count, events2.Count);

        for (int i = 0; i < events1.Count; i++)
        {
            Assert.Equal(events1[i].Type, events2[i].Type);
            Assert.Equal(events1[i].Intensity, events2[i].Intensity);
            Assert.Equal(events1[i].Arg0, events2[i].Arg0);
            Assert.Equal(events1[i].Arg1, events2[i].Arg1);
        }
    }

    // ─────────────────────────────────────────────────────
    //  4. Different seed → different results
    // ─────────────────────────────────────────────────────

    [Fact]
    public void DifferentSeed_DifferentEvents()
    {
        var events1 = RunFullSession(0xC0FFEEu);
        var events2 = RunFullSession(0xDEADBEEFu);

        // Both complete with 12 gate events, but outcomes differ
        var gates1 = events1
            .Where(e => e.Type == GameEventType.EnteredGate || e.Type == GameEventType.HitWall)
            .ToList();
        var gates2 = events2
            .Where(e => e.Type == GameEventType.EnteredGate || e.Type == GameEventType.HitWall)
            .ToList();

        Assert.Equal(12, gates1.Count);
        Assert.Equal(12, gates2.Count);

        // At least one gate should have a different outcome (pass vs miss)
        // or different score/intensity
        bool anyDiffer = false;
        for (int i = 0; i < gates1.Count; i++)
        {
            if (gates1[i].Type != gates2[i].Type ||
                gates1[i].Arg1 != gates2[i].Arg1 ||
                Math.Abs(gates1[i].Intensity - gates2[i].Intensity) > 0.001f)
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.True(anyDiffer, "Different seeds should produce different gate outcomes.");
    }

    // ─────────────────────────────────────────────────────
    //  5. Simulation completes within expected tick range
    // ─────────────────────────────────────────────────────

    [Fact]
    public void FullSession_CompletesWithin_ReasonableTickCount()
    {
        var cfg = new ReflexGateConfig();
        var sim = new ReflexGateSimulation(cfg);
        sim.Reset(TestSeed);

        // Last gate at FirstGateX + 11 * GateSpacingX = 400 + 11*450 = 5350
        // At ScrollSpeed=70, that's ~76.4 seconds = ~4586 ticks at 60Hz
        // Add some margin for the first gate at 400px: 400/70 = ~5.7s = ~343 ticks
        // So completion should be around tick 4500-5500
        int tickCount = 0;
        var events = new List<GameEvent>(capacity: 256);
        bool complete = false;

        while (tickCount < 8000 && !complete)
        {
            events.Clear();
            sim.FixedUpdate(tickCount, Dt, CenterInput, events);

            if (events.Any(e => e.Type == GameEventType.LevelComplete))
                complete = true;

            tickCount++;
        }

        Assert.True(complete, $"Session should complete within 8000 ticks, ran {tickCount}");
        Assert.InRange(tickCount, 3000, 7000);
    }

    // ─────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────

    private static List<GameEvent> RunFullSession(uint seed)
    {
        var cfg = new ReflexGateConfig();
        var sim = new ReflexGateSimulation(cfg);
        sim.Reset(seed);
        return RunSimToCompletion(sim);
    }

    private static List<GameEvent> RunSimToCompletion(ReflexGateSimulation sim)
    {
        var allEvents = new List<GameEvent>(capacity: 256);
        var frameEvents = new List<GameEvent>(capacity: 16);

        for (int tick = 0; tick < 8000; tick++)
        {
            frameEvents.Clear();
            sim.FixedUpdate(tick, Dt, CenterInput, frameEvents);

            // Capture non-Tick events (Tick events are noise for replay comparison)
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
