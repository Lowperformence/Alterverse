using UnityEngine;
namespace Alterverse.Onboarding
{
public enum OnboardingStep{Move=0,Attack=1,Ability=2,Front=3,Objective=4,Recall=5,Complete=6}
public static class OnboardingProgress
{
private const string Key="DF_OnboardingStep";public static OnboardingStep Current=>(OnboardingStep)Mathf.Clamp(PlayerPrefs.GetInt(Key,0),0,(int)OnboardingStep.Complete);public static bool Complete=>Current==OnboardingStep.Complete;public static void Mark(OnboardingStep step){if((int)step<(int)Current)return;int next=Mathf.Min((int)OnboardingStep.Complete,(int)step+1);PlayerPrefs.SetInt(Key,next);PlayerPrefs.Save();}public static void Skip(){PlayerPrefs.SetInt(Key,(int)OnboardingStep.Complete);PlayerPrefs.Save();}
}
}
