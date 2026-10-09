"""Compile unchanged production source sampling/retargeting using real bank data and substitute Unity APIs."""
import argparse
import gzip
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from run import extract_block


def literal(value):
    if isinstance(value, str):
        return json.dumps(value)
    if isinstance(value, int):
        return str(value)
    return format(value, '.9g') + 'f'


def array(kind, values):
    return 'new ' + kind + '[]{' + ','.join(literal(v) for v in values) + '}'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path)
    parser.add_argument('--native-fixtures', action='store_true', help='Use native per-hero geometry, real visual offsets, actor Y=1 and ground=-.02')
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    repo = here.parents[2]
    visuals = repo / 'Assets/Alterverse/Scripts/Visuals'
    resource = repo / 'Assets/Alterverse/Resources/LocomotionAnimation/Library.bytes'
    bank = json.loads(gzip.decompress(resource.read_bytes()))
    # Unity's JSON parser is replaced by fixture field assignment. All actual loaded
    # asset values are preserved; the production loader still validates the fields.
    fixture = ['using System;using System.Reflection;using Alterverse.Visuals;using UnityEngine;class Fixture {',
               'static void Set(object o,string n,object v){o.GetType().GetField(n).SetValue(o,v);}',
               'public static T Decode<T>(string json){object bank=Activator.CreateInstance(typeof(T),true);']
    for field, kind in [('bones', 'string'), ('restPositions', 'float')]:
        fixture.append('Set(bank,' + json.dumps(field) + ',' + array(kind, bank[field]) + ');')
    fixture += ['Set(bank,"version",' + str(bank['version']) + ');',
                'Set(bank,"sourceLegLength",' + literal(bank['sourceLegLength']) + ');',
                'var clips=new LocomotionAnimationLibrary.Clip[' + str(len(bank['clips'])) + '];']
    for index, clip in enumerate(bank['clips']):
        fields = []
        for name, value in clip.items():
            rendered = array('int' if name == 'contacts' else 'float', value) if isinstance(value, list) else literal(value)
            fields.append(name + '=' + rendered)
        fixture.append('clips[' + str(index) + ']=new LocomotionAnimationLibrary.Clip{' + ','.join(fields) + '};')
    fixture += ['Set(bank,"clips",clips);return (T)bank;} }']
    phase_line = next(line.strip() for line in (visuals / 'HeroVisualMotion.cs').read_text().splitlines()
                      if 'if(!GroundingSuspended)phase=' in line)
    fixture.append('class DistancePhase {public float phase;public void Advance(float travel,float strideLength,bool GroundingSuspended){' +
                   phase_line + '}}')
    stubs = (here.parent / 'UpperBodyRegression/EngineStubs.cs').read_text()
    stubs = stubs.replace('public static class Time {', 'public static class Time { public static int frameCount; public static float timeScale=1;')
    stubs = stubs.replace('public const float PI=',
                          'public static float Repeat(float x,float length){return x-(float)Math.Floor(x/length)*length;} '
                          'public static int FloorToInt(float x){return (int)Math.Floor(x);} '
                          'public static int Min(int a,int b){return Math.Min(a,b);} public const float PI=')
    stubs = stubs.replace('public class Component {', 'public class Component { public string name=>gameObject.name;')
    stubs = stubs.replace('public UnityEngine.Quaternion PresentedRotation=',
                          'public UnityEngine.Vector3 PresentedPosition; public UnityEngine.Quaternion PresentedRotation=')
    stubs = stubs.replace('public const float PI=', 'public static float Exp(float x){return (float)Math.Exp(x);} public const float PI=')
    stubs = stubs.replace('public struct Vector3 {', 'public struct Vector3 { public static Vector3 MoveTowards(Vector3 current,Vector3 target,float step){Vector3 d=target-current;float m=d.magnitude;return m<=step||m<.0000001f?target:current+d*(step/m);}')
    stubs = stubs.replace('public class Transform:Component {',
                          'public struct Matrix4x4 {private Transform t;public Matrix4x4(Transform transform){t=transform;}public Vector3 MultiplyVector(Vector3 v){return t.TransformVector(v);}}\n'
                          'public class Transform:Component {public Matrix4x4 localToWorldMatrix=>new Matrix4x4(this); '
                          'public Vector3 lossyScale=>p==null?ls:Vector3.Scale(p.lossyScale,ls);')
    grounded = (visuals / 'HeroGroundedLocomotion.cs').read_text()
    methods = '\n'.join(extract_block(grounded, token) for token in [
        'private sealed class Leg', 'private Leg Capture(', 'private void RestorePelvis(', 'public void ResetContacts(',
        'public void PrepareSourcePelvis(', 'private void OffsetPelvis(', 'private static float SoleExtent(',
        'private void StabilizeAuthoredTarget(',
        'public void FinalizeFeet(', 'private void PlanAuthored(', 'private void FitAuthoredTarget(', 'private void Plan(', 'private static float RequiredDrop(',
        'private void PrepareFoot(', 'private void Solve(', 'public float CycleStride('])
    nominal = next(line.strip() for line in grounded.splitlines() if 'private Vector3 Nominal(' in line)
    contact_harness = '''using UnityEngine;using Alterverse.Visuals;using Alterverse.Combat;
public class GroundedContactHarness:MonoBehaviour {
private HeroVisualMotion motion;private HeroLocomotionAnimation clips;private Leg left,right;
private Transform pelvis;private Vector3 pelvisBefore,pelvisAfter;private bool correctedPelvis,reportedAuthoredContacts,displayedDropReady,terrainOffsetReady;private int appliedFrame=-1,preparedPelvisFrame=-1;private float floorOffset,displayedDrop,terrainPelvisOffset;
public bool Ready=>left!=null&&right!=null;public float LegLength=>(left.length+right.length)*.5f;
public void Setup(Transform[] bones,Transform hipRoot,Transform model,Vector3[][] solePoints){motion=GetComponent<HeroVisualMotion>();clips=GetComponent<HeroLocomotionAnimation>();
left=Capture(bones[0],bones[1],bones[2]);right=Capture(bones[3],bones[4],bones[5]);pelvis=hipRoot;
for(int side=0;side<2;side++){Leg leg=side==0?left:right;leg.solePoints=new Vector3[solePoints[side].Length];float low=float.PositiveInfinity;
for(int i=0;i<solePoints[side].Length;i++){Vector3 world=model.TransformPoint(solePoints[side][i]);low=Mathf.Min(low,world.y);leg.solePoints[i]=leg.foot.InverseTransformPoint(world);}
leg.sole=Mathf.Clamp(leg.foot.position.y-low,.01f,leg.length*.22f);}
floorOffset=Mathf.Min(left.foot.position.y-left.sole,right.foot.position.y-right.sole)-transform.position.y;}

public void RebasePelvis(){RestorePelvis();preparedPelvisFrame=-1;}
public Vector3 Target(bool r){return (r?right:left).target;}
public bool Contact(bool r){return (r?right:left).contact;}
public float Error(bool r){var leg=r?right:left;return Vector3.Distance(leg.foot.position,leg.target);}
private Vector3 Ground(Vector3 p,Leg leg,out Vector3 normal){normal=Vector3.up;p.y=motion.PresentedPosition.y+leg.sole;return p;}
''' + nominal + '\n' + methods + '\n}\n'
    if args.native_fixtures:
        contact_harness = contact_harness.replace('p.y=motion.PresentedPosition.y+leg.sole', 'p.y=-.02f+leg.sole')
    stubs += '''
namespace UnityEngine {
public enum RuntimeInitializeLoadType{SubsystemRegistration}
public class RuntimeInitializeOnLoadMethodAttribute:System.Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
public class TextAsset{public byte[] bytes;}
public static class Resources{public static string BankPath;public static T Load<T>(string path)where T:class{return new TextAsset{bytes=System.IO.File.ReadAllBytes(BankPath)}as T;}}
public static class JsonUtility{public static T FromJson<T>(string text){return Fixture.Decode<T>(text);}}
}
'''
    env = os.environ.copy()
    if args.mono_root:
        root = args.mono_root.resolve()
        runtime = str(root / 'usr/bin/mono-sgen')
        compiler = [runtime, str(root / 'usr/lib/mono/4.5/mcs.exe')]
        env['MONO_PATH'] = str(root / 'usr/lib/mono/4.5')
        env['MONO_CFG_DIR'] = str(root / 'etc')
        reference = str(root / 'usr/lib/mono/4.5/System.Numerics.dll')
    else:
        runtime, mcs = shutil.which('mono'), shutil.which('mcs')
        if not runtime or not mcs:
            parser.error('Mono runtime/compiler missing. Install Mono or pass --mono-root.')
        compiler, reference = [mcs], 'System.Numerics'
    with tempfile.TemporaryDirectory(prefix='alterverse-locomotion-runtime-') as temporary:
        temp = Path(temporary)
        (temp / 'EngineStubs.cs').write_text(stubs)
        (temp / 'Fixture.cs').write_text('\n'.join(fixture))
        (temp / 'Grounded.cs').write_text(contact_harness)
        runtime_source = (here / 'RuntimeRegression.cs').read_text()
        if args.native_fixtures:
            runtime_source = runtime_source.replace('new Vector3(3, 0, -2)', 'new Vector3(3, 1, -2)')
            runtime_source = runtime_source.replace('visual.localScale =', 'visual.localPosition = new Vector3(0,.32f,0); visual.localScale =')
            runtime_source = runtime_source.replace('rig.model.localScale =', 'rig.model.localPosition = new Vector3(0,-.96f,0); rig.model.localScale =')
            runtime_source = runtime_source.replace('File.ReadAllLines(rigPath)', 'File.ReadAllLines(Path.Combine(rigPath,(hero==HeroId.Nerok?\"Rook\":hero.ToString())+\"-rest-rig.csv\"))')
            runtime_source = runtime_source.replace('var rig = new Rig();', '''var points=new[]{new List<Vector3>(),new List<Vector3>()};
foreach(string row in File.ReadAllLines(Path.Combine(rigPath,(hero==HeroId.Nerok?"Rook":hero.ToString())+"-foot-sole.csv"))){
if(row.StartsWith("side,"))continue;var c=row.Split(',');points[int.Parse(c[0])].Add(new Vector3(F(c[1]),F(c[2]),F(c[3])));}
solePoints=new[]{points[0].ToArray(),points[1].ToArray()};var rig = new Rig();''')
            runtime_source = runtime_source.replace('Rig rig = Make(HeroId.Nerok); SetDirection(rig, direction); rig.motion.RunBlend = run;', 'Rig rig = Make(integratedHero); SetDirection(rig, direction); rig.motion.RunBlend = run;')
            runtime_source = runtime_source.replace('static void IntegratedContacts()\n    {', 'static void IntegratedContacts()\n    { foreach(HeroId integratedHero in Enum.GetValues(typeof(HeroId))) {')
            runtime_source = runtime_source.replace('static void DistanceCadence()', '} static void DistanceCadence()')
            runtime_source = runtime_source.replace('Integrated direction=', 'Integrated hero="+integratedHero+" direction=')
            runtime_source = runtime_source.replace('float[] maxPose = new float[2];', 'float[] maxPose = new float[2]; float maxKnee=0f,minKnee=180f,maxSourceFk=0f;')
            runtime_source = runtime_source.replace('Sample(rig); ground.FinalizeFeet(); testedFrames++;', '''Sample(rig);
for(int inspectSide=0;inspectSide<2;inspectSide++){Vector3 sampleOffset,sampleHint;bool sampleContact;Quaternion sampleRotation;
if(rig.animation.TryFootSample(inspectSide==1,out sampleOffset,out sampleHint,out sampleContact,out sampleRotation)){
Vector3 difference=rig.neutralFeet[inspectSide]+(rig.actor.transform.position-new Vector3(3,1,-2))+sampleOffset-bones[inspectSide*3+2].position;
difference.y=0;maxSourceFk=Math.Max(maxSourceFk,difference.magnitude);}}
ground.FinalizeFeet(); testedFrames++;''')
            runtime_source = runtime_source.replace('float error = ground.Error(side == 1);', """var thigh=bones[side*3].position-bones[side*3+1].position;
var calf=bones[side*3+2].position-bones[side*3+1].position;
float flex=180f-(float)(Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(thigh.normalized,calf.normalized))))*180/Math.PI);
maxKnee=Math.Max(maxKnee,flex);minKnee=Math.Min(minKnee,flex);
float error = ground.Error(side == 1);""")
            runtime_source = runtime_source.replace('for (int side = 0; side < 2; side++) Check(maxPose', 'Console.WriteLine("Knee flex="+minKnee.ToString("F1")+".."+maxKnee.ToString("F1")+"deg maxSourceVsFk="+maxSourceFk.ToString("F3")+"m"); for (int side = 0; side < 2; side++) Check(maxPose')

            runtime_source = runtime_source.replace('native Nerok fixture evaluations across eight identity profiles', 'native per-hero fixture evaluations across eight identity profiles (Nerok aliases Rook)')
        (temp / 'RuntimeRegression.cs').write_text(runtime_source)
        files = [temp / 'EngineStubs.cs', temp / 'Fixture.cs', temp / 'Grounded.cs', temp / 'RuntimeRegression.cs', here / 'ContinuityRegression.cs',
                 visuals / 'HeroUpperBodyGait.cs', visuals / 'MeleeBasicAttackAnimation.cs', visuals / 'CombatMotion.cs',
                 visuals / 'LocomotionAnimationLibrary.cs', visuals / 'HeroLocomotionAnimation.cs']
        binary = temp / 'Regression.exe'
        subprocess.run(compiler + ['-langversion:latest', '-r:' + reference, '-out:' + str(binary)] +
                       [str(file) for file in files], env=env, check=True)
        subprocess.run([runtime, str(binary), str(here / 'fixtures') if args.native_fixtures else str(here.parent / 'UpperBodyRegression/nerok-rest-rig.csv'), str(resource), str(here / 'fixtures/Veyr-foot-sole.csv') if args.native_fixtures else str(here / 'nerok-foot-sole.csv')], env=env, check=True)


if __name__ == '__main__':
    main()
