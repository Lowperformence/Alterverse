namespace Alterverse.Onboarding
{
public static class OnboardingDiagnostics
{
public static string CurrentLabel=>OnboardingProgress.Complete?"COMPLETE":OnboardingCopy.Title(OnboardingProgress.Current);public static string CurrentBody=>OnboardingProgress.Complete?"Onboarding complete.":OnboardingCopy.Body(OnboardingProgress.Current);public static int StepNumber=>OnboardingProgress.Complete?6:(int)OnboardingProgress.Current+1;public const int TotalSteps=6;
}
}
