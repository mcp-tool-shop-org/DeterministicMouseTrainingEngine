namespace MouseTrainer.Domain.Events;
public readonly record struct GameEvent(GameEventType Type,float Intensity=1f,int Arg0=0,int Arg1=0,string? Tag=null);
