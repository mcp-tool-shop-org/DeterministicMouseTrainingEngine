using MouseTrainer.Domain.Events;

namespace MouseTrainer.Audio.Core;

/// <summary>
/// Maps game events to one or more candidate audio assets.
/// Deterministic selection happens in AudioDirector.
/// </summary>
public sealed class AudioCueMap
{
    private readonly Dictionary<GameEventType, string[]> _candidates = new();

    public AudioCueMap Register(GameEventType type, params string[] assetNames)
    {
        _candidates[type] = assetNames;
        return this;
    }

    public bool TryGetCandidates(GameEventType type, out string[] assets)
        => _candidates.TryGetValue(type, out assets!);

    public static AudioCueMap Default() => new AudioCueMap()
        .Register(GameEventType.HitWall, "sfx_hit_01.wav", "sfx_hit_02.wav", "sfx_hit_03.wav")
        .Register(GameEventType.EnteredGate, "sfx_gate_01.wav", "sfx_gate_02.wav")
        .Register(GameEventType.ComboUp, "sfx_combo_01.wav", "sfx_combo_02.wav", "sfx_combo_03.wav")
        .Register(GameEventType.DragStart, "sfx_drag_start.wav")
        .Register(GameEventType.DragEnd, "sfx_drag_end.wav")
        .Register(GameEventType.LevelComplete, "sfx_level_complete.wav");
}
