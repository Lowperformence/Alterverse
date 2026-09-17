using Alterverse.Shop;
namespace Alterverse.Audio
{
public static class RecallSoundDesign
{
public static DimensionSound CompletionCue=>RecallCosmeticEntitlement.Owned?DimensionSound.RealityShatter:DimensionSound.RecallComplete;public static float CompletionGain=>RecallCosmeticEntitlement.Owned?.78f:.58f;public static float ChannelPitch(float progress)=>.92f+.18f*UnityEngine.Mathf.Clamp01(progress);public const string StandardIdentity="controlled dimensional return: rising energy then clean lock";public const string RealityShatterIdentity="fracture both realities, collision crack, premium dimensional collapse";
}
}
