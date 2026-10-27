"""Check serialized vendor references and compile actual wind/client-server configuration with engine fixtures."""
import argparse,re,subprocess,tempfile,json
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[1]
catalog=(root/'Assets/Alterverse/Resources/PlanetWindCatalog.asset').read_text()
vendor=root/'Assets/Vefects/Wind VFX URP/VFX/Particles'
for field,name in [('flows','VFX_Wind_Flows_01'),('dust','VFX_Wind_Dust_Specs_01'),('leaves','VFX_Wind_Debris_Tree_Leaves_01')]:
 text=(vendor/(name+'.prefab')).read_text();meta=(vendor/(name+'.prefab.meta')).read_text();guid=re.search(r'^guid: (\w+)',meta,re.M).group(1)
 match=re.search(r'  '+field+r': \{fileID: (\d+), guid: '+guid+r', type: 3\}',catalog);assert match,(name,'catalog reference')
 assert re.search(r'--- !u!1 &'+match.group(1)+r'\nGameObject:',text),(name,'root gameobject id')
 assert not re.search(r'^--- !u!114 ',text,re.M),(name,'unexpected vendor behaviours')
print('PASS: original vendor prefab GUID/fileID references resolve; no demo behaviours instantiated.',flush=True)
fixture=(here/'Regression/Fixtures.cs').read_text()
def patch(old,new):
 global fixture
 assert old in fixture,old
 fixture=fixture.replace(old,new)
patch('public class Object\n {','public class Object\n {public static Func<GameObject,Transform,GameObject> InstantiateHook;public static GameObject Instantiate(GameObject g,Transform p,bool world)=>InstantiateHook(g,p);')
patch('public class Component:Object {public GameObject gameObject;', 'public class Component:Object {public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public GameObject gameObject;')
patch('public void SetActive(bool value)=>activeSelf=value;', 'public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class=>All.Where(g=>(g==this||g.transform.IsChildOf(transform))&&!g.destroyed).SelectMany(g=>g.components).OfType<T>().ToArray();public void SetActive(bool value)=>activeSelf=value;')
patch('public Transform parent;public Vector3 localPosition;', 'public Transform parent;public Vector3 localPosition;public Vector3 position{get=>parent==null?localPosition:parent.position+localPosition;set=>localPosition=parent==null?value:value-parent.position;}public Vector3 forward;public bool IsChildOf(Transform p){for(var t=parent;t!=null;t=t.parent)if(t==p)return true;return false;}')
patch('public static Vector3 up=>new(0,1,0);', 'public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);public static Vector3 zero=>new();public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 up=>new(0,1,0);')
patch('public static Color white=>new(1,1,1);', 'public static Color Lerp(Color a,Color b,float t)=>new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t);public static Color white=>new(1,1,1);')
patch('public const float PI=(float)Math.PI;', 'public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);public const float PI=(float)Math.PI;')
patch('public static bool Available=true;public static int Loads;', 'public static Dictionary<string,Object> Assets=new();public static bool Available=true;public static int Loads;')
patch('Loads++;return Available?new T():null;', 'Loads++;return Assets.TryGetValue(name,out var asset)?asset as T:Available?new T():null;')
patch('public class EmissionModule {public MinMaxCurve rateOverTime;}', 'public struct Burst{}public class EmissionModule {public bool enabled;public void SetBursts(Burst[] values){}public MinMaxCurve rateOverTime;}')
patch('public class ShapeModule {public bool enabled;', 'public class ShapeModule {public Vector3 position;public bool enabled;')
patch('public bool useAutoRandomSeed,isPlaying,isStopped=true;', 'public class ToggleModule {public bool enabled;}public ToggleModule collision=new(),trigger=new(),subEmitters=new();public bool useAutoRandomSeed,isPlaying,isStopped=true;')
patch('public void Play(){', 'public void Play(bool children=true){')
patch('public class ParticleSystemRenderer:Component {', 'public class ParticleSystemRenderer:Component {public Mesh mesh=new();public bool enabled=true;')
patch('public static float LengthScale=1.25f;', 'public static float LengthScale=1.25f;public const float HalfLength=50.82f,HalfBreadth=36f;')
fixture+='\nnamespace UnityEngine {public class ScriptableObject:Object {} public class Camera:Component {public static Camera main;}public class Mesh:Object {public Bounds bounds=new();}public class Bounds {public Vector3 size=new(1,1,1);}}'
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-authored-wind-') as tmp:
 temp=Path(tmp);engine=temp/'Fixtures.cs';engine.write_text(fixture);output=temp/'Regression.dll';rsp=temp/'compile.rsp';source=root/'Assets/Alterverse/Scripts/Visuals'
 files=[engine,here/'WindRegression.cs',source/'PlanetWindCatalog.cs',source/'WindVfxRuntime.cs',source/'PlanetWeatherRuntime.cs']
 rsp.write_text('\n'.join(['-nologo','-target:exe','-langversion:9','-out:'+str(output),*['-r:'+str(f) for f in runtime.glob('*.dll')],*map(str,files)]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],check=True)
 rsp.write_text(rsp.read_text().replace('-target:exe','-target:library\n-define:UNITY_SERVER').replace(str(output),str(temp/'Server.dll')))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
