using MouseTrainer.Domain.Events;using MouseTrainer.Domain.Input;
namespace MouseTrainer.Simulation.Core;
public interface IGameSimulation{void Reset(uint sessionSeed);void FixedUpdate(long tick,float dt,in PointerInput input,List<GameEvent> events);}
