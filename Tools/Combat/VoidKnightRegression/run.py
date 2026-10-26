"""Compile production combat using deterministic Unity fixtures; not a native Unity test."""
from pathlib import Path
import argparse,json,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args();dotnet=a.dotnet_root.resolve()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
names=['Shop/WeaponSkinCatalog.cs','Shop/HeroUnlockCatalog.cs','Characters/PlayerHeroSelection.cs','Characters/HeroIdentity.cs','Characters/HeroAbilityKit.cs','Characters/HeroCombatProfile.cs','Characters/TimmyBalance.cs','Characters/Bolt7Balance.cs','Characters/VoidKnightBalance.cs','Characters/VoidKnightState.cs','Characters/HeroAbilityExecution.cs','Characters/HeroAbilityExecution.CyberShark.cs','Characters/HeroAbilityExecution.Timmy.cs','Characters/HeroAbilityExecution.VoidKnight.cs','Characters/HeroAimPrediction.cs','Characters/PlayerHeroAbilities.cs','Combat/AimingSettings.cs','Combat/AimGeometry.cs','Combat/HeroAttackDelivery.cs','Combat/HeroCombatRules.cs','Combat/HeroCombatStatus.cs','Combat/BasicAttack.cs','Combat/SoftTargeting.cs','Combat/ManualAiming.cs','Multiplayer/CoopGameplayCommands.cs','Audio/HeroCombatAudio.cs','Audio/CombatSoundPalette.cs','UI/HeroAttackCatalog.cs','UI/CombatHudLayout.cs','Visuals/HeroVfxEvents.cs','Visuals/HeroSkillVfx.VoidKnight.cs','Visuals/HeroSkillVfx.Traveler.cs','Visuals/VoidKnightPresentation.cs']
s=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text()
s=s.replace('public class SerializeField:Attribute{}','public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}').replace('public Vector3 position,forward=','public Vector3 localPosition;public Quaternion localRotation=Quaternion.identity;public Vector3 position,forward=')
s=s.replace('public static T Load<T>(string s)where T:class','public static T[] LoadAll<T>(string s)=>new T[0];public static T Load<T>(string s)where T:class').replace('LogError(object x)=>','LogError(object x,Object context=null)=>')
s=s.replace('public static bool CanSimulateHostGameplay=>','public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
s=s.replace('float shield=Math.Min(Shield,n);','n=GetComponent<Alterverse.Characters.VoidKnightState>()?.ReduceFrontalDamage(n,source)??n;float shield=Math.Min(Shield,n);').replace('UnityEngine.GameObject actor=null)=>HeroVfxPool.Emit','UnityEngine.GameObject actor=null,bool warning=false)=>HeroVfxPool.Emit')
s=s.replace('public enum DimensionSound{Kill}','public enum DimensionSound{Kill,Purchase}')
s=s.replace('public static void BroadcastTimmyEffect(','public static void BroadcastVoidKnightEffect(UnityEngine.GameObject actor,int kind,UnityEngine.Vector3 from,UnityEngine.Vector3 to,float radius,float duration){}public static void BroadcastTimmyEffect(')
# Keep the visual dependencies in this combat fixture in sync with production.
# Exercise the real pure math helpers instead of replacing them with no-op solvers.
motion=(src/'Visuals/CombatMotion.cs').read_text()
ease=motion[motion.index('public static float Ease('):motion.index('public static float Curve(')]
solve=motion[motion.index('public static void Solve('):motion.index('public static event ')]
s='using UnityEngine;\n'+s
s=s.replace('public static class CombatMotion {','public static class CombatMotion {'+ease+solve)
s=s.replace('public Vector3 InverseTransformPoint(', 'public Vector3 up=>rotation*Vector3.up;public Vector3 InverseTransformPoint(')
s=s.replace('public static Vector3 ClampMagnitude(', 'public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>n.sqrMagnitude<1e-12f?v:v-n*(Dot(v,n)/n.sqrMagnitude);public static Vector3 ClampMagnitude(')
s=s.replace('public static Quaternion Euler(', 'public static Quaternion FromToRotation(Vector3 a,Vector3 b){if(a.sqrMagnitude<1e-12f||b.sqrMagnitude<1e-12f)return identity;a=a.normalized;b=b.normalized;float d=Mathf.Clamp(Vector3.Dot(a,b),-1,1);if(d>.999999f)return identity;Vector3 axis=Vector3.Cross(a,b);if(d<-.999999f){axis=Vector3.Cross(a,Mathf.Abs(a.y)<.9f?Vector3.up:Vector3.right);return AngleAxis(180,axis);}return AngleAxis(Mathf.Acos(d)*Mathf.Rad2Deg,axis);}public static Quaternion Euler(')
# BOLT-7 combat is exercised by its separate production-code runner.
s+='\nnamespace Alterverse.Characters { public sealed partial class HeroAbilityExecution { private bool TryCastBolt7(int slot,Alterverse.Combat.AimPose aim)=>false; } }\n'
refs=next((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='void-knight-regression-') as tmp:
 tmp=Path(tmp);(tmp/'EngineStubs.cs').write_text(s);out=tmp/'Regression.dll'
 subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest','-out:'+str(out),*[f'-r:{x}' for x in refs.glob('*.dll')],str(tmp/'EngineStubs.cs'),str(here/'ExtraStubs.cs'),str(here/'Regression.cs'),*[str(src/n) for n in names]],check=True)
 (tmp/'Regression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(dotnet/'dotnet'),str(out)],check=True)
 # Exercise the future release with a temporary source copy only. Production stays locked.
 unlock=tmp/'HeroUnlockCatalog.cs'
 original=(src/'Shop/HeroUnlockCatalog.cs').read_text()
 assert 'public const bool VoidKnightReleased=false;' in original
 unlock.write_text(original.replace('public const bool VoidKnightReleased=false;','public const bool VoidKnightReleased=true;'))
 release_out=tmp/'PurchaseRegression.dll'
 release_sources=[str(unlock) if n=='Shop/HeroUnlockCatalog.cs' else str(src/n) for n in names]
 subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest','-out:'+str(release_out),*[f'-r:{x}' for x in refs.glob('*.dll')],str(tmp/'EngineStubs.cs'),str(here/'ExtraStubs.cs'),str(here/'PurchaseRegression.cs'),*release_sources],check=True)
 (tmp/'PurchaseRegression.runtimeconfig.json').write_text((tmp/'Regression.runtimeconfig.json').read_text())
 subprocess.run([str(dotnet/'dotnet'),str(release_out)],check=True)
