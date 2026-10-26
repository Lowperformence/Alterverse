#!/usr/bin/env python3
"""Execute production reserve/activation/aura import logic with controlled Unity substitutes."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',required=True,type=Path)
a=p.parse_args();dotnet=a.dotnet_root.resolve();here=Path(__file__).resolve().parent
root=here.parents[2];src=root/'Assets/Alterverse'

def method(text,name):
    start=text.rfind('\n',0,text.index(name))+1
    opening=text.index('{',text.index(name));depth=1;end=opening+1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]

main=(src/'Scripts/Combat/HotZoneTank.cs').read_text()
methods='\n'.join(method(main,n) for n in ['public static bool Spawn(', 'public static HotZoneTank EnsureNetworkReplica(', 'private static bool SpawnReward('])
bootstrap=(src/'Scripts/Prototype/PrototypeBootstrap.cs').read_text()
assert bootstrap.index('HotZoneTank.PrepareForMatch(world.transform)')<bootstrap.index('completed=true;')
assert 'if(!HotZoneTank.FinishMatchPreparation())return;' in bootstrap
assert 'health.IsDead&&pooledReward' in main
sync=(src/'Scripts/Multiplayer/CoopWorldStateSync.cs').read_text()
assert 'entity.Key==id&&!HotZoneTank.ReturnToRewardPool(entity.gameObject)' in sync
diamond=(src/'Scripts/Visuals/DiamondCoreGuardianWorldVisual.cs').read_text()
assert '!tank.IsReservedReward&&(' in diamond

engine=(root/'Tools/SpaceBomber/NukeRegression/Fixtures.cs').read_text()
engine=engine.replace('public class MonoBehaviour:Component {}','''public class Behaviour:Component
    {
        private bool state=true;
        public bool enabled {get=>state;set {bool changed=state!=value;state=value;if(changed&&gameObject!=null&&gameObject.activeInHierarchy)GameObject.Invoke(this,value?"OnEnable":"OnDisable");}}
        public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
    }
    public class MonoBehaviour:Behaviour {}''')
engine=engine.replace('Components.Add(c);return c;','Components.Add(c);GameObject.Invoke(c,"Awake");if(c is Behaviour b&&b.isActiveAndEnabled)GameObject.Invoke(c,"OnEnable");return c;')
start=engine.index('        public void SetActive(bool value)');end=engine.index('        public static void Invoke',start)
engine=engine[:start]+'''        public void SetActive(bool value)
        {
            bool before=activeInHierarchy;activeSelf=value;
            if(before==activeInHierarchy)return;
            foreach(var c in GetComponentsInChildren<Behaviour>(true))if(c.enabled)Invoke(c,activeInHierarchy?"OnEnable":"OnDisable");
            if(value)foreach(var ps in GetComponentsInChildren<ParticleSystem>())if(ps.main.playOnAwake)ps.Play(false);
        }
        public static GameObject Find(string name)=>All.LastOrDefault(g=>!g.Destroyed&&g.activeInHierarchy&&g.name==name);
'''+engine[end:]
engine=engine.replace('public class Transform:Component','public class Transform:Component')
engine=engine.replace('public Vector3 position,localPosition;', 'public Vector3 position,localPosition;')
pos=engine.index('{',engine.index('public sealed class Transform:Component'))+1
engine=engine[:pos]+'''public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
        public Vector3 eulerAngles;
'''+engine[pos:]

engine=engine.replace('public struct Bounds {public Vector3 center,extents;}','public struct Bounds {public Vector3 center,extents;public void Encapsulate(Bounds b){extents=new(Math.Max(extents.x,b.extents.x),Math.Max(extents.y,b.extents.y),Math.Max(extents.z,b.extents.z));}}')
engine=engine.replace('public static float time;','public static float time,unscaledTime;')
engine=engine.replace('public static class Mathf {','public static class Mathf {public static int RoundToInt(float value)=>(int)MathF.Round(value);public static int Clamp(int v,int lo,int hi)=>Math.Clamp(v,lo,hi);public static float Clamp(float v,float lo,float hi)=>Math.Clamp(v,lo,hi);')
engine=engine.replace('public class Collider:Component {}','public class Collider:Component {public bool enabled=true;}')
engine=engine.replace('public class RuntimeInitializeOnLoadMethodAttribute:Attribute','public class SerializeField:Attribute {}public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int order){}}public class RuntimeInitializeOnLoadMethodAttribute:Attribute')
engine=engine.replace('public class Health:UnityEngine.MonoBehaviour {public bool IsDead;}','''public class Health:UnityEngine.MonoBehaviour
    {public static readonly List<Health> Active=new();public bool IsDead,DestroyOnDeath;public float Current;
    public void Configure(float max,bool destroy){IsDead=false;Current=max;DestroyOnDeath=destroy;}
    private void OnEnable(){if(!Active.Contains(this))Active.Add(this);}private void OnDisable()=>Active.Remove(this);}''')
engine=engine.replace('public static class Resources {','public static class Resources {public static int Loads;')
load_method='public static T Load<T>(string path)where T:class=>(Prefabs.TryGetValue(path,out var prefab)?prefab:Prefab) as T;'
assert load_method in engine
engine=engine.replace(load_method,'public static T Load<T>(string path)where T:class{Loads++;return (Prefabs.TryGetValue(path,out var prefab)?prefab:Prefab) as T;}')
refs=sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc=sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-hotzone-spawn-') as directory:
    d=Path(directory);fixture=d/'Engine.cs';fixture.write_text(engine)
    enum=re.search(r'public enum ArenaVariant\s*\{[^}]+\}',(src/'Scripts/Match/ArenaVariantRuntime.cs').read_text())[0]
    bridge=d/'Bridge.cs';bridge.write_text('using UnityEngine; namespace Alterverse.Match {'+enum+'}\nnamespace Alterverse.Combat { public sealed partial class HotZoneTank { '+methods+' }}')
    output=d/'Regression.dll'
    inputs=[fixture,bridge,here/'Fixtures.cs',here/'Regression.cs',src/'Scripts/Combat/HotZoneTank.Pooling.cs',src/'Scripts/Visuals/HotZoneSpawnAura.cs',src/'Editor/HotZoneSpawnAuraBuilder.cs',src/'Editor/BomberNukeBuilder.cs',src/'Scripts/Visuals/BomberNukeVfx.cs',src/'Scripts/Multiplayer/HotZoneRewardKey.cs',src/'Scripts/Multiplayer/CoopWorldEntityKey.cs']
    command=[str(dotnet/'dotnet'),str(csc),'-nologo','-noconfig','-nowarn:0414','-langversion:latest',*['-r:'+str(x) for x in refs.glob('*.dll')],*[str(x) for x in inputs]]
    subprocess.run([*command,'-define:UNITY_EDITOR','-out:'+str(output)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(dotnet/'dotnet'),str(output)],cwd=d,check=True)
    subprocess.run([*command,'-define:UNITY_EDITOR,UNITY_SERVER','-target:library','-out:'+str(d/'Server.dll')],check=True)

