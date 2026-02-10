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
    public float GateSpacingX { get; init; } = 450f;

    // --- Gate aperture (gap height) ---
    public float BaseApertureHeight { get; init; } = 200f;
    public float MinApertureHeight { get; init; } = 100f;

    // --- Gate oscillation ---
    // Welcoming ramp: gates 1-4 nearly stationary, 5-8 moderate, 9-12 hard.
    // Linear interpolation in GenerateGates from Base→Max across all gates.
    public float BaseAmplitude { get; init; } = 40f;
    public float MaxAmplitude { get; init; } = 350f;
    public float BaseFreqHz { get; init; } = 0.15f;
    public float MaxFreqHz { get; init; } = 1.2f;

    // --- Scroll speed ---
    // 70 px/s × ~5800px total = ~83 seconds per clean run
    public float ScrollSpeed { get; init; } = 70f;

    // --- Scoring ---
    public int CenterScore { get; init; } = 100;
    public int EdgeScore { get; init; } = 50;
    public int ComboThreshold { get; init; } = 3;
}
