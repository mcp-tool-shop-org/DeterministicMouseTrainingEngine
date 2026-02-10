using MouseTrainer.Domain.Runs;

namespace MouseTrainer.Simulation.Levels;

/// <summary>
/// Generates a LevelBlueprint from a RunDescriptor.
/// Deterministic contract: same descriptor = same blueprint, bit-for-bit.
/// </summary>
public interface ILevelGenerator
{
    LevelBlueprint Generate(RunDescriptor run);
}
