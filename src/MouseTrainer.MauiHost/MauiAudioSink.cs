using System.Collections.Concurrent;
using MouseTrainer.Audio.Core;
using Plugin.Maui.Audio;

namespace MouseTrainer.MauiHost;

/// <summary>
/// Real audio playback sink using Plugin.Maui.Audio.
/// Replaces <see cref="LogAudioSink"/> for production use.
/// Thread-safe: all methods may be called from any thread.
/// </summary>
public sealed class MauiAudioSink : IAudioSink, IAsyncDisposable
{
    private readonly IAudioManager _audioManager;

    /// <summary>Cached raw WAV bytes keyed by asset name. Total payload &lt;600KB.</summary>
    private readonly ConcurrentDictionary<string, byte[]> _assetCache = new();

    /// <summary>Active looping players keyed by loop key (e.g. "drag", "amb").</summary>
    private readonly ConcurrentDictionary<string, IAudioPlayer> _loops = new();

    /// <summary>Optional log callback for debug diagnostics.</summary>
    private readonly Action<string>? _log;

    /// <summary>Cap simultaneous one-shots to prevent audio chaos on event spikes.</summary>
    private int _activeShotCount;
    private const int MaxConcurrentShots = 8;

    public MauiAudioSink(IAudioManager audioManager, Action<string>? log = null)
    {
        _audioManager = audioManager;
        _log = log;
    }

    public void PlayOneShot(in AudioCue cue)
    {
        if (Interlocked.Increment(ref _activeShotCount) > MaxConcurrentShots)
        {
            Interlocked.Decrement(ref _activeShotCount);
            return; // Drop — too many concurrent shots
        }

        var assetName = cue.AssetName;
        var volume = Clamp01(cue.Volume);
        var pitch = cue.Pitch;

        _ = Task.Run(async () =>
        {
            try
            {
                var bytes = await GetAssetBytesAsync(assetName);
                var ms = new MemoryStream(bytes, writable: false);
                var player = _audioManager.CreatePlayer(ms);

                player.Loop = false;
                player.Volume = volume;
                TrySetSpeed(player, pitch);

                player.Play();

                // Wait for playback to finish (duration + small margin)
                var waitMs = Math.Max(100, (int)(player.Duration * 1000) + 50);
                await Task.Delay(waitMs);

                player.Dispose();
            }
            catch (Exception ex)
            {
                _log?.Invoke($"> [AudioSink] OneShot error ({assetName}): {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _activeShotCount);
            }
        });
    }

    public void StartLoop(in AudioCue cue, string loopKey)
    {
        // Bail if loop already active with this key
        if (_loops.ContainsKey(loopKey)) return;

        var assetName = cue.AssetName;
        var volume = Clamp01(cue.Volume);
        var pitch = cue.Pitch;

        _ = Task.Run(async () =>
        {
            try
            {
                var bytes = await GetAssetBytesAsync(assetName);
                var ms = new MemoryStream(bytes, writable: false);
                var player = _audioManager.CreatePlayer(ms);

                player.Loop = true;
                player.Volume = volume;
                TrySetSpeed(player, pitch);

                if (_loops.TryAdd(loopKey, player))
                {
                    player.Play();
                }
                else
                {
                    // Another thread raced us — dispose this duplicate
                    player.Dispose();
                    ms.Dispose();
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"> [AudioSink] StartLoop error ({loopKey}/{assetName}): {ex.Message}");
            }
        });
    }

    public void StopLoop(string loopKey)
    {
        if (_loops.TryRemove(loopKey, out var player))
        {
            try { player.Stop(); }
            catch { /* best effort */ }

            try { player.Dispose(); }
            catch { /* best effort */ }
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var key in _loops.Keys.ToArray())
            StopLoop(key);

        _assetCache.Clear();
        return ValueTask.CompletedTask;
    }

    // ─────────────────────────────────────────────────────
    //  Internals
    // ─────────────────────────────────────────────────────

    private async Task<byte[]> GetAssetBytesAsync(string assetName)
    {
        if (_assetCache.TryGetValue(assetName, out var cached))
            return cached;

        await using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        _assetCache[assetName] = bytes;
        return bytes;
    }

    private static void TrySetSpeed(IAudioPlayer player, float pitch)
    {
        if (!player.CanSetSpeed) return;
        if (pitch <= 0.01f) pitch = 1f;

        // Clamp to safe range
        var speed = Math.Clamp((double)pitch,
            player.MinimumSpeed,
            player.MaximumSpeed);

        player.Speed = speed;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
