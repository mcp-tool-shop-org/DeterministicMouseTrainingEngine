using MouseTrainer.Audio.Core;

namespace MouseTrainer.MauiHost;

/// <summary>
/// Tiny audio sink that logs cues instead of playing them.
/// Swap with a real IAudioSink implementation later (e.g., Plugin.Maui.Audio),
/// while keeping the deterministic cueing layer unchanged.
/// </summary>
public sealed class LogAudioSink : IAudioSink
{
    private readonly Action<string> _log;

    public LogAudioSink(Action<string> log) => _log = log;

    public void PlayOneShot(in AudioCue cue)
        => _log($"> [SFX] {cue.AssetName} vol={cue.Volume:0.00} pitch={cue.Pitch:0.00}");

    public void StartLoop(in AudioCue cue, string loopKey)
        => _log($"> [LOOP START:{loopKey}] {cue.AssetName} vol={cue.Volume:0.00} pitch={cue.Pitch:0.00}");

    public void StopLoop(string loopKey)
        => _log($"> [LOOP STOP:{loopKey}]");
}
