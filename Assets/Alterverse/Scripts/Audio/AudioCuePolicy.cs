namespace Alterverse.Audio
{
public enum AudioCuePriority{Ambient=0,World=1,Combat=2,Critical=3}
public static class AudioCuePolicy
{
public static float MinimumInterval(AudioCuePriority priority)=>priority switch{AudioCuePriority.Critical=>.02f,AudioCuePriority.Combat=>.04f,AudioCuePriority.World=>.18f,_=>.35f};public static bool Allow(string channel,AudioCuePriority priority)=>AudioPriorityLimiter.Allow(channel,MinimumInterval(priority));
}
}
