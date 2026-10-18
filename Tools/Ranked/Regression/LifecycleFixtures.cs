// Result storage, Unity drawing and native transport are controlled boundaries in these tests.
using System.Collections;
using UnityEngine;
namespace Alterverse.Match
{
 public enum PauseReason {Result,Settings,Transition}
 public static class MatchClock {public static void Set(PauseReason reason,bool paused){}}
 public class MatchScoreboard {public static MatchScoreboard Instance;public bool ResultCommitted;}
}
namespace Alterverse.UI
{
 public static class OnlineMatchLoadingScreen {public static void Cancel(){}}
 public class CombatHudLayout {public CombatHudLayout(Rect r){}public Matrix4x4 Matrix;public float Width=1280;}
}
namespace Alterverse.Prototype
{
 public class PrototypeBootstrap:MonoBehaviour {public static int Restarts;public static void PrepareTransitionFrame(){}public IEnumerator RestartPrototype(){Restarts++;yield break;}}
}
namespace Alterverse.Multiplayer
{
 public class CoopMatchResultSync {public static CoopMatchResultSync Instance;public bool ResultReady;}
 public static class MatchNetworkProtocol {public static bool Authorized(ulong id,string player)=>true;public static bool ReadRound(ref Unity.Netcode.FastBufferReader reader){reader.ReadValueSafe(out uint round);return round==CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan);}}
}

namespace Alterverse.Combat
{
 public class HotZoneTank:MonoBehaviour {public static int Spawns;public static readonly System.Collections.Generic.List<HotZoneTank> Active=new();public Health TankHealth;public static bool Spawn(int team){Spawns++;return true;}}
 public class LaneMinion:MonoBehaviour {public static readonly System.Collections.Generic.List<LaneMinion> Active=new();}
 public class DimensionPlanet:MonoBehaviour {public void ApplyNetworkAvailability(bool alive){}}
}
namespace Alterverse.Match
{
 public sealed partial class HotZoneDirector:MonoBehaviour
 {
  enum State {Waiting,Active,Finished}
  State state;bool rewardIssued,openingAnnounced;int occurrence,zeroInside,lumenInside;
  float elapsed,nextOpenAt=60,activeElapsed,zeroTime,lumenTime,nextScan;
  DimensionsFront front;
  public int Visuals;public bool IsActive=>state==State.Active;
  public float Remaining=>30-activeElapsed;public float ZeroControlSeconds=>zeroTime;
  public int ZeroInside=>zeroInside;
  Vector3 ChooseSpawnPosition()=>new(3,0,4);
  void BuildZoneVisual(){Visuals++;}void DestroyZoneVisual(float delay){}void RecordWinningParticipants(int team){}
  void ResolveNoReward(){state=State.Finished;}
 }
}
namespace Alterverse.Multiplayer
{
 public sealed partial class CoopWorldEventSync:MonoBehaviour {Unity.Netcode.CustomMessagingManager messaging;}
 public class CoopWorldEntityKey:MonoBehaviour
 {
  public string Key;public void Configure(){}public void ApplyTarget(Vector3 position,float yaw,bool immediate){transform.position=position;}
  public void ApplyAlivePresentation(bool alive){}
 }
}
