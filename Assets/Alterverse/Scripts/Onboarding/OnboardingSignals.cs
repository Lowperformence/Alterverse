namespace Alterverse.Onboarding
{
public static class OnboardingSignals
{
public static void Movement(){Advance(OnboardingStep.Move);}public static void Attack(){Advance(OnboardingStep.Attack);}public static void Ability(){Advance(OnboardingStep.Ability);}public static void FrontInfluence(){Advance(OnboardingStep.Front);}public static void Objective(){Advance(OnboardingStep.Objective);}public static void Recall(){Advance(OnboardingStep.Recall);}private static void Advance(OnboardingStep step){if(OnboardingProgress.Current==step)OnboardingProgress.Mark(step);}
}
}
