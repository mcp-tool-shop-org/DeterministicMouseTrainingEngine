using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Session;
using Xunit;

namespace MouseTrainer.Tests.Determinism;

/// <summary>
/// Unit tests for SessionController state machine + event aggregation.
/// Tests the pure orchestration logic in isolation from the simulation.
/// </summary>
public class SessionControllerTests
{
    // ─────────────────────────────────────────────────────
    //  1. State transitions
    // ─────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsReady()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);

        Assert.Equal(SessionState.Ready, sc.State);
    }

    [Fact]
    public void Start_TransitionsTo_Playing()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);
        sc.Start();

        Assert.Equal(SessionState.Playing, sc.State);
    }

    [Fact]
    public void Start_OnlyFromReady_Ignored_InPlaying()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);
        sc.Start();

        // Second Start should be a no-op
        sc.Start();
        Assert.Equal(SessionState.Playing, sc.State);
    }

    [Fact]
    public void LevelComplete_TransitionsTo_Results()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);
        sc.Start();

        var events = new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 100, Arg1: 3)
        };

        bool transitioned = sc.ApplyEvents(events);

        Assert.True(transitioned);
        Assert.Equal(SessionState.Results, sc.State);
    }

    [Fact]
    public void ApplyEvents_Ignored_InReadyState()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);

        var events = new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 0, Arg1: 85)
        };

        bool transitioned = sc.ApplyEvents(events);

        Assert.False(transitioned);
        Assert.Equal(0, sc.TotalScore);
    }

    // ─────────────────────────────────────────────────────
    //  2. Combo tracking
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ThreeConsecutive_EnteredGate_ComboEquals3()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.8f, Arg0: 2, Arg1: 80)
        });

        Assert.Equal(3, sc.CurrentCombo);
        Assert.Equal(3, sc.MaxCombo);
    }

    [Fact]
    public void HitWall_ResetsCombo_ToZero()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });

        Assert.Equal(2, sc.CurrentCombo);

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 2, Arg1: 500)
        });

        Assert.Equal(0, sc.CurrentCombo);
        Assert.Equal(2, sc.MaxCombo); // MaxCombo preserved
    }

    [Fact]
    public void MaxCombo_PreservedAcross_MultipleStreaks()
    {
        var sc = CreatePlayingSession();

        // Streak of 3
        for (int i = 0; i < 3; i++)
        {
            sc.ApplyEvents(new List<GameEvent>
            {
                new(GameEventType.EnteredGate, Intensity: 1f, Arg0: i, Arg1: 100)
            });
        }

        Assert.Equal(3, sc.MaxCombo);

        // Break streak
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 3, Arg1: 500)
        });

        // New streak of 2
        for (int i = 4; i < 6; i++)
        {
            sc.ApplyEvents(new List<GameEvent>
            {
                new(GameEventType.EnteredGate, Intensity: 1f, Arg0: i, Arg1: 100)
            });
        }

        Assert.Equal(2, sc.CurrentCombo);
        Assert.Equal(3, sc.MaxCombo); // Still 3 from first streak
    }

    // ─────────────────────────────────────────────────────
    //  3. Score accumulation
    // ─────────────────────────────────────────────────────

    [Fact]
    public void EnteredGate_AccumulatesScore()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 0, Arg1: 85)
        });

        Assert.Equal(85, sc.TotalScore);
    }

    [Fact]
    public void MultipleGates_AccumulateTotalScore()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.8f, Arg0: 1, Arg1: 80)
        });

        Assert.Equal(180, sc.TotalScore);
    }

    [Fact]
    public void HitWall_DoesNot_AddScore()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 1, Arg1: 500)
        });

        Assert.Equal(100, sc.TotalScore); // Only first gate's score
    }

    // ─────────────────────────────────────────────────────
    //  4. Gate result offset decoding
    // ─────────────────────────────────────────────────────

    [Fact]
    public void EnteredGate_OffsetDecoding()
    {
        // Intensity = 1f - normalizedOffset * 0.5f
        // So normalizedOffset = (1f - Intensity) * 2f
        // Intensity=0.8 → offset = (1-0.8)*2 = 0.4

        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.8f, Arg0: 0, Arg1: 80)
        });

        // Complete the session to access results
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 80, Arg1: 1)
        });

        var result = sc.GetResult();
        Assert.NotNull(result);
        Assert.Single(result.Gates);
        Assert.True(result.Gates[0].Passed);

        // OffsetNormalized = (1 - 0.8) * 2 = 0.4
        Assert.Equal(0.4f, result.Gates[0].OffsetNormalized, precision: 3);
    }

    [Fact]
    public void HitWall_OffsetDecoding()
    {
        // Arg1 encodes missDistance * 1000
        // Arg1=350 → missDistance = 0.35
        // OffsetNormalized = 1 + missDistance = 1.35

        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.35f, Arg0: 0, Arg1: 350)
        });

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var result = sc.GetResult();
        Assert.NotNull(result);
        Assert.Single(result.Gates);
        Assert.False(result.Gates[0].Passed);
        Assert.Equal(0, result.Gates[0].Score);

        // OffsetNormalized = 1 + (350/1000) = 1.35
        Assert.Equal(1.35f, result.Gates[0].OffsetNormalized, precision: 3);
    }

    // ─────────────────────────────────────────────────────
    //  5. GetResult completeness
    // ─────────────────────────────────────────────────────

    [Fact]
    public void GetResult_ReturnsNull_BeforeLevelComplete()
    {
        var sc = CreatePlayingSession();
        Assert.Null(sc.GetResult());
    }

    [Fact]
    public void GetResult_ReturnsValidResult_AfterLevelComplete()
    {
        var sc = CreatePlayingSession();

        // Pass 2, miss 1
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.HitWall, Intensity: 0.5f, Arg0: 2, Arg1: 500)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 190, Arg1: 0)
        });

        var result = sc.GetResult();
        Assert.NotNull(result);

        Assert.Equal(0xC0FFEEu, result.Seed);
        Assert.Equal(190, result.TotalScore);
        Assert.Equal(2, result.MaxCombo);
        Assert.Equal(2, result.GatesPassed);
        Assert.Equal(12, result.GatesTotal);
        Assert.Equal(3, result.Gates.Count);
    }

    // ─────────────────────────────────────────────────────
    //  6. Idempotent reset
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ResetToReady_ClearsAllState()
    {
        var sc = CreatePlayingSession();

        // Accumulate some state
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 1f, Arg0: 0, Arg1: 100)
        });
        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.EnteredGate, Intensity: 0.9f, Arg0: 1, Arg1: 90)
        });

        Assert.Equal(190, sc.TotalScore);
        Assert.Equal(2, sc.CurrentCombo);

        // Reset
        sc.ResetToReady(0xBEEFu, 8);

        Assert.Equal(SessionState.Ready, sc.State);
        Assert.Equal(0xBEEFu, sc.Seed);
        Assert.Equal(0, sc.TotalScore);
        Assert.Equal(0, sc.CurrentCombo);
        Assert.Equal(0, sc.MaxCombo);
    }

    [Fact]
    public void ResetToReady_AfterResults_AllowsNewSession()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        Assert.Equal(SessionState.Results, sc.State);

        // Reset and start new session
        sc.ResetToReady(0xAAAAAAAAu, 12);
        sc.Start();

        Assert.Equal(SessionState.Playing, sc.State);
        Assert.Equal(0, sc.TotalScore);
    }

    // ─────────────────────────────────────────────────────
    //  7. Tick events are ignored
    // ─────────────────────────────────────────────────────

    [Fact]
    public void TickEvents_DoNot_AffectState()
    {
        var sc = CreatePlayingSession();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.Tick, Intensity: 1f, Arg0: 42)
        });

        Assert.Equal(0, sc.TotalScore);
        Assert.Equal(0, sc.CurrentCombo);
        Assert.Equal(SessionState.Playing, sc.State);
    }

    // ─────────────────────────────────────────────────────
    //  8. RunDescriptor integration
    // ─────────────────────────────────────────────────────

    [Fact]
    public void ResetToReady_WithRunDescriptor_StoresSeedAndRunId()
    {
        var sc = new SessionController();
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);

        sc.ResetToReady(run, 12);

        Assert.Equal(0xC0FFEEu, sc.Seed);
        Assert.NotNull(sc.RunDescriptor);
        Assert.Equal(run, sc.RunDescriptor.Value);
        Assert.Equal(run.Id, sc.RunDescriptor.Value.Id);
    }

    [Fact]
    public void GetResult_WithRunDescriptor_IncludesRunId()
    {
        var sc = new SessionController();
        var run = RunDescriptor.Create(ModeId.ReflexGates, 0xC0FFEEu);
        sc.ResetToReady(run, 12);
        sc.Start();

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var result = sc.GetResult();
        Assert.NotNull(result);
        Assert.NotNull(result.RunId);
        Assert.Equal(run.Id, result.RunId.Value);
    }

    [Fact]
    public void GetResult_WithoutRunDescriptor_RunIdIsNull()
    {
        var sc = CreatePlayingSession(); // Uses old ResetToReady(uint, int)

        sc.ApplyEvents(new List<GameEvent>
        {
            new(GameEventType.LevelComplete, Intensity: 1f, Arg0: 0, Arg1: 0)
        });

        var result = sc.GetResult();
        Assert.NotNull(result);
        Assert.Null(result.RunId);
    }

    // ─────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────

    private static SessionController CreatePlayingSession()
    {
        var sc = new SessionController();
        sc.ResetToReady(0xC0FFEEu, 12);
        sc.Start();
        return sc;
    }
}
