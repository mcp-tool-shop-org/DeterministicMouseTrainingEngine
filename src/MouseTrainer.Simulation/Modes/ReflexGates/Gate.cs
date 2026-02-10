namespace MouseTrainer.Simulation.Modes.ReflexGates;

/// <summary>
/// Immutable definition of a single gate.
/// Generated once from the session seed; never mutated during the level.
/// </summary>
public readonly struct Gate
{
    /// <summary>Fixed X position of this gate in virtual space.</summary>
    public float WallX { get; init; }

    /// <summary>Resting center Y of the aperture (before oscillation).</summary>
    public float RestCenterY { get; init; }

    /// <summary>Height of the passable gap.</summary>
    public float ApertureHeight { get; init; }

    /// <summary>Peak vertical displacement from RestCenterY.</summary>
    public float Amplitude { get; init; }

    /// <summary>Phase offset in radians (seeded per gate).</summary>
    public float Phase { get; init; }

    /// <summary>Oscillation frequency in Hz.</summary>
    public float FreqHz { get; init; }

    /// <summary>
    /// Current center Y of the aperture at the given simulation time.
    /// </summary>
    public float CurrentCenterY(float simTimeSeconds)
    {
        return RestCenterY
            + Amplitude * MathF.Sin(Phase + 2f * MathF.PI * FreqHz * simTimeSeconds);
    }

    /// <summary>
    /// How far cursorY is from dead center, normalized to [0..1] where 0=center, 1=edge.
    /// Returns > 1 if the cursor is outside the aperture (wall hit).
    /// </summary>
    public float NormalizedOffset(float cursorY, float simTimeSeconds)
    {
        float centerY = CurrentCenterY(simTimeSeconds);
        float halfAperture = ApertureHeight * 0.5f;
        if (halfAperture <= 0f) return float.MaxValue;
        return MathF.Abs(cursorY - centerY) / halfAperture;
    }
}
