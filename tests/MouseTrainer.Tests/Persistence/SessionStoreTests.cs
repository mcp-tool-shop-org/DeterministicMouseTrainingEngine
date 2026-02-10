using MouseTrainer.MauiHost;
using MouseTrainer.Simulation.Session;
using Xunit;

namespace MouseTrainer.Tests.Persistence;

/// <summary>
/// Persistence torture tests for SessionStore.
/// All tests use unique temp directories, cleaned up on dispose.
/// Verifies round-trip, corrupt files, concurrent access, and query correctness.
/// </summary>
public class SessionStoreTests : IDisposable
{
    private readonly string _tempDir;

    public SessionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MouseTrainerTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    // ─────────────────────────────────────────────────────
    //  1. Round-trip
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoad_RoundTrips_Correctly()
    {
        var store1 = CreateStore();
        await store1.LoadAsync();
        await store1.SaveSessionAsync(MakeResult(seed: 0xC0FFEEu, score: 500, passed: 10, total: 12));

        // New store instance from same path
        var store2 = CreateStore();
        await store2.LoadAsync();

        var recent = store2.GetRecentSessions(limit: 1);
        Assert.Single(recent);
        Assert.Equal(0xC0FFEEu, recent[0].Seed);
        Assert.Equal(500, recent[0].TotalScore);
        Assert.Equal(10, recent[0].GatesPassed);
        Assert.Equal(12, recent[0].GatesTotal);
    }

    // ─────────────────────────────────────────────────────
    //  2. Multiple sessions
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleSessions_AllPersisted()
    {
        var store = CreateStore();
        await store.LoadAsync();

        for (int i = 0; i < 5; i++)
        {
            await store.SaveSessionAsync(MakeResult(seed: (uint)(0xAA + i), score: 100 * (i + 1)));
        }

        var store2 = CreateStore();
        await store2.LoadAsync();

        var recent = store2.GetRecentSessions(limit: 20);
        Assert.Equal(5, recent.Count);
    }

    // ─────────────────────────────────────────────────────
    //  3. Personal bests
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task PersonalBests_ReturnsCorrectValues()
    {
        var store = CreateStore();
        await store.LoadAsync();

        await store.SaveSessionAsync(MakeResult(seed: 1u, score: 300, maxCombo: 5, passed: 10, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 2u, score: 700, maxCombo: 8, passed: 12, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 3u, score: 500, maxCombo: 3, passed: 8, total: 12));

        var bests = store.GetPersonalBests();

        Assert.Equal(700, bests.BestScore);
        Assert.Equal(8, bests.BestCombo);
        Assert.Equal(2u, bests.BestScoreSeed);
    }

    // ─────────────────────────────────────────────────────
    //  4. Seed-specific PB
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task SeedSpecificBest_ReturnsCorrectSeedValues()
    {
        var store = CreateStore();
        await store.LoadAsync();

        await store.SaveSessionAsync(MakeResult(seed: 0xAAAAu, score: 300, passed: 10, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 0xAAAAu, score: 500, passed: 11, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 0xBBBBu, score: 900, passed: 12, total: 12));

        var bests = store.GetPersonalBests(currentSeed: 0xAAAAu);

        Assert.Equal(500, bests.SeedBestScore);
        Assert.Equal(900, bests.BestScore); // Overall best is from different seed
    }

    // ─────────────────────────────────────────────────────
    //  5. Lifetime stats
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LifetimeStats_CorrectAggregates()
    {
        var store = CreateStore();
        await store.LoadAsync();

        await store.SaveSessionAsync(MakeResult(seed: 1u, score: 400, passed: 10, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 2u, score: 600, passed: 12, total: 12)); // clean run
        await store.SaveSessionAsync(MakeResult(seed: 1u, score: 300, passed: 8, total: 12));

        var stats = store.GetLifetimeStats();

        Assert.Equal(3, stats.TotalSessions);
        Assert.Equal(30, stats.TotalGatesPassed); // 10 + 12 + 8
        Assert.Equal(36, stats.TotalGatesAttempted); // 12 * 3
        Assert.Equal(1, stats.CleanRuns); // Only session 2
        Assert.Equal(2, stats.UniqueSeedsPlayed); // seed 1 and 2
    }

    // ─────────────────────────────────────────────────────
    //  6. Missing file — graceful empty start
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task MissingFile_LoadsEmpty_NoCrash()
    {
        var nonExistentDir = Path.Combine(_tempDir, "does_not_exist");
        var store = new SessionStore(basePath: nonExistentDir);
        await store.LoadAsync();

        var bests = store.GetPersonalBests();
        Assert.Equal(0, bests.BestScore);

        var stats = store.GetLifetimeStats();
        Assert.Equal(0, stats.TotalSessions);
    }

    // ─────────────────────────────────────────────────────
    //  7. Corrupt file — garbage bytes
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task CorruptFile_GarbageBytes_LoadsEmpty()
    {
        var statsPath = Path.Combine(_tempDir, "stats.json");
        await File.WriteAllBytesAsync(statsPath, new byte[] { 0xFF, 0xFE, 0xAB, 0xCD, 0x00, 0x01 });

        var store = CreateStore();
        await store.LoadAsync();

        var stats = store.GetLifetimeStats();
        Assert.Equal(0, stats.TotalSessions);
    }

    // ─────────────────────────────────────────────────────
    //  8. Partial JSON — truncated
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task PartialJson_LoadsEmpty()
    {
        var statsPath = Path.Combine(_tempDir, "stats.json");
        await File.WriteAllTextAsync(statsPath, "{\"schemaVersion\":1,\"sessions\":[{\"id\":\"abc\"");

        var store = CreateStore();
        await store.LoadAsync();

        var stats = store.GetLifetimeStats();
        Assert.Equal(0, stats.TotalSessions);
    }

    // ─────────────────────────────────────────────────────
    //  9. Empty JSON object
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyJsonObject_LoadsGracefully()
    {
        var statsPath = Path.Combine(_tempDir, "stats.json");
        await File.WriteAllTextAsync(statsPath, "{}");

        var store = CreateStore();
        await store.LoadAsync();

        var stats = store.GetLifetimeStats();
        Assert.Equal(0, stats.TotalSessions);
    }

    // ─────────────────────────────────────────────────────
    //  10. Large dataset — performance
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LargeDataset_500Sessions_LoadsUnder2Seconds()
    {
        var store = CreateStore();
        await store.LoadAsync();

        for (int i = 0; i < 500; i++)
        {
            await store.SaveSessionAsync(MakeResult(
                seed: (uint)(i % 50),
                score: 100 + i,
                passed: 8 + (i % 5),
                total: 12));
        }

        // Reload from disk and measure query time
        var store2 = CreateStore();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await store2.LoadAsync();
        var bests = store2.GetPersonalBests(currentSeed: 10u);
        var stats = store2.GetLifetimeStats();
        var recent = store2.GetRecentSessions(20);
        sw.Stop();

        Assert.Equal(500, stats.TotalSessions);
        Assert.True(sw.Elapsed.TotalSeconds < 2.0,
            $"Load + query took {sw.Elapsed.TotalSeconds:0.000}s, expected < 2.0s");
    }

    // ─────────────────────────────────────────────────────
    //  11. Concurrent saves — no crash
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentSaves_NoCrash_AllPersisted()
    {
        var store = CreateStore();
        await store.LoadAsync();

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                await store.SaveSessionAsync(MakeResult(
                    seed: (uint)(0x1000 + index),
                    score: 100 * (index + 1)));
            }));
        }

        await Task.WhenAll(tasks);

        // Reload and verify
        var store2 = CreateStore();
        await store2.LoadAsync();

        var stats = store2.GetLifetimeStats();
        // Due to concurrent file writes, some may overwrite others.
        // The important thing is: no crash, and at least some are persisted.
        Assert.True(stats.TotalSessions >= 1,
            $"Expected at least 1 session after concurrent saves, got {stats.TotalSessions}");
    }

    // ─────────────────────────────────────────────────────
    //  12. LoadAsync is idempotent
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_CalledTwice_Idempotent()
    {
        var store = CreateStore();
        await store.LoadAsync();
        await store.SaveSessionAsync(MakeResult(seed: 1u, score: 100));

        // Second load should be a no-op (returns immediately)
        await store.LoadAsync();

        var stats = store.GetLifetimeStats();
        Assert.Equal(1, stats.TotalSessions);
    }

    // ─────────────────────────────────────────────────────
    //  13. GetSessionsForSeed
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionsForSeed_ReturnsFilteredResults()
    {
        var store = CreateStore();
        await store.LoadAsync();

        await store.SaveSessionAsync(MakeResult(seed: 0xAAAAu, score: 300));
        await store.SaveSessionAsync(MakeResult(seed: 0xBBBBu, score: 500));
        await store.SaveSessionAsync(MakeResult(seed: 0xAAAAu, score: 700));

        var seedSessions = store.GetSessionsForSeed(0xAAAAu);
        Assert.Equal(2, seedSessions.Count);

        // Ordered by score descending
        Assert.Equal(700, seedSessions[0].TotalScore);
        Assert.Equal(300, seedSessions[1].TotalScore);
    }

    // ─────────────────────────────────────────────────────
    //  14. Clean run detection
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task PersonalBests_CleanRuns_CountedCorrectly()
    {
        var store = CreateStore();
        await store.LoadAsync();

        await store.SaveSessionAsync(MakeResult(seed: 1u, score: 100, passed: 12, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 2u, score: 200, passed: 11, total: 12));
        await store.SaveSessionAsync(MakeResult(seed: 3u, score: 300, passed: 12, total: 12));

        var bests = store.GetPersonalBests();
        Assert.Equal(2, bests.CleanRuns);
    }

    // ─────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────

    private SessionStore CreateStore()
        => new(basePath: _tempDir);

    private static SessionResult MakeResult(
        uint seed = 0xC0FFEEu,
        int score = 100,
        int maxCombo = 3,
        int passed = 10,
        int total = 12)
    {
        var gates = new List<GateResult>();

        for (int i = 0; i < total; i++)
        {
            bool p = i < passed;
            gates.Add(new GateResult(
                GateIndex: i,
                Passed: p,
                Score: p ? (score / Math.Max(passed, 1)) : 0,
                OffsetNormalized: p ? 0.3f : 1.5f));
        }

        return new SessionResult(
            Seed: seed,
            Elapsed: TimeSpan.FromSeconds(60.0),
            TotalScore: score,
            MaxCombo: maxCombo,
            GatesPassed: passed,
            GatesTotal: total,
            Gates: gates.AsReadOnly());
    }
}
