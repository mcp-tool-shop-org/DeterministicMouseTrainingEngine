using MouseTrainer.Domain.Runs;

namespace MouseTrainer.Simulation.Session;

/// <summary>
/// Session lifecycle phases.
/// </summary>
public enum SessionState
{
    Ready,
    Playing,
    Results
}

/// <summary>
/// Per-gate outcome recorded during play.
/// </summary>
public readonly record struct GateResult(
    int GateIndex,
    bool Passed,
    int Score,
    float OffsetNormalized);

/// <summary>
/// Immutable summary of a completed session.
/// Sealed record class (not struct) because it holds an IReadOnlyList.
/// </summary>
public sealed record SessionResult(
    uint Seed,
    TimeSpan Elapsed,
    int TotalScore,
    int MaxCombo,
    int GatesPassed,
    int GatesTotal,
    IReadOnlyList<GateResult> Gates,
    RunId? RunId = null);
