namespace MouseTrainer.Domain.Runs;

/// <summary>
/// Identifies a game mode. String-based for forward-compat
/// (new modes don't require enum changes in Domain).
/// </summary>
public readonly record struct ModeId(string Value)
{
    public static readonly ModeId ReflexGates = new("ReflexGates");

    public override string ToString() => Value;
}
