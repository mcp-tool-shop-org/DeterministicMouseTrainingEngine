namespace MouseTrainer.Simulation.Debug;

/// <summary>
/// Optional debug telemetry for hosts/tools. Not used by gameplay logic.
/// Simulations may implement this to expose internal state for visualization.
/// </summary>
public interface ISimDebugOverlay
{
    bool TryGetGatePreview(float simTimeSeconds, out GatePreview preview);
}

/// <summary>
/// Snapshot of the next gate's state for debug visualization.
/// All values in virtual coordinate space (1920x1080).
/// </summary>
public readonly record struct GatePreview(
    float WallX,
    float CenterY,
    float ApertureHeight,
    int GateIndex,
    float ScrollX
);
