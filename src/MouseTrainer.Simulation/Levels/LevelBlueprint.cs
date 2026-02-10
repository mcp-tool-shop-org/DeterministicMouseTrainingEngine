using MouseTrainer.Simulation.Modes.ReflexGates;

namespace MouseTrainer.Simulation.Levels;

/// <summary>
/// Immutable snapshot of a generated level. Pure data, no behavior, no back-references.
/// Acid test: serialize to disk, load in a different process, execute identically.
/// Sealed record class (not struct) because it holds IReadOnlyList.
/// </summary>
public sealed class LevelBlueprint
{
    /// <summary>Gate array (immutable after construction).</summary>
    public required IReadOnlyList<Gate> Gates { get; init; }

    /// <summary>Corridor width in virtual pixels.</summary>
    public required float PlayfieldWidth { get; init; }

    /// <summary>Corridor height in virtual pixels.</summary>
    public required float PlayfieldHeight { get; init; }

    /// <summary>Horizontal scroll speed in virtual pixels per second.</summary>
    public required float ScrollSpeed { get; init; }

    /// <summary>Number of gates (convenience, equals Gates.Count).</summary>
    public int GateCount => Gates.Count;
}
