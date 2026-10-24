#!/usr/bin/env python3
"""Exercise production hero audio bank/routing against controlled native-library/voice fixtures."""
import argparse,ast,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];shared=root/'Tools/Aiming/Regression';dotnet=a.dotnet_root.resolve()
names=ast.literal_eval(re.search(r'^names=(\[[^\n]+\])',(shared/'run.py').read_text(),re.M)[1])
names+=['Characters/VoidKnightBalance.cs','Characters/VoidKnightState.cs','Characters/HeroAbilityExecution.VoidKnight.cs']
engine=(shared/'EngineStubs.cs').read_text()
engine=engine.replace('public class SerializeField:Attribute{}','public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}')
engine=engine.replace('public static bool CanSimulateHostGameplay=>','public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
needle='public static UnityEngine.AudioClip AudioFor(int slot,int phase)=>null;'
assert needle in engine
engine=engine.replace(needle,'public static System.Collections.Generic.Dictionary<int,UnityEngine.AudioClip> NativeClips=new();public static UnityEngine.AudioClip AudioFor(int slot,int phase)=>NativeClips.GetValueOrDefault(slot*3+phase);public static bool Defer;public static System.Action Pending;')
needle='public static bool ScheduleImpactAudio(UnityEngine.GameObject actor,int slot,UnityEngine.Vector3 point,System.Action play)=>false;'
assert needle in engine
engine=engine.replace(needle,'public static bool ScheduleImpactAudio(UnityEngine.GameObject actor,int slot,UnityEngine.Vector3 point,System.Action play){if(!Defer)return false;Pending=play;return true;}')
engine=engine.replace('public static int LastCue=-1,','public static int Plays;public static float LastCooldown;public static int LastCue=-1,')
engine=engine.replace('{LastCue=cue;if(cue==132)','{Plays++;LastCooldown=cooldown;LastCue=cue;if(cue==132)')
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-nova-audio-') as directory:
    d=Path(directory);fixtures=d/'Engine.cs';fixtures.write_text(engine)
    presentation=d/'Presentation.cs';presentation.write_text('namespace Alterverse.Visuals {public class VoidKnightPresentation:UnityEngine.MonoBehaviour {public void BeginCounter(){}}public sealed partial class HeroSkillVfx {public static void CancelVoidKnightWarnings(UnityEngine.GameObject a){}public static void VoidKnightField(UnityEngine.GameObject a,UnityEngine.Vector3 d,float r,float s,bool w,bool dash=false){}}}')
    output=d/'AudioRegression.dll'
    subprocess.run([str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-langversion:latest','-define:UNITY_EDITOR','-main:NovaAudioRegression','-out:'+str(output),*['-r:'+str(x) for x in refs.glob('*.dll')],str(fixtures),str(presentation),str(here/'AudioRegression.cs'),*[str(root/'Assets/Alterverse/Scripts'/n) for n in names]],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(output)],check=True)
