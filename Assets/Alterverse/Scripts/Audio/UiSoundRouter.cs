namespace Alterverse.Audio
{
public static class UiSoundRouter
{
public static void Confirm(){if(AudioCuePolicy.Allow("ui-confirm",AudioCuePriority.World))DimensionAudio.Play(DimensionSound.UiConfirm,.42f);}public static void Back(){if(AudioCuePolicy.Allow("ui-back",AudioCuePriority.World))DimensionAudio.Play(DimensionSound.UiBack,.36f);}public static void PurchaseSuccess(){if(AudioCuePolicy.Allow("ui-purchase",AudioCuePriority.Critical)){DimensionAudio.Play(DimensionSound.UiConfirm,.62f,1.08f);DimensionAudio.Play(DimensionSound.FrontShift,.22f,1.18f);}}
}
}
