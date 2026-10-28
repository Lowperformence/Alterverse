"""Compile the real death-body builder with shipped BOLT data and controlled engine fixtures.
Validates hierarchy, joint setup, skin copying, fade and cleanup; does not emulate PhysX.
"""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args();sdk=a.dotnet_root.resolve();here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
# Reuse the existing model fixture construction before its compiler invocation.
model_runner=here.parent/'Bolt7Regression/run.py';scope={'__file__':str(model_runner)}
exec(compile(model_runner.read_text().split('refs=next(')[0],str(model_runner),'exec'),scope)
s=scope['s']
def replace_block(text,token,replacement):
 start=text.index(token);end=text.index('{',start)+1;depth=1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[:start]+replacement+text[end:]
extras=(here/'Fixtures.cs').read_text()
transform=extras[extras.index(' public class Transform:'):extras.index(' // END TRANSFORM')]
s=replace_block(s,'public class Transform:',transform)
s=s.replace('public static void Destroy(Object o){if(o is GameObject g)g.SetActive(false);}','public static void Destroy(Object o){if(o is GameObject g)g.DestroyForTest();}')
s=s.replace('public GameObject gameObject;','public new string name{get=>gameObject.name;set=>gameObject.name=value;}public GameObject gameObject;')
s=s.replace('public int layer;public bool activeInHierarchy=true;public bool activeSelf=>activeInHierarchy;', 'public UnityEngine.SceneManagement.Scene scene=UnityEngine.SceneManagement.SceneManager.ActiveScene;public int layer;private bool selfActive=true;public bool activeInHierarchy=>selfActive&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);public bool activeSelf=>selfActive;public bool Destroyed;public void DestroyForTest(){if(Destroyed)return;Destroyed=true;foreach(var child in transform.Children.ToArray())child.gameObject.DestroyForTest();SetActive(false);foreach(var c in components)Invoke(c,"OnDestroy");All.Remove(this);}')
s=s.replace('if(activeInHierarchy==v)return;activeInHierarchy=v;','if(selfActive==v)return;selfActive=v;')
s=s.replace('public class CharacterController:Component {','public class CharacterController:Component {public bool enabled=true;public Vector3 velocity;')
s=s.replace('public class CapsuleCollider:Collider {','public class CapsuleCollider:Collider {public int direction;')
s=s.replace('public float distance;}','public float distance;public Vector3 normal,point;}')
s=s.replace('public static class Physics {','public static class Physics {public static PhysicsScene defaultPhysicsScene=>new PhysicsScene(1);public static void IgnoreCollision(Collider a,Collider b){}')
s=s.replace('public static float time,unscaledTime,deltaTime=.016f,timeScale=1;', 'public static float fixedDeltaTime=.02f;public static float time,unscaledTime,deltaTime=.016f,timeScale=1;')
s=s.replace('public static void LogError(object x)=>Console.WriteLine(x);','public static int Errors;public static void LogError(object x,Object context=null){Errors++;Console.WriteLine(x);}')
s=s.replace('public class Renderer:Component {public Material[] sharedMaterials;}','''public class Renderer:Component {
 public bool enabled=true;public Material[] sharedMaterials;public Bounds bounds=>new Bounds(transform.position);
 public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public uint renderingLayerMask;
 private readonly Dictionary<int,MaterialPropertyBlock> blocks=new();
 public void GetPropertyBlock(MaterialPropertyBlock to,int i=-1){to.Clear();if(blocks.TryGetValue(i,out var from))foreach(var v in from.Floats)to.Floats[v.Key]=v.Value;}
 public void SetPropertyBlock(MaterialPropertyBlock from,int i=-1){var copy=new MaterialPropertyBlock();foreach(var v in from.Floats)copy.Floats[v.Key]=v.Value;blocks[i]=copy;}
 }''')
s=s.replace('public class MeshRenderer:Component {','public class MeshRenderer:Renderer {')
s=s.replace('public Bounds(Vector3 p){center=p;}', 'public Vector3 min=>center-extents;public Bounds(Vector3 p){center=p;}')
s=s.replace('public Dictionary<int,float> Floats=new();public Dictionary<int,Color> Colors=new();','public Dictionary<int,float> Floats=new();public Dictionary<int,Color> Colors=new();public bool isEmpty=>Floats.Count==0&&Colors.Count==0;public void Clear(){Floats.Clear();Colors.Clear();}public float GetFloat(int id)=>Floats.TryGetValue(id,out var v)?v:0;')
s=s.replace('public static Quaternion Slerp(', 'public static Quaternion FromToRotation(Vector3 a,Vector3 b){float dot=Vector3.Dot(a.normalized,b.normalized);if(dot>.99999f)return identity;if(dot<-.99999f)return AngleAxis(180,Vector3.right);return AngleAxis(Mathf.Acos(dot)*Mathf.Rad2Deg,Vector3.Cross(a,b));}public static Quaternion Slerp(')
# Transform was inserted into the engine namespace; the remaining physics APIs stand alone.
s+=extras[extras.index(' // END TRANSFORM')+len(' // END TRANSFORM'):]
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='hero-death-') as d:
 d=Path(d);(d/'Fixtures.cs').write_text(s);out=d/'Regression.dll'
 r=subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-nowarn:0649,0067,0108','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],str(d/'Fixtures.cs'),str(here/'Regression.cs'),*[str(src/'Visuals'/n) for n in ['Bolt7Assets.cs','Bolt7Model.cs','Bolt7Visual.cs','HeroDeathBody.cs']]])
 if r.returncode:raise SystemExit(r.returncode)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(sdk/'dotnet'),str(out),str(root/'Assets/Alterverse/Resources')],check=True)
