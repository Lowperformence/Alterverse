namespace Alterverse.Onboarding
{
public static class OnboardingRules
{
public static bool Expects(OnboardingStep step)=>!OnboardingProgress.Complete&&OnboardingProgress.Current==step;public static bool TryComplete(OnboardingStep step){if(!Expects(step))return false;OnboardingTelemetry.RecordCompletion(step);OnboardingProgress.Mark(step);return true;}public static float Progress01=>(int)OnboardingProgress.Current/(float)OnboardingDiagnostics.TotalSteps;
}
}
