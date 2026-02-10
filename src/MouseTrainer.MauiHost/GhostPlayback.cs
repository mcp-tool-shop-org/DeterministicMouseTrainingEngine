using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Replay;

namespace MouseTrainer.MauiHost;

/// <summary>
/// Self-contained ghost state manager. Encapsulates PB replay loading,
/// tick-indexed position lookup, and lifecycle management.
/// Ghost is purely visual — never affects simulation or scoring.
/// </summary>
public sealed class GhostPlayback
{
    private InputTrace? _trace;
    private int _ghostTick;
    private float _ghostX;
    private float _ghostY;
    private bool _active;
    private bool _loaded;

    public bool Active => _active;
    public float X => _ghostX;
    public float Y => _ghostY;

    /// <summary>
    /// Attempt to load a PB replay file for the given RunId.
    /// Returns true if successful and ghost is ready to play.
    /// On any failure: returns false, ghost disabled, error logged.
    /// </summary>
    public bool TryLoad(RunId runId, string replayDir, Action<string>? log = null)
    {
        Disable();

        var path = Path.Combine(replayDir, $"{runId}.mtr");
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            var envelope = ReplaySerializer.Read(stream);

            // Validate RunId matches
            if (envelope.RunId != runId)
            {
                log?.Invoke($"> Ghost: RunId mismatch in {path}");
                return false;
            }

            _trace = envelope.Trace;
            _loaded = true;
            log?.Invoke($"> Ghost: Loaded PB replay ({_trace.TotalTicks} ticks)");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"> Ghost: Failed to load {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reset ghost to tick 0 and activate playback.
    /// Must be called after TryLoad succeeds, before each session start.
    /// </summary>
    public void Reset()
    {
        _ghostTick = 0;
        _ghostX = 0f;
        _ghostY = 0f;
        _active = _loaded && _trace != null && _trace.TotalTicks > 0;
    }

    /// <summary>
    /// Advance the ghost by the specified number of ticks.
    /// Call once per frame with the same tick count the simulation advanced.
    /// Deactivates when the trace is exhausted.
    /// </summary>
    public void AdvanceTicks(int count)
    {
        if (!_active || _trace == null) return;

        for (int i = 0; i < count; i++)
        {
            if (_ghostTick >= _trace.TotalTicks)
            {
                _active = false;
                return;
            }

            var sample = _trace.At(_ghostTick);
            var (x, y) = sample.Dequantize();
            _ghostX = x;
            _ghostY = y;
            _ghostTick++;
        }
    }

    /// <summary>
    /// Fully disable ghost playback. Safe to call at any time.
    /// </summary>
    public void Disable()
    {
        _trace = null;
        _loaded = false;
        _active = false;
        _ghostTick = 0;
        _ghostX = 0f;
        _ghostY = 0f;
    }

    /// <summary>
    /// Check if a PB replay file exists for the given RunId (without loading it).
    /// </summary>
    public static bool PbExists(RunId runId, string replayDir)
    {
        return File.Exists(Path.Combine(replayDir, $"{runId}.mtr"));
    }
}
