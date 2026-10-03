using UnityEngine;
namespace Alterverse.Onboarding
{
public static class OnboardingReset
{
private const string Key="DF_OnboardingStep";public static void DevelopmentReset(){Alterverse.Profile.AccountProfilePrefs.DeleteKey(Key);Alterverse.Profile.AccountProfilePrefs.Save();}
}
}

