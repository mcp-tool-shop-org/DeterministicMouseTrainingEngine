namespace MouseTrainer.MauiHost;

/// <summary>
/// A completed session persisted to disk. Mirrors SessionResult plus computed metrics.
/// All fields are immutable — safe for caching and display.
/// </summary>
public sealed record StoredSession(
    string Id,
    DateTime CompletedUtc,
    uint Seed,
    TimeSpan Elapsed,
    int TotalScore,
    int MaxCombo,
    int GatesPassed,
    int GatesTotal,
    float MeanOffset,
    float WorstOffset,
    IReadOnlyList<StoredGateResult> Gates,
    string? RunId = null);

/// <summary>
/// Per-gate outcome stored alongside each session.
/// </summary>
public readonly record struct StoredGateResult(
    int GateIndex,
    bool Passed,
    int Score,
    float OffsetNormalized);

/// <summary>
/// Computed summary of personal best records. Recalculated on each query from stored sessions.
/// </summary>
public sealed record PersonalBests(
    int BestScore,
    int BestCombo,
    int CleanRuns,
    float BestAccuracy,
    uint? BestScoreSeed,
    int? SeedBestScore,
    float? SeedBestAccuracy);

/// <summary>
/// Computed lifetime aggregates across all stored sessions.
/// </summary>
public sealed record LifetimeStats(
    int TotalSessions,
    int TotalGatesPassed,
    int TotalGatesAttempted,
    float OverallAccuracy,
    float MeanScore,
    float MeanOffset,
    int CleanRuns,
    int UniqueSeedsPlayed);
