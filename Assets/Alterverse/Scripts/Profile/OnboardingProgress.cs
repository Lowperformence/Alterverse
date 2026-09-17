using UnityEngine;
namespace Alterverse.Profile
{
public static class OnboardingProgress
{
private const string IntroKey="DF_OnboardingIntro",MovementKey="DF_OnboardingMovement",CombatKey="DF_OnboardingCombat",FrontKey="DF_OnboardingFront";public static bool IntroSeen{get=>Get(IntroKey);set=>Set(IntroKey,value);}public static bool MovementLearned{get=>Get(MovementKey);set=>Set(MovementKey,value);}public static bool CombatLearned{get=>Get(CombatKey);set=>Set(CombatKey,value);}public static bool FrontLearned{get=>Get(FrontKey);set=>Set(FrontKey,value);}public static bool Complete=>IntroSeen&&MovementLearned&&CombatLearned&&FrontLearned;public static float Progress=>(IntroSeen?1:0)+(MovementLearned?1:0)+(CombatLearned?1:0)+(FrontLearned?1:0);private static bool Get(string key)=>PlayerPrefs.GetInt(key,0)==1;private static void Set(string key,bool value){PlayerPrefs.SetInt(key,value?1:0);PlayerPrefs.Save();}public static void Reset(){PlayerPrefs.DeleteKey(IntroKey);PlayerPrefs.DeleteKey(MovementKey);PlayerPrefs.DeleteKey(CombatKey);PlayerPrefs.DeleteKey(FrontKey);PlayerPrefs.Save();}
}
}
