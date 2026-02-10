using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Simulation.Modes.ReflexGates;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Scales Gate.ApertureHeight by a factor less than 1.0 (making gaps narrower).
/// Parameter: "factor" (float, range [0.1, 1.0], default 0.75).
/// All other gate fields and blueprint metadata are preserved.
/// </summary>
public sealed class NarrowMarginMutator : IBlueprintMutator
{
    private readonly float _factor;

    public NarrowMarginMutator(float factor = 0.75f)
    {
        if (factor < 0.1f || factor > 1.0f)
            throw new ArgumentOutOfRangeException(nameof(factor),
                $"NarrowMargin factor must be in [0.1, 1.0]. Got: {factor}");
        _factor = factor;
    }

    public NarrowMarginMutator(MutatorSpec spec)
        : this(GetFactor(spec)) { }

    public LevelBlueprint Apply(LevelBlueprint blueprint)
    {
        var gates = new Gate[blueprint.Gates.Count];
        for (int i = 0; i < blueprint.Gates.Count; i++)
        {
            var src = blueprint.Gates[i];
            gates[i] = new Gate
            {
                WallX = src.WallX,
                RestCenterY = src.RestCenterY,
                ApertureHeight = src.ApertureHeight * _factor,
                Amplitude = src.Amplitude,
                Phase = src.Phase,
                FreqHz = src.FreqHz,
            };
        }

        return new LevelBlueprint
        {
            Gates = gates,
            PlayfieldWidth = blueprint.PlayfieldWidth,
            PlayfieldHeight = blueprint.PlayfieldHeight,
            ScrollSpeed = blueprint.ScrollSpeed,
        };
    }

    private static float GetFactor(MutatorSpec spec)
    {
        for (int i = 0; i < spec.Params.Count; i++)
        {
            if (spec.Params[i].Key == "factor")
                return spec.Params[i].Value;
        }
        return 0.75f;
    }
}
