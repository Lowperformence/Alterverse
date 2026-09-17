namespace Alterverse.Audio
{
public static class AudioQualityRules
{
public static bool NeverCull(DimensionSound sound)=>sound==DimensionSound.HitHeavy||sound==DimensionSound.RecallComplete||sound==DimensionSound.RealityShatter||sound==DimensionSound.Overtime||sound==DimensionSound.SuddenDeath||sound==DimensionSound.Victory||sound==DimensionSound.Defeat;public static bool IsDecorative(DimensionSound sound)=>sound==DimensionSound.FrontShift;public const string Rule="Preserve player-critical feedback before decorative world audio.";
}
}
