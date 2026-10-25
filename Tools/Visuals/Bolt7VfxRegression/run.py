"""Compile production BOLT-7 native-effect importer, scheduler and sound lifecycle using controlled fixtures."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args();sdk=a.dotnet_root.resolve();here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse'
def block(s,token):
 a=s.index(token);b=s.index('{',a);depth=1;i=b+1
 while depth:
  depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[a:i]
fixture=(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs').read_text();math=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text()
for token in ['public struct Vector3','public struct Quaternion','public static class Mathf']:fixture=fixture.replace(block(fixture,token),block(math,token))
fixture=fixture.replace('public static float Max(float a,float b)=>Math.Max(a,b);','public static float Max(params float[] v)=>v.Max();')
fixture=fixture.replace('public Quaternion rotation;','public Quaternion rotation;public Vector3 forward=Vector3.forward;')
fixture=fixture.replace('public static float time;','public static float time,timeScale=1;')
fixture=fixture.replace('public static Color white=>new(1,1,1);','public static Color white=>new(1,1,1);public static Color yellow=>new(1,1,0);')
fixture=fixture.replace('public class AudioClip:Object {}','public class AudioClip:Object {public float length=1;}')
fixture=fixture.replace('public class AudioSource:Component {public AudioClip clip;}', '''public class AudioSource:Component {public AudioClip clip;public float volume=1,pitch=1,spatialBlend=1,time,panStereo;public bool loop,playOnAwake=true,isPlaying;public int Plays,Stops;public void Play(){isPlaying=true;Plays++;}public void Stop(){isPlaying=false;Stops++;}public void Pause(){isPlaying=false;}public void UnPause(){if(Plays>0)isPlaying=true;}}''')
fixture=fixture.replace('public static GameObject Prefab;', 'public static readonly Dictionary<string,AudioClip> AudioClips=new();public static GameObject Prefab;')
fixture=fixture.replace('=>(Prefabs.TryGetValue(path,out var prefab)?prefab:Prefab) as T;', '=>typeof(T)==typeof(AudioClip)?(AudioClips.TryGetValue(path,out var audio)?audio:null) as T:(Prefabs.TryGetValue(path,out var prefab)?prefab:Prefab) as T;')
fixture+='''
namespace UnityEngine {public static class SystemInfo{public static Rendering.GraphicsDeviceType graphicsDeviceType;}public class Camera{public static Camera main;public Vector3 WorldToViewportPoint(Vector3 p)=>new(.5f,.5f,1);}}
namespace UnityEngine.Rendering {public enum GraphicsDeviceType{Vulkan,Null}}
namespace Alterverse.Profile{public static class PlayerProfile{public static float MasterVolume=1,SfxVolume=1;}}
namespace Alterverse.Characters{public enum HeroId{Bolt7,Nyra}public static class HeroCombatProfile{public struct Stats{public float AttackInterval;}public static Stats StatsFor(HeroId h)=>new(){AttackInterval=.62f};}}
namespace Alterverse.Editor{public static class UltimateLightningBuilder{public const string IoneSource="Assets/Epic Toon FX/Prefabs/Combat/Nova/Lightning/NovaLightningPurple.prefab";}}
namespace Alterverse.Visuals {
 public static class HeroVfxEvents{public static float ReplayElapsed;}
 public sealed class Bolt7Visual:UnityEngine.MonoBehaviour{public UnityEngine.Vector3 Gatling=>transform.position;public UnityEngine.Vector3 Launcher=>transform.position;public void Shot(){}public void Skill(int slot){}}
 internal enum HeroFxShape{Halo,Streak,PressureWave,ContactStar,Spark}
 internal enum HeroFxMotion{AttachedJet,Hold,RadialPulse,Contract,Drift}
 internal static class HeroVfxPool{public static int Emits,Traces;public static void Trace(UnityEngine.Vector3 a,UnityEngine.Vector3 b,UnityEngine.Color c,float w,float d){Traces++;}public static void Emit(HeroFxShape s,UnityEngine.Vector3 a,UnityEngine.Vector3 b,UnityEngine.Vector3 z,UnityEngine.Color c,float d,HeroFxMotion m=HeroFxMotion.Hold,UnityEngine.Quaternion r=default,UnityEngine.GameObject owner=null,bool warning=false,UnityEngine.Transform follow=null,bool cancelOnOwnerDeath=false){Emits++;}}
}
'''
importer=(src/'Editor/Bolt7Importer.cs').read_text();a1=importer.index('    // FBX');b1=importer.index('    [InitializeOnLoad]');importer=importer[:a1]+importer[b1:]
arena=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text()).group()
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='bolt7-native-vfx-') as d:
 d=Path(d);(d/'Fixtures.cs').write_text(fixture);(d/'Importer.cs').write_text(importer);(d/'Arena.cs').write_text('namespace Alterverse.Match{'+arena+'}');out=d/'Regression.dll'
 sources=[d/'Fixtures.cs',d/'Importer.cs',d/'Arena.cs',here/'Regression.cs',src/'Scripts/Characters/Bolt7Balance.cs',src/'Scripts/Visuals/Bolt7VfxLibrary.cs',src/'Scripts/Visuals/Bolt7Vfx.cs',src/'Scripts/Visuals/BomberNukeVfx.cs',src/'Editor/BomberNukeBuilder.cs']
 result=subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-nowarn:0649','-define:UNITY_EDITOR','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],*map(str,sources)])
 if result.returncode:raise SystemExit(result.returncode)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(sdk/'dotnet'),str(out)],check=True,cwd=d)
