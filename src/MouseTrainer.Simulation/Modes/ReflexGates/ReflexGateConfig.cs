namespace MouseTrainer.Simulation.Modes.ReflexGates;

/// <summary>
/// All tuning knobs for a Reflex Gate level.
/// Immutable after construction — deterministic by design.
/// </summary>
public sealed class ReflexGateConfig
{
    // --- Virtual playfield ---
    public float PlayfieldWidth { get; init; } = 1920f;
    public float PlayfieldHeight { get; init; } = 1080f;

    // --- Gate layout ---
    public int GateCount { get; init; } = 12;
    public float FirstGateX { get; init; } = 400f;
    public float GateSpacingX { get; init; } = 300f;

    // --- Gate aperture (gap height) ---
    public float BaseApertureHeight { get; init; } = 200f;
    public float MinApertureHeight { get; init; } = 80f;

    // --- Gate oscillation ---
    public float BaseAmplitude { get; init; } = 150f;
    public float MaxAmplitude { get; init; } = 350f;
    public float BaseFreqHz { get; init; } = 0.4f;
    public float MaxFreqHz { get; init; } = 1.2f;

    // --- Scroll speed ---
    public float ScrollSpeed { get; init; } = 200f;

    // --- Scoring ---
    public int CenterScore { get; init; } = 100;
    public int EdgeScore { get; init; } = 50;
    public int ComboThreshold { get; init; } = 3;
}
