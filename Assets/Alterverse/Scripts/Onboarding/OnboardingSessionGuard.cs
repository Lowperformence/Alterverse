using Alterverse.Characters;using UnityEngine;
namespace Alterverse.Onboarding
{
public static class OnboardingSessionGuard
{
public static bool IsLocalPlayer(Component source){if(source==null)return false;return source.GetComponent<HeroRespawn>()!=null||source.GetComponentInParent<HeroRespawn>()!=null;}public static void Attack(Component source){if(IsLocalPlayer(source))OnboardingSignals.Attack();}public static void Ability(Component source){if(IsLocalPlayer(source))OnboardingSignals.Ability();}public static void Recall(Component source){if(IsLocalPlayer(source))OnboardingSignals.Recall();}
}
}
