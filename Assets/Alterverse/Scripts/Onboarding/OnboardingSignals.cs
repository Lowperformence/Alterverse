namespace Alterverse.Onboarding
{
public static class OnboardingSignals
{
public static void Movement()=>OnboardingRules.TryComplete(OnboardingStep.Move);public static void Attack()=>OnboardingRules.TryComplete(OnboardingStep.Attack);public static void Ability()=>OnboardingRules.TryComplete(OnboardingStep.Ability);public static void FrontInfluence()=>OnboardingRules.TryComplete(OnboardingStep.Front);public static void Objective()=>OnboardingRules.TryComplete(OnboardingStep.Objective);public static void Recall()=>OnboardingRules.TryComplete(OnboardingStep.Recall);
}
}
