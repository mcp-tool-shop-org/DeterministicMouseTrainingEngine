using MouseTrainer.Domain.Runs;
using MouseTrainer.Domain.Utility;
using MouseTrainer.Simulation.Levels;

namespace MouseTrainer.Simulation.Modes.ReflexGates;

/// <summary>
/// Extracted from ReflexGateSimulation.GenerateGates(). Produces a LevelBlueprint from RunDescriptor + config.
/// GeneratorVersion 1: linear difficulty ramp, xorshift32 RNG.
/// The GenerateGates body is a character-for-character copy of the original for bitwise equivalence.
/// </summary>
public sealed class ReflexGateGenerator : ILevelGenerator
{
    private readonly ReflexGateConfig _cfg;

    public ReflexGateGenerator(ReflexGateConfig? cfg = null)
    {
        _cfg = cfg ?? new ReflexGateConfig();
    }

    public LevelBlueprint Generate(RunDescriptor run)
    {
        var gates = GenerateGates(run.Seed);
        return new LevelBlueprint
        {
            Gates = gates,
            PlayfieldWidth = _cfg.PlayfieldWidth,
            PlayfieldHeight = _cfg.PlayfieldHeight,
            ScrollSpeed = _cfg.ScrollSpeed,
        };
    }

    /// <summary>
    /// Exact copy of ReflexGateSimulation.GenerateGates().
    /// Produces bitwise-identical output for the same seed + config.
    /// DO NOT modify without also updating ReflexGateSimulation.GenerateGates().
    /// </summary>
    private Gate[] GenerateGates(uint sessionSeed)
    {
        var rng = new DeterministicRng(sessionSeed);
        var gates = new Gate[_cfg.GateCount];
        float playfieldCenterY = _cfg.PlayfieldHeight * 0.5f;

        for (int i = 0; i < _cfg.GateCount; i++)
        {
            float t = _cfg.GateCount <= 1 ? 0f : (float)i / (_cfg.GateCount - 1);

            float aperture = _cfg.BaseApertureHeight
                + t * (_cfg.MinApertureHeight - _cfg.BaseApertureHeight);

            float amplitude = _cfg.BaseAmplitude
                + t * (_cfg.MaxAmplitude - _cfg.BaseAmplitude);

            float freq = _cfg.BaseFreqHz
                + t * (_cfg.MaxFreqHz - _cfg.BaseFreqHz);

            float phase = rng.NextFloat01() * 2f * MathF.PI;

            float maxCenterVariation = playfieldCenterY - amplitude - aperture * 0.5f;
            if (maxCenterVariation < 0f) maxCenterVariation = 0f;
            float centerVariation = (rng.NextFloat01() * 2f - 1f) * maxCenterVariation * 0.3f;
            float restCenterY = playfieldCenterY + centerVariation;

            gates[i] = new Gate
            {
                WallX = _cfg.FirstGateX + i * _cfg.GateSpacingX,
                RestCenterY = restCenterY,
                ApertureHeight = aperture,
                Amplitude = amplitude,
                Phase = phase,
                FreqHz = freq,
            };
        }

        return gates;
    }
}
