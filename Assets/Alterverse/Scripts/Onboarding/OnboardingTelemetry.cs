using UnityEngine;
namespace Alterverse.Onboarding
{
public static class OnboardingTelemetry
{
private const string Prefix="DF_OnboardingSeen_";public static int CompletionCount(OnboardingStep step)=>Alterverse.Profile.AccountProfilePrefs.GetInt(Prefix+(int)step,0);public static void RecordCompletion(OnboardingStep step){string key=Prefix+(int)step;Alterverse.Profile.AccountProfilePrefs.SetInt(key,CompletionCount(step)+1);Alterverse.Profile.AccountProfilePrefs.Save();}public static string DebugSummary(){return $"MOVE {CompletionCount(OnboardingStep.Move)} | ATTACK {CompletionCount(OnboardingStep.Attack)} | ABILITY {CompletionCount(OnboardingStep.Ability)} | FRONT {CompletionCount(OnboardingStep.Front)} | OBJECTIVE {CompletionCount(OnboardingStep.Objective)} | RECALL {CompletionCount(OnboardingStep.Recall)}";}
}
}

