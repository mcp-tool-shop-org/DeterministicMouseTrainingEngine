namespace MouseTrainer.Domain.Runs;

/// <summary>
/// Identifies a blueprint mutator. String-based for forward-compat.
/// Same permanent protocol rules as ModeId: once created, frozen forever.
/// </summary>
public readonly record struct MutatorId(string Value)
{
    public static readonly MutatorId NarrowMargin = new("NarrowMargin");
    public static readonly MutatorId WideMargin = new("WideMargin");
    public static readonly MutatorId DifficultyCurve = new("DifficultyCurve");

    public override string ToString() => Value;
}
