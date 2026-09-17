using UnityEngine;
namespace Alterverse.Content
{
public static class MatchFlavorDirector
{
private static int swingIndex,objectiveIndex;public static string NextFrontSwing(){string value=MatchCalloutContent.FrontSwing[swingIndex%MatchCalloutContent.FrontSwing.Length];swingIndex++;return value;}public static string NextObjective(){string value=MatchCalloutContent.ObjectiveSpawn[objectiveIndex%MatchCalloutContent.ObjectiveSpawn.Length];objectiveIndex++;return value;}public static string LoadingTip(int seed){if(LoadingTipContent.Tips.Length==0)return "FIGHT FOR YOUR REALITY.";return LoadingTipContent.Tips[Mathf.Abs(seed)%LoadingTipContent.Tips.Length];}public static void Reset(){swingIndex=0;objectiveIndex=0;}
}
}
