using MouseTrainer.Domain.Events;using MouseTrainer.Domain.Input;
namespace MouseTrainer.Simulation.Core;
public sealed class GameSimulation:IGameSimulation{public void Reset(uint sessionSeed){}public void FixedUpdate(long tick,float dt,in PointerInput input,List<GameEvent> events){events.Add(new GameEvent(GameEventType.Tick,1f,Arg0:(int)(tick%int.MaxValue)));}}
