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
  Near(ArenaLayout.SpawnX*2f,60f*1.1f,"spawn-to-spawn length");
  Near(ArenaLayout.HalfLength*2f,84f*1.1f,"floor length");Near(ArenaLayout.HalfBreadth*2f,72f,"unchanged breadth");
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
  var border=GameplayCollision.ClampToRoundedArena(new Vector3(100,1,0),40,33);
  Near(border.x,44f,"extended movement/dash/network clamp");Near(border.z,0,"width axis unaffected");
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
  Near(maxX,46.2f,"actual floor mesh X extent");Near(maxZ,36f,"actual floor mesh Z extent");
  var terrain=new Transform();ArenaLayout.StretchTerrain(terrain);ArenaLayout.StretchTerrain(terrain);
  Near(terrain.localScale.x,1.1f,"terrain scaling is idempotent");Near(terrain.localScale.y,1f,"unchanged height");Near(terrain.localScale.z,1f,"unchanged breadth");
  Console.WriteLine("PASS: "+checks+" arena layout checks (4/5 heroes per team, spawn spacing, healing, bounds, floor geometry)");
 }
}
