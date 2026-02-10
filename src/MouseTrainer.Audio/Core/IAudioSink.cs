namespace MouseTrainer.Audio.Core;
public interface IAudioSink{void PlayOneShot(in AudioCue cue);void StartLoop(in AudioCue cue,string loopKey);void StopLoop(string loopKey);}
