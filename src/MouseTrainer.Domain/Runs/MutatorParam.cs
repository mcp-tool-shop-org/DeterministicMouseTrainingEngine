namespace MouseTrainer.Domain.Runs;

/// <summary>
/// Single key-value parameter for a mutator. Float-only for trivial canonicalization.
/// Validated at construction: no NaN, no Infinity, no empty keys.
/// </summary>
public readonly record struct MutatorParam
{
    public string Key { get; }
    public float Value { get; }

    public MutatorParam(string Key, float Value)
    {
        if (string.IsNullOrEmpty(Key))
            throw new ArgumentException("Key must not be null or empty.", nameof(Key));
        if (float.IsNaN(Value) || float.IsInfinity(Value))
            throw new ArgumentException($"Value must be finite. Got: {Value}", nameof(Value));

        this.Key = Key;
        this.Value = Value;
    }
}
