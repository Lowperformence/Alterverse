"""Compile production combat using deterministic Unity fixtures; not a native Unity test."""
from pathlib import Path
import argparse,json,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args();dotnet=a.dotnet_root.resolve()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
names=['Characters/HeroIdentity.cs','Characters/HeroAbilityKit.cs','Characters/HeroCombatProfile.cs','Characters/TimmyBalance.cs','Characters/VoidKnightBalance.cs','Characters/VoidKnightState.cs','Characters/HeroAbilityExecution.cs','Characters/HeroAbilityExecution.CyberShark.cs','Characters/HeroAbilityExecution.Timmy.cs','Characters/HeroAbilityExecution.VoidKnight.cs','Characters/HeroAimPrediction.cs','Characters/PlayerHeroAbilities.cs','Combat/AimingSettings.cs','Combat/AimGeometry.cs','Combat/HeroAttackDelivery.cs','Combat/HeroCombatRules.cs','Combat/HeroCombatStatus.cs','Combat/BasicAttack.cs','Combat/SoftTargeting.cs','Combat/ManualAiming.cs','Multiplayer/CoopGameplayCommands.cs','Audio/HeroCombatAudio.cs','Audio/CombatSoundPalette.cs','UI/HeroAttackCatalog.cs','UI/CombatHudLayout.cs','Visuals/HeroSkillVfx.VoidKnight.cs','Visuals/VoidKnightPresentation.cs']
s=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text()
s=s.replace('public class SerializeField:Attribute{}','public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}').replace('public Vector3 position,forward=','public Vector3 localPosition;public Quaternion localRotation=Quaternion.identity;public Vector3 position,forward=')
s=s.replace('public static T Load<T>(string s)where T:class','public static T[] LoadAll<T>(string s)=>new T[0];public static T Load<T>(string s)where T:class').replace('LogError(object x)=>','LogError(object x,Object context=null)=>')
s=s.replace('public static bool CanSimulateHostGameplay=>','public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
s=s.replace('float shield=Math.Min(Shield,n);','n=GetComponent<Alterverse.Characters.VoidKnightState>()?.ReduceFrontalDamage(n,source)??n;float shield=Math.Min(Shield,n);').replace('UnityEngine.GameObject actor=null)=>HeroVfxPool.Emit','UnityEngine.GameObject actor=null,bool warning=false)=>HeroVfxPool.Emit')
s=s.replace('public static void BroadcastTimmyEffect(','public static void BroadcastVoidKnightEffect(UnityEngine.GameObject actor,int kind,UnityEngine.Vector3 from,UnityEngine.Vector3 to,float radius,float duration){}public static void BroadcastTimmyEffect(')
refs=next((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='void-knight-regression-') as tmp:
 tmp=Path(tmp);(tmp/'EngineStubs.cs').write_text(s);out=tmp/'Regression.dll'
 subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest','-out:'+str(out),*[f'-r:{x}' for x in refs.glob('*.dll')],str(tmp/'EngineStubs.cs'),str(here/'ExtraStubs.cs'),str(here/'Regression.cs'),*[str(src/n) for n in names]],check=True)
 (tmp/'Regression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(dotnet/'dotnet'),str(out)],check=True)
