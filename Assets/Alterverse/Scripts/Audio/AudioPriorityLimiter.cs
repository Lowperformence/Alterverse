using System.Collections.Generic;using UnityEngine;
namespace Alterverse.Audio
{
public static class AudioPriorityLimiter
{
private static readonly Dictionary<string,float> LastPlayed=new();public static bool Allow(string channel,float minimumInterval){float now=Time.unscaledTime;if(LastPlayed.TryGetValue(channel,out float last)&&now-last<minimumInterval)return false;LastPlayed[channel]=now;return true;}public static void Reset()=>LastPlayed.Clear();
}
}
