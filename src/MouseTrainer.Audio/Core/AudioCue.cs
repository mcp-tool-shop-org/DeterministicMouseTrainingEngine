namespace MouseTrainer.Audio.Core;
public readonly record struct AudioCue(string AssetName,float Volume=1f,float Pitch=1f,bool Loop=false);
