#!/usr/bin/env python3
"""Exercise Timmy's production attacks, HeroMotor and HeroLocomotion together.

Unity physics/rendering and NGO transport remain deterministic substitutes.
Unlike the aiming harness, dashes and attack movement are not mocked here.
"""
import argparse
import os
import subprocess
import tempfile
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--mono-root', type=Path, required=True)
p.add_argument('--roslyn', type=Path, required=True)
a = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
src = root / 'Assets/Alterverse/Scripts'
engine = (root / 'Tools/Aiming/Regression/EngineStubs.cs').read_text()
start = engine.index(' public class HeroMotor:')
end = engine.index(' public class VeyrAbilities:', start)
engine = engine[:start] + engine[end:]
engine = engine.replace('public class SerializeField:Attribute{}', 'public class SerializeField:Attribute{} public class HeaderAttribute:Attribute{public HeaderAttribute(string s){}}')
engine = engine.replace('public Vector3 position,forward=Vector3.forward,right=Vector3.right,localScale=Vector3.one;', 'public Vector3 position,localScale=Vector3.one;public Vector3 forward{get=>rotation*Vector3.forward;set=>rotation=Quaternion.LookRotation(value);}public Vector3 right=>rotation*Vector3.right;')
engine = engine.replace('public Quaternion rotation=Quaternion.identity;public Transform parent;', 'public Quaternion rotation=Quaternion.identity;public Transform parent;public Vector3 lossyScale=>localScale;public Vector3 up=>Vector3.up;')
engine = engine.replace('public float sqrMagnitude=>x*x+y*y;', 'public Vector2 normalized=>magnitude>.000001f?this/magnitude:zero;public float sqrMagnitude=>x*x+y*y;')
engine = engine.replace('public static Vector3 Lerp(Vector3 a,Vector3 b,float t)', 'public static Vector3 MoveTowards(Vector3 a,Vector3 b,float d)=>a+ClampMagnitude(b-a,d);public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n)=>a-n*Dot(a,n);public static Vector3 Lerp(Vector3 a,Vector3 b,float t)')
engine = engine.replace('public static Quaternion Inverse(Quaternion q)', 'public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>b;public static Quaternion RotateTowards(Quaternion a,Quaternion b,float d)=>b;public static Quaternion Inverse(Quaternion q)')
engine = engine.replace('public static float Max(float a,float b)', 'public static float Exp(float x)=>(float)Math.Exp(x);public static float Max(float a,float b)')
engine = engine.replace('public struct Bounds {public Vector3 center;public Bounds(Vector3 p){center=p;}}', 'public struct Bounds {public Vector3 center,size;public Vector3 min=>center-size*.5f;public Vector3 max=>center+size*.5f;public Bounds(Vector3 p){center=p;size=Vector3.one;}public void Expand(Vector3 v){size+=v;}}')
engine = engine.replace('public Collider collider;public float distance;', 'public Collider collider;public float distance;public Vector3 normal;')
engine = engine.replace('public class CharacterController:Component {public float height,radius;public UnityEngine.Vector3 center;}', '''public class CharacterController:Collider {public float height=2,radius=.4f,skinWidth=.08f,minMoveDistance,stepOffset;public Vector3 center;public void Move(Vector3 delta){delta.y=0;transform.position+=delta;}}''')
engine = engine.replace('public static void SyncTransforms(){}', '''public static void SyncTransforms(){}
 public static int CapsuleCastNonAlloc(Vector3 top,Vector3 bottom,float radius,Vector3 direction,RaycastHit[] hits,float distance,int mask,QueryTriggerInteraction q){
  int n=SphereCastNonAlloc((top+bottom)*.5f,radius,direction,hits,distance,mask,q);
  for(int i=0;i<n;i++)hits[i].normal=hits[i].collider.gameObject.layer==10?Vector3.up:-direction;
  return n;
 }
 public static RaycastHit[] CapsuleCastAll(Vector3 top,Vector3 bottom,float radius,Vector3 direction,float distance,int mask,QueryTriggerInteraction q){var hits=new RaycastHit[1024];int n=CapsuleCastNonAlloc(top,bottom,radius,direction,hits,distance,mask,q);return hits.Take(n).ToArray();}
''')
engine = engine.replace('public static void Advance(float dt){Time.deltaTime=dt;Time.time+=dt;Time.unscaledTime+=dt;Time.frameCount++;foreach', 'public static void BeginFrame(float dt){Time.deltaTime=dt;Time.time+=dt;Time.unscaledTime+=dt;Time.frameCount++;}public static void Advance(float dt){BeginFrame(dt);Resume();}public static void Resume(){foreach')
engine = engine.replace('public Button qKey=new(),', 'public Button aKey=new(),sKey=new(),dKey=new(),wKey=new(),qKey=new(),')
engine = engine.replace('public int Team;}public class LaneTower', 'public int Team;public float RunningSpeed=6.5f;}public class TrenchSoldier:UnityEngine.MonoBehaviour{}public class LaneTower')
engine = engine.replace('Structures=9,SightMask=', 'Structures=9,MovementMask=(1<<0)|(1<<Structures)|(1<<10),SightMask=')
engine = engine.replace('public float CooldownMultiplier=1;', 'public float CooldownMultiplier=1,MoveSpeedMultiplier=1;')
engine = engine.replace('public static UnityEngine.Vector3 ClampToRoundedArena', 'public static bool ContainsRoundedArena(UnityEngine.Vector3 p,float a,float b)=>Math.Abs(p.x)<=a&&Math.Abs(p.z)<=b;public static UnityEngine.Vector3 ClampToRoundedArena')

names = [
    'Visuals/HeroVfxPool.cs', 'Visuals/HeroSkillVfx.Timmy.cs',
    'Audio/HeroCombatAudio.cs', 'Audio/CombatSoundPalette.cs',
    'Combat/AimingSettings.cs', 'Combat/AimGeometry.cs', 'Combat/ManualAiming.cs',
    'Combat/HeroAttackDelivery.cs', 'Combat/HeroCombatRules.cs',
    'Combat/HeroCombatStatus.cs', 'Combat/SoftTargeting.cs', 'Combat/BasicAttack.cs',
    'Characters/HeroAbilityKit.cs', 'Characters/HeroIdentity.cs',
    'Characters/HeroCombatProfile.cs', 'Characters/TimmyBalance.cs',
    'Characters/HeroAimPrediction.cs', 'Characters/HeroAbilityExecution.cs',
    'Characters/HeroAbilityExecution.CyberShark.cs',
    'Characters/HeroAbilityExecution.Timmy.cs', 'Characters/PlayerHeroAbilities.cs',
    'Characters/HeroMotor.cs', 'Characters/HeroLocomotion.cs',
    'Multiplayer/CoopGameplayCommands.cs', 'UI/CombatHudLayout.cs',
    'UI/PrototypeHUD.cs', 'UI/HeroAttackCatalog.cs', 'UI/AbilityRangePreviewOverlay.cs',
]
env = dict(os.environ,
    MONO_PATH=str(a.mono_root/'usr/lib/mono/4.5')+':'+str(a.mono_root/'usr/lib/mono/4.5/Facades'),
    MONO_CFG_DIR=str(a.mono_root/'etc'),
    LD_LIBRARY_PATH=str(a.mono_root/'usr/lib')+':'+str(a.mono_root/'usr/lib/x86_64-linux-gnu'))
mono = a.mono_root / 'usr/bin/mono-sgen'
with tempfile.TemporaryDirectory(prefix='df-timmy-movement-') as d:
    d = Path(d)
    (d/'Engine.cs').write_text(engine)
    binary = d/'Regression.exe'
    subprocess.run([str(mono), str(a.roslyn), '-nologo', '-noconfig', '-langversion:9',
        '-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.dll'),
        '-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.Core.dll'), '-out:'+str(binary),
        str(d/'Engine.cs'), str(here/'Regression.cs'), str(here/'BasicMovementRegression.cs'), *[str(src/n) for n in names]],
        env=env, check=True)
    subprocess.run([str(mono), str(binary)], env=env, check=True)
