namespace Alterverse.Onboarding
{
public static class OnboardingCopy
{
public static string Title(OnboardingStep step)=>step switch{OnboardingStep.Move=>"MOVE INTO THE FRONT",OnboardingStep.Attack=>"STRIKE",OnboardingStep.Ability=>"BREAK THEIR LINE",OnboardingStep.Front=>"CHANGE REALITY",OnboardingStep.Objective=>"CLAIM THE PLANET",OnboardingStep.Recall=>"RETURN TO BASE",_=>"FRONT READY"};public static string Body(OnboardingStep step)=>step switch{OnboardingStep.Move=>"Use the movement control to enter the contested space.",OnboardingStep.Attack=>"Hold ATTACK near an enemy. Keep moving to control your distance.",OnboardingStep.Ability=>"Hold a skill to inspect its range and effect. Release to cast; drag outside to cancel.",OnboardingStep.Front=>"Kills and objectives push the Dimensions Front. Make your reality occupy more of the arena.",OnboardingStep.Objective=>"Secure a dimension planet to empower your team and influence the Front.",OnboardingStep.Recall=>"Recall when you need to reset. Moving or taking damage interrupts it.",_=>"Fight for your reality."};
}
}
