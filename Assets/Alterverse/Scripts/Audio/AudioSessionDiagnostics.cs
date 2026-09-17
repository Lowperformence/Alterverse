using Alterverse.Performance;
namespace Alterverse.Audio
{
public static class AudioSessionDiagnostics
{
public static string Summary=>$"AUDIO {AudioStressBudget.RecommendedVoices} VOICES | {MobilePerformanceProfile.Current} | FPS {RuntimePerformanceMonitor.SmoothedFps:0}";public static string MixState=>AudioMixState.CombatActive?"COMBAT PRIORITY":"WORLD BALANCED";public static string RecallState=>RecallSoundDesign.RealityShatterIdentity;
}
}
