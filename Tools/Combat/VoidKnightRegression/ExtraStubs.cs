using UnityEngine;
namespace UnityEngine {public class AnimationClip:Object {public float length=1;public void SampleAnimation(GameObject o,float t){}}}
namespace Alterverse.UI {public class AbilityRangePreviewOverlay:MonoBehaviour{}}
namespace Alterverse.Visuals {
 internal enum HeroFxShape {Ring,Hex,Slash,Rift,Orb,Bolt,Shield,Plus,Shard,Cracks,Halo,Burst,Petals,ShockDome,Runes,Seed,RiftCross,ContactStar,Spark,Streak,ArcRibbon,Leaf,AnchorProjectile,WaterSpiral,WaveCrest,AnchorChain,MonsterRock,MonsterDiamond,MonsterSlime,MonsterBeam,PressureWave,GravityEight,AnchorStrikeZero,Laser,EchoSickle,Mist,Claw,TimmyGrenade,Count}
 internal enum HeroFxMotion {Hold,Expand,Contract,Travel,Orbit,Sweep,Scatter,Spiral,TravelFixedFrame,Drift,Tether,Lob,RadialPulse,LaserBeam,Sustained,AttachedJet}
 public static class HeroVfxPool {
 internal static int BeginDelivery()=>1;internal static void EndDelivery(){}internal static void CancelDelivery(int n){}public static void CancelProjectiles(GameObject a){}public static void CancelWarnings(GameObject a){}
 internal static void Emit(HeroFxShape s,Vector3 f,Vector3 t,Vector3 z,Color c,float d,HeroFxMotion m,Quaternion q,GameObject a,bool warning=false){}
 }
 public sealed partial class HeroSkillVfx {public static void EndTimmyLeap(GameObject a){}public static void TimmyEffect(GameObject a,int kind,Vector3 from,Vector3 to,float radius,float duration){}}
}
namespace Alterverse.Profile
{
 public static class AccountProfilePrefs {public static int GetInt(string key,int fallback)=>1;}
}
namespace Alterverse.Shop
{
 public static class ProfileTransaction
 {
  public static int Recoveries,Commits;public static void Recover(){Recoveries++;}public static void Commit(params Write[] writes){Commits++;}
  public struct Write{public Write(string key,int value){}}
 }
 public static class PlayerWallet
 {
  public static int EarnedBalance=99999,ServerRequests;public static bool CanAfford(int price)=>true;
  public static bool BeginServerPurchase(string sku){ServerRequests++;return true;}
 }
 public class PaidShardCommerce {public static PaidShardCommerce Instance=new();public bool Ready=true;}
}
