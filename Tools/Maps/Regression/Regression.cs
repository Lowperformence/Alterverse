using System;
using Alterverse.Combat;
using UnityEngine;
static class Regression
{
 static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Near(float actual,float expected,string why)=>Check(MathF.Abs(actual-expected)<.001f,why);
 static float Distance(Vector3 a,Vector3 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.z-b.z)*(a.z-b.z));
 static void Main()
 {
  Near(ArenaLayout.CoreX*2f,79.2f*1.1f,"another ten percent between base centres");
  Near(ArenaLayout.CoreX-ArenaLayout.SpawnX,6.6f,"base-to-spawn spacing preserved");
  Near(ArenaLayout.HealingX-ArenaLayout.SpawnX,1f,"plate-to-spawn spacing preserved");
  Near(ArenaLayout.BunkerX-ArenaLayout.HealingX,2f,"bunker-to-plate spacing preserved");
  Near(ArenaLayout.TrenchX-ArenaLayout.HealingX,3f,"trench-to-plate spacing preserved");
  Near(ArenaLayout.HalfLength*2f,92.4f*1.1f,"floor length");Near(ArenaLayout.HalfBreadth*2f,72f,"unchanged breadth");
  foreach(int teamSize in new[]{4,5})for(int team=0;team<2;team++)for(int slot=0;slot<teamSize;slot++)
  {
   var p=ArenaLayout.HeroSpawn(team,slot,teamSize);var other=ArenaLayout.HeroSpawn(1-team,slot,teamSize);
   Near(p.x,-other.x,"mirrored X");Near(p.z,-other.z,"mirrored Z");
   var heal=ArenaLayout.HealingPosition(team);
   Check(MathF.Abs(p.x)<MathF.Abs(heal.x),"spawn is in front of healing plate");
   Check(Distance(p,heal)<4.5f,"spawn in friendly healing radius");
   Check(GameplayCollision.ContainsRoundedArena(p,40f,33f),"spawn inside movement boundary");
   for(int i=0;i<slot;i++)Check(Distance(p,ArenaLayout.HeroSpawn(team,i,teamSize))>=1.39f,"unique separated slots");
  }
  for(int team=0;team<2;team++)for(int slot=0;slot<4;slot++)
  {
   var offline=Offline.SafeSpawn(team,slot);var online=Online.SafeSpawn(team,team*4+slot);
   Near(offline.x,online.x,"offline/online X parity");Near(offline.z,online.z,"offline/online slot parity");
  }
  Near(ArenaLayout.HealingPlateRadius,2.15f*1.3f,"platform diameter increased by 30 percent");
  Near(ArenaLayout.HealingRadius,4.5f*1.3f,"healing coverage increased by 30 percent");
  Check(ArenaLayout.HealingGuardOffset-ArenaLayout.HealingPlateRadius>1.13f,"guardian clears enlarged plate and hero radius");
  Structures.Build();
  Check(DefenseBunker.Positions.Count==4&&DefenseTrench.Positions.Count==4,"shared builder retains all eight defenses");
  for(int team=0;team<2;team++)for(int flank=0;flank<2;flank++)
  {
   var bunker=DefenseBunker.Positions[team*2+flank];var trench=DefenseTrench.Positions[team*2+flank];
   Check(MathF.Abs(bunker.x)>ArenaLayout.HealingX,"bunker pulled back behind plate");
   Check(MathF.Abs(trench.x)>MathF.Abs(bunker.x),"trench pulled back behind bunker");
   Check(MathF.Abs(trench.x)+3.7f<ArenaLayout.HalfLength,"entire trench remains on map");
   Check(MathF.Abs(bunker.z)>MathF.Abs(trench.z),"outer and inner defensive flanks retained");
   var mirror=DefenseBunker.Positions[(1-team)*2+flank];Near(bunker.x,-mirror.x,"mirrored bunker X");Near(bunker.z,-mirror.z,"mirrored bunker Z");
  }
  Check(ArenaLayout.CoreX+4.75f<ArenaLayout.HalfLength,"complete base deck fits enlarged floor");
  Check(ArenaLayout.HealingX-ArenaLayout.HealingRadius>ArenaLayout.HalfLength*.5f,"entire healing area stays in team-end quarter");
  Check(ArenaLayout.SpawnX>ArenaLayout.HalfLength*.5f,"compact formation stays at team map end");
  Check(ArenaLayout.CoreX-ArenaLayout.HealingX>1.62f+ArenaLayout.HealingPlateRadius,"healing plate clears solid core");
  foreach(var point in DefenseBunker.Positions)
  {
   Check(MathF.Abs(point.x)+2.575f<ArenaLayout.HalfLength,"bunker footprint remains on floor");
   Check(MathF.Abs(point.x)-2.575f>ArenaLayout.HalfLength*.5f,"bunker stays at owning map end");
   Check(MathF.Abs(point.z)+3.225f<ArenaLayout.HalfBreadth,"bunker lateral footprint remains on floor");
  }
  foreach(var point in DefenseTrench.Positions)
  {
   Check(MathF.Abs(point.x)-2.55f>ArenaLayout.HalfLength*.5f,"trench stays at owning map end");
   Check(MathF.Abs(point.z)+3.625f<ArenaLayout.HalfBreadth,"trench lateral footprint remains on floor");
   Check(MathF.Abs(point.z)-3.625f>ArenaLayout.HealingRadius,"trench stays clear of whole healing area");
  }
  // Run the production healing component: every angle/radius on the visible enlarged disc,
  // including its edge, must actually heal on the authoritative peer.
  var zone=new SpawnHealingZone();zone.InitializeTeam(0);zone.transform.position=ArenaLayout.HealingPosition(0);
  UnityEngine.Object.Scene.Clear();
  for(int ring=0;ring<=4;ring++)for(int angle=0;angle<360;angle++)
  {
   float a=angle*MathF.PI/180f;float radius=ArenaLayout.HealingPlateRadius*ring/4f;
   var hp=new Health();hp.transform.position=new Vector3(zone.transform.position.x+MathF.Cos(a)*radius,10f,MathF.Sin(a)*radius);
   Check(zone.ContainsWorldPoint(hp.transform.position),"every point on enlarged plate is functional");
   UnityEngine.Object.Scene.Add(new TeamMember {Team=0,Health=hp});
  }
  var enemy=new Health();enemy.transform.position=zone.transform.position;UnityEngine.Object.Scene.Add(new TeamMember{Team=1,Health=enemy});
  var outside=new Health();outside.transform.position=new Vector3(zone.transform.position.x+zone.Radius+.01f,1,0);UnityEngine.Object.Scene.Add(new TeamMember{Team=0,Health=outside});
  var update=typeof(SpawnHealingZone).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  update.Invoke(zone,null);
  foreach(var item in UnityEngine.Object.Scene)if(item is TeamMember member&&member.Health!=enemy&&member.Health!=outside)Check(member.Health.Received>0,"friendly occupant receives actual healing");
  Near(enemy.Received,0,"hostile heroes are not healed");Near(outside.Received,0,"outside healing area is not healed");
  Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay=false;
  var friendly=((TeamMember)UnityEngine.Object.Scene[0]).Health;float received=friendly.Received;
  update.Invoke(zone,null);Near(friendly.Received,received,"client cannot independently heal");
  var border=GameplayCollision.ClampToRoundedArena(new Vector3(100,1,0),40,33);
  Near(border.x,48.4f,"extended movement/dash/network clamp");Near(border.z,0,"width axis unaffected");
  Near(GameplayCollision.ClampToRoundedArena(new Vector3(0,1,100),40,33).z,33,"unchanged lateral boundary");
  // Corners remain inside the physical rounded floor, including collision radius clearance.
  for(int x=-100;x<=100;x+=2)for(int z=-100;z<=100;z+=2)
  {
   var p=GameplayCollision.ClampToRoundedArena(new Vector3(x,1,z),41,35);
   float dx=MathF.Max(0,MathF.Abs(p.x)-(ArenaLayout.HalfLength-4f));
   float dz=MathF.Max(0,MathF.Abs(p.z)-(ArenaLayout.HalfBreadth-4f));
   Check(dx*dx+dz*dz<=16.001f,"movement stays on rounded floor");
  }
  var mesh=GameplayCollision.CreateRoundedPrismMesh(Vector3.zero,ArenaLayout.HalfLength*2,.2f,ArenaLayout.HalfBreadth*2,4,8);
  float maxX=0,maxZ=0;foreach(var v in mesh.vertices){maxX=MathF.Max(maxX,MathF.Abs(v.x));maxZ=MathF.Max(maxZ,MathF.Abs(v.z));}
  Near(maxX,50.82f,"actual floor mesh X extent");Near(maxZ,36f,"actual floor mesh Z extent");
  var terrain=new Transform();ArenaLayout.StretchTerrain(terrain);ArenaLayout.StretchTerrain(terrain);
  Near(terrain.localScale.x,1.21f,"terrain scaling is idempotent");Near(terrain.localScale.y,1f,"unchanged height");Near(terrain.localScale.z,1f,"unchanged breadth");
  Console.WriteLine("PASS: "+checks+" arena layout checks (4/5 heroes per team, spawn spacing, healing, bounds, floor geometry)");
 }
}
