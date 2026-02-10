using MouseTrainer.Domain.Runs;
using MouseTrainer.Simulation.Levels;
using MouseTrainer.Simulation.Modes.ReflexGates;

namespace MouseTrainer.Simulation.Mutators;

/// <summary>
/// Reshapes the difficulty gradient across the gate array via power-curve re-interpolation.
/// Parameter: "curve" (float, range [-2.0, 2.0], default 0.0).
///   Positive = back-loaded (easier early, harder late — amplifies the existing ramp).
///   Negative = front-loaded (harder early, easier late — flattens/reverses the ramp).
///   Zero = no change.
/// Transforms ApertureHeight, Amplitude, and FreqHz distribution.
/// Preserves WallX, RestCenterY, and Phase (position, layout, randomness untouched).
/// </summary>
public sealed class DifficultyCurveMutator : IBlueprintMutator
{
    private readonly float _curve;

    public DifficultyCurveMutator(float curve = 0f)
    {
        if (curve < -2.0f || curve > 2.0f)
            throw new ArgumentOutOfRangeException(nameof(curve),
                $"DifficultyCurve curve must be in [-2.0, 2.0]. Got: {curve}");
        _curve = curve;
    }

    public DifficultyCurveMutator(MutatorSpec spec)
        : this(GetCurve(spec)) { }

    public LevelBlueprint Apply(LevelBlueprint blueprint)
    {
        int n = blueprint.Gates.Count;
        if (n <= 1)
        {
            // Single gate or empty: return clone with no changes
            return CloneBlueprint(blueprint);
        }

        // 1. Find min/max across all gates for the three transformed properties
        float minAperture = float.MaxValue, maxAperture = float.MinValue;
        float minAmplitude = float.MaxValue, maxAmplitude = float.MinValue;
        float minFreq = float.MaxValue, maxFreq = float.MinValue;

        for (int i = 0; i < n; i++)
        {
            var g = blueprint.Gates[i];
            if (g.ApertureHeight < minAperture) minAperture = g.ApertureHeight;
            if (g.ApertureHeight > maxAperture) maxAperture = g.ApertureHeight;
            if (g.Amplitude < minAmplitude) minAmplitude = g.Amplitude;
            if (g.Amplitude > maxAmplitude) maxAmplitude = g.Amplitude;
            if (g.FreqHz < minFreq) minFreq = g.FreqHz;
            if (g.FreqHz > maxFreq) maxFreq = g.FreqHz;
        }

        // 2. Compute power exponent: pow(2, curve)
        //    curve=0 → exponent=1 (identity), curve=1 → exponent=2, curve=-1 → exponent=0.5
        float exponent = MathF.Pow(2f, _curve);

        // 3. Re-interpolate each gate along the power curve
        var gates = new Gate[n];
        for (int i = 0; i < n; i++)
        {
            var src = blueprint.Gates[i];
            float tOriginal = (float)i / (n - 1);
            float tNew = MathF.Pow(tOriginal, exponent);

            // Aperture: lerp from max to min (larger aperture at start = easier, smaller at end = harder)
            float aperture = maxAperture + tNew * (minAperture - maxAperture);

            // Amplitude: lerp from min to max (less movement at start, more at end)
            float amplitude = minAmplitude + tNew * (maxAmplitude - minAmplitude);

            // FreqHz: lerp from min to max (slower at start, faster at end)
            float freq = minFreq + tNew * (maxFreq - minFreq);

            gates[i] = new Gate
            {
                WallX = src.WallX,
                RestCenterY = src.RestCenterY,
                ApertureHeight = aperture,
                Amplitude = amplitude,
                Phase = src.Phase,
                FreqHz = freq,
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

    private static LevelBlueprint CloneBlueprint(LevelBlueprint blueprint)
    {
        var gates = new Gate[blueprint.Gates.Count];
        for (int i = 0; i < blueprint.Gates.Count; i++)
        {
            var src = blueprint.Gates[i];
            gates[i] = new Gate
            {
                WallX = src.WallX,
                RestCenterY = src.RestCenterY,
                ApertureHeight = src.ApertureHeight,
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

    private static float GetCurve(MutatorSpec spec)
    {
        for (int i = 0; i < spec.Params.Count; i++)
        {
            if (spec.Params[i].Key == "curve")
                return spec.Params[i].Value;
        }
        return 0f;
    }
}
