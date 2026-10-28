#!/usr/bin/env python3
"""Compile real guard initialization/IK and skeleton fallback methods with engine fixtures.

This covers the eight missing-symbol failures from the native Unity import. Other
pose layers and material/mesh construction are substitutes, not a Unity build.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile


def method(source, declaration):
    start = source.index(declaration)
    opening = source.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def field(source, name):
    matches = [line for line in source.splitlines()
               if re.match(r'^        private [^()]+;$', line)
               and re.search(r'\b' + name + r'\b', line)]
    if len(matches) != 1:
        raise ValueError('Expected one production field declaration: ' + name)
    return matches[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--source-root', type=Path, help='Optional source tree for baseline controls')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    source = (args.source_root or root) / 'Assets/Alterverse/Scripts'
    authored = (source / 'Visuals/AuthoredHeroPresentation.cs').read_text()
    tank = (source / 'Combat/HotZoneTank.cs').read_text()
    engine = (root / 'Tools/Visuals/Regression/EngineStubs.cs').read_text().split('namespace Alterverse.Characters')[0]
    engine = engine.replace('public class Shader:Object {',
        'public class Shader:Object {public static bool Available=true;public static Shader Find(string name)=>Available?new Shader{name=name}:null;')
    engine = engine.replace('public class Material:Object {',
        'public class Material:Object {public Color Color,Emission;')
    engine = engine.replace('public struct Color {',
        'public struct Color {public static Color black=>new Color(0,0,0);public static Color operator*(Color c,float v)=>new Color(c.r*v,c.g*v,c.b*v,c.a*v);')
    fields = authored[authored.index('private sealed class Binding'):authored.index('public IReadOnlyList<Renderer>')]
    guard = '''using System;using System.Collections.Generic;using UnityEngine;using Alterverse.Characters;
namespace Alterverse.Visuals {public sealed class AuthoredHeroPresentation:MonoBehaviour {
''' + fields + '\n'.join(method(authored, name) for name in (
        'private void Initialize(', 'private ArmChain CaptureArm(', 'private void SolveGuard(')) + '''
private void BuildControls(){}private void Bind(string a,string b,string c=null){}
private void BindWeaponGrip(string s){}private void BuildNyraWeaponDetails(){}private void BuildRigidBlades(){}
public void ConfigureForTest(Transform root,Transform rig)=>Initialize(root,rig);
public void GuardForTest(bool left,Vector3 target,Vector3 hint,float weight)=>SolveGuard(left?leftArm:rightArm,target,hint,weight);
}}
'''
    declarations = '\n'.join(dict.fromkeys(field(tank, name) for name in (
        'team', 'barrelPivot', 'referenceRig', 'skeletonLeftArm',
        'skeletonLeftArmBase', 'skeletonPelvisBaseScale', 'armorMat')))
    skeleton = '''using UnityEngine;using Alterverse.Visuals;
namespace Alterverse.Combat {public sealed class HotZoneTank:MonoBehaviour {
''' + declarations + method(tank, 'private void BuildSkeletonVisuals(') + '''
public int Parts;public Material TeamMaterial=>teamMat;public Transform Muzzle=>barrelPivot;
private Material MakeMaterial(Shader shader,Color color,float metallic,float smooth,Color emission)=>new Material(shader){Color=color,Emission=emission};
private Transform Part(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material,Quaternion rotation)
{Parts++;var t=new GameObject("DF_HotZoneTank_"+name).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;t.localRotation=rotation;return t;}
public void BuildForTest(int value){team=value;BuildSkeletonVisuals();}
}}
'''
    fixtures = '''using UnityEngine;
namespace UnityEngine {public enum PrimitiveType{Cube,Sphere,Cylinder,Capsule}public class Renderer:Component {public string name;}}
namespace Alterverse.Characters {public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight,Bolt7}public class HeroIdentity:MonoBehaviour {public HeroId Hero;}}
namespace Alterverse.Visuals {
public class HeroVisualMotion:MonoBehaviour{}public class SiegeTankAppearance:MonoBehaviour{}public class UmbralMageAppearance:MonoBehaviour{}
public class MeleeBasicAttackAnimation {public static MeleeBasicAttackAnimation For(GameObject actor)=>null;public void InitializeRig(Transform t){}public void EvaluatePose(){}}
public static class HeroLocomotionAnimation {public static void For(GameObject actor,Transform rig){}}
public static class StylizedArtDirection {public static Color ZeroEnergy=>new Color(.2f,.4f,1);public static Color LumenEnergy=>new Color(1,.3f,.1f);}
public class ReferenceMonsterRig {public Transform Muzzle;public static ReferenceMonsterRig Next;public static ReferenceMonsterRig Build(Transform t,string name)=>Next;}
}
'''
    tests = '''using System;using UnityEngine;using Alterverse.Characters;using Alterverse.Visuals;using Alterverse.Combat;
static class Regression {
static int checks;static void Check(bool pass,string label){if(!pass)throw new Exception(label);checks++;}
static Transform Bone(string name,Transform parent,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
static void Near(float a,float b,string label)=>Check(Math.Abs(a-b)<.003f,label);
static void Main(){
foreach(float yaw in new[]{0f,45f,180f})foreach(bool left in new[]{true,false}){
var actor=new GameObject("actor");actor.AddComponent<HeroIdentity>().Hero=HeroId.Veyr;
var model=Bone("model",actor.transform,new Vector3(2,0,-1));model.localRotation=Quaternion.Euler(0,yaw,0);
Bone("Chest",model,new Vector3(0,1.4f,0));Transform hand=null;
foreach(string side in new[]{"L","R"}){float sign=side=="L"?-1:1;var upper=Bone("UpperArm"+side,model,new Vector3(sign*.3f,1.4f,0));var elbow=Bone("Forearm"+side,upper,new Vector3(sign*.2f,-.25f,.1f));var h=Bone("Hand"+side,elbow,new Vector3(sign*.1f,-.2f,.1f));if((side=="L")==left)hand=h;}
var presentation=actor.AddComponent<AuthoredHeroPresentation>();presentation.ConfigureForTest(actor.transform,model);
float direction=left?-1:1;var point=new Vector3(direction*.25f,1.3f,.4f);var hint=new Vector3(direction*.6f,.8f,0);
Vector3 before=hand.position;presentation.GuardForTest(left,point,hint,0);Near(Vector3.Distance(before,hand.position),0,"zero weight preserves hand");
presentation.GuardForTest(left,point,hint,1);Near(Vector3.Distance(hand.position,model.TransformPoint(point)),0,"guard reaches model target on either arm and rotated rig");
Near(Quaternion.Angle(hand.localRotation,Quaternion.identity),0,"guard preserves the calibrated local wrist");
Check(!float.IsNaN(hand.position.x)&&!float.IsNaN(hand.position.y)&&!float.IsNaN(hand.position.z),"finite hand position");
}
foreach(int team in new[]{0,1}){Shader.Available=true;ReferenceMonsterRig.Next=null;var tank=new GameObject("tank").AddComponent<HotZoneTank>();tank.BuildForTest(team);var expected=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;
Check(tank.Parts>40,"fallback constructs complete skeleton");Check(tank.Muzzle!=null,"fallback retains attack socket");
Near(tank.TeamMaterial.Color.r,expected.r,"team red");Near(tank.TeamMaterial.Color.g,expected.g,"team green");Near(tank.TeamMaterial.Color.b,expected.b,"team blue");
Near(tank.TeamMaterial.Emission.r,expected.r*1.2f,"team beacon emission");}
var marker=new GameObject("authored muzzle").transform;ReferenceMonsterRig.Next=new ReferenceMonsterRig{Muzzle=marker};var authored=new GameObject("authored").AddComponent<HotZoneTank>();authored.BuildForTest(0);Check(authored.Parts==0&&authored.Muzzle==marker,"authored rig bypasses procedural geometry");
ReferenceMonsterRig.Next=null;Shader.Available=false;var noShader=new GameObject("headless").AddComponent<HotZoneTank>();noShader.BuildForTest(1);Check(noShader.Parts==0&&noShader.TeamMaterial==null,"missing shader exits without geometry");
Console.WriteLine("PASS "+checks+" guard initialization/IK and skeleton fallback checks. Controlled engine fixtures; no native Unity build.");
}}
'''
    sdk = args.dotnet_root.resolve()
    refs = sorted((sdk / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((sdk / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    with tempfile.TemporaryDirectory(prefix='df-visual-compile-') as directory:
        directory = Path(directory)
        inputs = []
        for name, content in [('Engine', engine), ('Guard', guard), ('Skeleton', skeleton), ('Fixtures', fixtures), ('Regression', tests)]:
            path = directory / (name + '.cs')
            path.write_text(content)
            inputs.append(path)
        output = directory / 'Regression.dll'
        response = directory / 'compile.rsp'
        response.write_text('\n'.join(['-nologo', '-langversion:9', '-nowarn:0649,0169,0414', '-out:' + str(output),
            *['-r:' + str(path) for path in refs.glob('*.dll')], *map(str, inputs)]))
        subprocess.run([str(sdk / 'dotnet'), str(csc), '-noconfig', '@' + str(response)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0',
            'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(sdk / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    main()
