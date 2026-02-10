using MouseTrainer.Domain.Events;
using MouseTrainer.Domain.Utility;

namespace MouseTrainer.Audio.Core;

/// <summary>
/// Deterministic audio cue resolver.
/// Takes simulation events + tick and emits stable audio cues.
/// </summary>
public sealed class AudioDirector
{
    private readonly AudioCueMap _map;
    private readonly IAudioSink _sink;

    // Basic rate-limits to avoid spam (deterministic per tick).
    private long _lastHitWallTick = -9999;

    public AudioDirector(AudioCueMap map, IAudioSink sink)
    {
        _map = map;
        _sink = sink;
    }

    public void Process(IReadOnlyList<GameEvent> events, long tick, uint sessionSeed)
    {
        int seq = 0;

        foreach (var ev in events)
        {
            seq++;

            switch (ev.Type)
            {
                case GameEventType.DragStart:
                    _sink.StartLoop(
                        new AudioCue("sfx_drag_loop.wav", Volume: 0.25f, Pitch: 1f, Loop: true),
                        loopKey: "drag");
                    break;

                case GameEventType.DragEnd:
                    _sink.StopLoop("drag");
                    break;

                case GameEventType.HitWall:
                    // Rate limit: at most once every 6 ticks (~100ms at 60Hz)
                    if (tick - _lastHitWallTick < 6) break;
                    _lastHitWallTick = tick;
                    EmitOneShot(ev, tick, sessionSeed, seq);
                    break;

                default:
                    EmitOneShot(ev, tick, sessionSeed, seq);
                    break;
            }
        }
    }

    private void EmitOneShot(in GameEvent ev, long tick, uint sessionSeed, int seq)
    {
        if (!_map.TryGetCandidates(ev.Type, out var assets) || assets.Length == 0)
            return;

        // Deterministic choice among candidates:
        var seed = DeterministicRng.Mix(sessionSeed, (uint)tick, (uint)seq);
        var rng = new DeterministicRng(seed);
        var pick = assets[rng.NextInt(0, assets.Length)];

        // Deterministic bounded variation:
        var vol = Clamp01(0.6f + 0.4f * Clamp01(ev.Intensity));
        var pitchJitter = 0.97f + 0.06f * rng.NextFloat01(); // [0.97, 1.03]
        var pitch = Clamp(pitchJitter, 0.9f, 1.1f);

        _sink.PlayOneShot(new AudioCue(pick, Volume: vol, Pitch: pitch, Loop: false));
    }

    private static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
}
