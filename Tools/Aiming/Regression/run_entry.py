#!/usr/bin/env python3
"""Run production skill delivery against deterministic movement/engine fixtures, not Unity."""
import argparse
import ast
import json
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--ranged-cones', action='store_true')
    parser.add_argument('--negative-held-basic', action='store_true', help='Restore send-time basic animation to prove the rejected-hold regression fails')
    parser.add_argument('--aim-jitter', action='store_true', help='Exercise actual HeroMotor facing and manual AOE edge placement')
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    names = ast.literal_eval(re.search(r'^names=(\[[^\n]+\])', (here / 'run.py').read_text(), re.M)[1])
    names += ['Characters/VoidKnightBalance.cs', 'Characters/VoidKnightState.cs',
              'Characters/HeroAbilityExecution.VoidKnight.cs',
              'Characters/HeroAbilityExecution.Bolt7.cs', 'Combat/Bolt7Combat.cs']
    # Extend the shared fixtures for the appended hero. Combat sources stay unchanged.
    stubs = (here / 'EngineStubs.cs').read_text().replace(
        'public class SerializeField:Attribute{}',
        'public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}')
    stubs = stubs.replace('public static bool CanSimulateHostGameplay=>',
                          'public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
    if args.aim_jitter:
        names.append('Characters/HeroMotor.cs')
        stubs = stubs.replace('public class SerializeField:Attribute{}', 'public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}public class SerializeField:Attribute{}')
        # Compile the real facing owner for these checks; native movement remains a fixture.
        stubs = re.sub(r'^ public class HeroMotor:.*\n', '', stubs, flags=re.M)
        stubs = stubs.replace('public class HeroLocomotion:UnityEngine.MonoBehaviour {',
            'public class HeroLocomotion:UnityEngine.MonoBehaviour { public bool CanMove=>true; public float SpeedMultiplier=>1; public UnityEngine.Vector3 LastCollisionNormal; public void FaceMovement(UnityEngine.Vector3 d){}')
        stubs = stubs.replace('public float CooldownMultiplier=1;', 'public float CooldownMultiplier=1,MoveSpeedMultiplier=1;')
        stubs = stubs.replace('public Button qKey=new()', 'public Button aKey=new(),dKey=new(),wKey=new(),sKey=new(),qKey=new()')
        stubs = stubs.replace('public static Vector2 zero=>', 'public Vector2 normalized=>magnitude>0?this/magnitude:zero;public static Vector2 zero=>')
        stubs = stubs.replace('public static Vector3 zero=>', 'public static Vector3 MoveTowards(Vector3 a,Vector3 b,float d){var v=b-a;return v.magnitude<=d?b:a+v.normalized*d;}public static Vector3 ProjectOnPlane(Vector3 v,Vector3 n)=>v-n*Dot(v,n)/Mathf.Max(.000001f,n.sqrMagnitude);public static Vector3 zero=>')
        stubs = stubs.replace('public const float PI=', 'public static float Exp(float x)=>(float)Math.Exp(x);public const float PI=')
        stubs = stubs.replace('public static Quaternion identity=>', 'public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>b;public static Quaternion identity=>')
    with tempfile.TemporaryDirectory(prefix='df-skill-entry-') as directory:
        directory = Path(directory)
        fixtures = directory / 'EngineStubs.cs'
        fixtures.write_text(stubs)
        presentation = directory / 'VoidPresentation.cs'
        presentation.write_text('''namespace Alterverse.Visuals {
public class VoidKnightPresentation:UnityEngine.MonoBehaviour {public void BeginCounter(){}}
public sealed partial class HeroSkillVfx {
 public static void CancelVoidKnightWarnings(UnityEngine.GameObject a){}
 public static void VoidKnightField(UnityEngine.GameObject a,UnityEngine.Vector3 d,float r,float s,bool w,bool dash=false){}
}}
''')
        output = directory / 'Regression.dll'
        tests = ['Regression.cs', 'TimmyRegression.cs', 'TimmyVfxRegression.cs', 'ConnectionTiming.cs',
                 'ConeCaptureRegression.cs', 'MovingConnectionRegression.cs', 'EntryRegression.cs',
                 'RangedConeRegression.cs', 'TemporalBeamRegression.cs']
        if args.aim_jitter:
            tests.append('JitterRegression.cs')
        files = [fixtures, presentation, *[here / n for n in tests],
                 root / 'Assets/Alterverse/Editor/AttackDeliveryRegression.cs',
                 root / 'Assets/Alterverse/Editor/BasicAttackDeliveryRegression.cs',
                 *[root / 'Assets/Alterverse/Scripts' / n for n in names]]
        if args.negative_held_basic:
            original = root / 'Assets/Alterverse/Scripts/Combat/BasicAttack.cs'
            negative = directory / 'BasicAttackNegative.cs'
            content = original.read_text()
            needle = 'nextAttackTime = Time.time + Cooldown;'
            assert content.count(needle) == 2
            negative.write_text(content.replace(needle, needle + '\n                CombatMotion.BeginAimed(gameObject, HeroAttackDelivery.Windup(identity.Hero), pose.Point);', 1))
            files = [negative if file == original else file for file in files]
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-langversion:9',
                        '-define:UNITY_EDITOR', '-main:' + ('JitterEntry' if args.aim_jitter else 'EntryPoint'), '-out:' + str(output),
                        *['-r:' + str(p) for p in refs.glob('*.dll')], *map(str, files)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output), *(['--ranged-cones'] if args.ranged_cones else [])], check=True)


if __name__ == '__main__':
    main()
