using UnityEngine;
namespace Alterverse.Profile
{
public static class PlayerExperience
{
private const string XpKey="DF_AccountXP";public const int MaxLevel=1000;
public static long TotalXp{get{string raw=Alterverse.Profile.AccountProfilePrefs.GetString(XpKey,"0");if(!long.TryParse(raw,out long value))return 0L;return System.Math.Min(XpForLevel(MaxLevel),System.Math.Max(0L,value));}}
public static int Level=>LevelForXp(TotalXp);
public static long XpIntoLevel=>TotalXp-XpForLevel(Level);
public static long XpToNextLevel=>Level>=MaxLevel?0:XpForLevel(Level+1)-TotalXp;
public static float LevelProgress=>Level>=MaxLevel?1f:(float)XpIntoLevel/(XpForLevel(Level+1)-XpForLevel(Level));
public static long XpForLevel(int level){level=Mathf.Clamp(level,1,MaxLevel);long n=level-1;return 100L*n+2L*n*n+(n*n*n)/250L;}
public static int LevelForXp(long xp){int lo=1,hi=MaxLevel;while(lo<hi){int mid=(lo+hi+1)/2;if(XpForLevel(mid)<=xp)lo=mid;else hi=mid-1;}return lo;}
public static string ProgressionPurpose=>"EXPERIENCE ONLY";public static int CalculateMatchXp(bool won,int kills,int assists,int objectives){int participation=20+Mathf.Clamp(kills,0,20)*2+Mathf.Clamp(assists,0,30)+Mathf.Clamp(objectives,0,10)*3;return Mathf.Clamp(participation+(won?20:0),20,120);}
public static int GrantMatchXp(bool won,int kills,int assists,int objectives){long before=TotalXp;if(before>=XpForLevel(MaxLevel))return 0;int calculated=CalculateMatchXp(won,kills,assists,objectives);long next=System.Math.Min(XpForLevel(MaxLevel),before+calculated);int granted=(int)System.Math.Max(0L,next-before);Alterverse.Profile.AccountProfilePrefs.SetString(XpKey,next.ToString());Alterverse.Profile.AccountProfilePrefs.Save();return granted;}
}
}
