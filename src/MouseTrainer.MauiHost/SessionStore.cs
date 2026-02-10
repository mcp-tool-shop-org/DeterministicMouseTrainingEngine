using System.Text.Json;
using System.Text.Json.Serialization;
using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Session;

namespace MouseTrainer.MauiHost;

/// <summary>
/// JSON-file-backed session persistence with personal best queries and lifetime stats.
/// Thread-safe via lock. Loads entire history into memory on startup (~1KB per session).
/// Stats are cosmetic — failures are logged, never crash the app.
/// </summary>
public sealed class SessionStore
{
    private const int CurrentSchemaVersion = 2;

    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly Action<string>? _log;
    private StatsFile _data = new();
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new TimeSpanJsonConverter() }
    };

    public SessionStore(Action<string>? log = null, string? basePath = null)
    {
        _log = log;
        var appData = basePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MouseTrainer");
        _filePath = Path.Combine(appData, "stats.json");
    }

    // ─────────────────────────────────────────────────────
    //  Load / Save
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Load session history from disk. Safe to call multiple times (no-op after first load).
    /// </summary>
    public async Task LoadAsync()
    {
        lock (_lock) { if (_loaded) return; }

        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var parsed = JsonSerializer.Deserialize<StatsFile>(json, JsonOpts);
                if (parsed != null)
                {
                    MigrateIfNeeded(parsed);
                    lock (_lock) { _data = parsed; _loaded = true; }
                    _log?.Invoke($"> [Stats] Loaded {parsed.Sessions.Count} sessions from disk.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"> [Stats] Load error: {ex.Message}. Starting fresh.");
        }

        lock (_lock) { _data = new StatsFile(); _loaded = true; }
        _log?.Invoke("> [Stats] No history found. Starting fresh.");
    }

    /// <summary>
    /// Persist a completed session. Appends to in-memory list and writes to disk.
    /// </summary>
    public async Task SaveSessionAsync(SessionResult result)
    {
        var stored = ToStored(result);

        lock (_lock)
        {
            _data.Sessions.Add(stored);
        }

        await WriteToDiskAsync();
        _log?.Invoke($"> [Stats] Saved session {stored.Id[..8]}. Total: {_data.Sessions.Count}");
    }

    private async Task WriteToDiskAsync()
    {
        try
        {
            string json;
            lock (_lock)
            {
                json = JsonSerializer.Serialize(_data, JsonOpts);
            }

            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"> [Stats] Write error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────
    //  Queries
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Compute personal best records. Optionally includes seed-specific bests.
    /// </summary>
    public PersonalBests GetPersonalBests(uint? currentSeed = null)
    {
        lock (_lock)
        {
            var sessions = _data.Sessions;
            if (sessions.Count == 0)
            {
                return new PersonalBests(
                    BestScore: 0, BestCombo: 0, CleanRuns: 0,
                    BestAccuracy: 0f, BestScoreSeed: null,
                    SeedBestScore: null, SeedBestAccuracy: null);
            }

            int bestScore = 0;
            int bestCombo = 0;
            int cleanRuns = 0;
            float bestAccuracy = 0f;
            uint? bestScoreSeed = null;

            int? seedBestScore = null;
            float? seedBestAccuracy = null;

            foreach (var s in sessions)
            {
                if (s.TotalScore > bestScore)
                {
                    bestScore = s.TotalScore;
                    bestScoreSeed = s.Seed;
                }

                if (s.MaxCombo > bestCombo)
                    bestCombo = s.MaxCombo;

                float acc = s.GatesTotal > 0 ? (float)s.GatesPassed / s.GatesTotal : 0f;
                if (acc > bestAccuracy)
                    bestAccuracy = acc;

                if (s.GatesPassed == s.GatesTotal)
                    cleanRuns++;

                if (currentSeed.HasValue && s.Seed == currentSeed.Value)
                {
                    if (!seedBestScore.HasValue || s.TotalScore > seedBestScore.Value)
                        seedBestScore = s.TotalScore;

                    if (!seedBestAccuracy.HasValue || acc > seedBestAccuracy.Value)
                        seedBestAccuracy = acc;
                }
            }

            return new PersonalBests(
                BestScore: bestScore,
                BestCombo: bestCombo,
                CleanRuns: cleanRuns,
                BestAccuracy: bestAccuracy * 100f,
                BestScoreSeed: bestScoreSeed,
                SeedBestScore: seedBestScore,
                SeedBestAccuracy: seedBestAccuracy.HasValue ? seedBestAccuracy.Value * 100f : null);
        }
    }

    /// <summary>
    /// Compute lifetime aggregates from all stored sessions.
    /// </summary>
    public LifetimeStats GetLifetimeStats()
    {
        lock (_lock)
        {
            var sessions = _data.Sessions;
            if (sessions.Count == 0)
            {
                return new LifetimeStats(
                    TotalSessions: 0, TotalGatesPassed: 0, TotalGatesAttempted: 0,
                    OverallAccuracy: 0f, MeanScore: 0f, MeanOffset: 0f,
                    CleanRuns: 0, UniqueSeedsPlayed: 0);
            }

            int totalPassed = 0;
            int totalAttempted = 0;
            long totalScore = 0;
            double totalOffset = 0;
            int totalPassedGatesWithOffset = 0;
            int cleanRuns = 0;
            var uniqueSeeds = new HashSet<uint>();

            foreach (var s in sessions)
            {
                totalPassed += s.GatesPassed;
                totalAttempted += s.GatesTotal;
                totalScore += s.TotalScore;
                uniqueSeeds.Add(s.Seed);

                if (s.GatesPassed == s.GatesTotal)
                    cleanRuns++;

                // Accumulate offset from passed gates
                if (s.MeanOffset > 0f && s.GatesPassed > 0)
                {
                    totalOffset += s.MeanOffset * s.GatesPassed;
                    totalPassedGatesWithOffset += s.GatesPassed;
                }
            }

            float overallAccuracy = totalAttempted > 0
                ? (float)totalPassed / totalAttempted * 100f : 0f;
            float meanScore = sessions.Count > 0
                ? (float)totalScore / sessions.Count : 0f;
            float meanOffset = totalPassedGatesWithOffset > 0
                ? (float)(totalOffset / totalPassedGatesWithOffset) : 0f;

            return new LifetimeStats(
                TotalSessions: sessions.Count,
                TotalGatesPassed: totalPassed,
                TotalGatesAttempted: totalAttempted,
                OverallAccuracy: overallAccuracy,
                MeanScore: meanScore,
                MeanOffset: meanOffset,
                CleanRuns: cleanRuns,
                UniqueSeedsPlayed: uniqueSeeds.Count);
        }
    }

    /// <summary>
    /// Get most recent sessions, ordered by completion date descending.
    /// </summary>
    public IReadOnlyList<StoredSession> GetRecentSessions(int limit = 20)
    {
        lock (_lock)
        {
            return _data.Sessions
                .OrderByDescending(s => s.CompletedUtc)
                .Take(limit)
                .ToList();
        }
    }

    /// <summary>
    /// Get sessions for a specific seed, ordered by score descending.
    /// </summary>
    public IReadOnlyList<StoredSession> GetSessionsForSeed(uint seed, int limit = 10)
    {
        lock (_lock)
        {
            return _data.Sessions
                .Where(s => s.Seed == seed)
                .OrderByDescending(s => s.TotalScore)
                .Take(limit)
                .ToList();
        }
    }

    // ─────────────────────────────────────────────────────
    //  Conversion
    // ─────────────────────────────────────────────────────

    private static StoredSession ToStored(SessionResult r)
    {
        float meanOffset = 0f;
        float worstOffset = 0f;
        int passedCount = 0;
        float offsetSum = 0f;

        foreach (var g in r.Gates)
        {
            if (!g.Passed) continue;
            passedCount++;
            offsetSum += g.OffsetNormalized;
            if (g.OffsetNormalized > worstOffset)
                worstOffset = g.OffsetNormalized;
        }

        if (passedCount > 0)
            meanOffset = offsetSum / passedCount;

        var gates = new List<StoredGateResult>(r.Gates.Count);
        foreach (var g in r.Gates)
        {
            gates.Add(new StoredGateResult(
                g.GateIndex, g.Passed, g.Score, g.OffsetNormalized));
        }

        return new StoredSession(
            Id: Guid.NewGuid().ToString("N"),
            CompletedUtc: DateTime.UtcNow,
            Seed: r.Seed,
            Elapsed: r.Elapsed,
            TotalScore: r.TotalScore,
            MaxCombo: r.MaxCombo,
            GatesPassed: r.GatesPassed,
            GatesTotal: r.GatesTotal,
            MeanOffset: meanOffset,
            WorstOffset: worstOffset,
            Gates: gates,
            RunId: r.RunId?.ToString());
    }

    // ─────────────────────────────────────────────────────
    //  Schema migration
    // ─────────────────────────────────────────────────────

    private static void MigrateIfNeeded(StatsFile data)
    {
        // v1 → v2: Backfill RunId for legacy sessions
        if (data.SchemaVersion < 2)
        {
            for (int i = 0; i < data.Sessions.Count; i++)
            {
                var s = data.Sessions[i];
                if (s.RunId == null)
                {
                    var run = RunDescriptor.Create(ModeId.ReflexGates, s.Seed);
                    data.Sessions[i] = s with { RunId = run.Id.ToString() };
                }
            }
        }

        data.SchemaVersion = CurrentSchemaVersion;
    }

    // ─────────────────────────────────────────────────────
    //  Internal file model
    // ─────────────────────────────────────────────────────

    private sealed class StatsFile
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<StoredSession> Sessions { get; set; } = new();
    }

    /// <summary>
    /// Custom converter for TimeSpan — System.Text.Json doesn't natively handle TimeSpan.
    /// Serializes as total seconds (float) for readability.
    /// </summary>
    private sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            return TimeSpan.FromSeconds(reader.GetDouble());
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value,
            JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.TotalSeconds);
        }
    }
}
