using MouseTrainer.Domain.Events;
namespace MouseTrainer.Simulation.Core;
public sealed class FrameResult{public required long Tick{get;init;}public required IReadOnlyList<GameEvent> Events{get;init;}public required float Alpha{get;init;}}
