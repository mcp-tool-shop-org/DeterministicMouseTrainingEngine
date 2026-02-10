namespace MouseTrainer.Domain.Runs;

/// <summary>
/// Platform-stable deterministic hash of a RunDescriptor.
/// NOT .NET GetHashCode(). Computed via FNV-1a 64-bit on canonical bytes.
/// </summary>
public readonly record struct RunId(ulong Value)
{
    public override string ToString() => $"R-{Value:X16}";
}
