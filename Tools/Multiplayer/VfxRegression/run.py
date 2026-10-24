#!/usr/bin/env python3
"""Exercise the real common VFX facade, pool, semantic events and network transport.
Unity/GPU/time/physics are deterministic substitutes; this is not a native build.
"""
import argparse
import ast
import json
from pathlib import Path
import re
import subprocess
import tempfile


def remove_class(text, declaration):
    start = text.index(declaration)
    brace = text.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[:start] + text[end:]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--negative-control', action='store_true', help='Temporarily omit the imported layer to prove the action checks detect it')
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    shared = root / 'Tools/Aiming/Regression'
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    names = ast.literal_eval(re.search(r'^names=(\[[^\n]+\])', (shared / 'run.py').read_text(), re.M)[1])
    names += ['Visuals/HeroSkillVfx.cs', 'Visuals/HeroSkillVfx.AttackPolish.cs', 'Visuals/MonsterAttackVfx.cs', 'Visuals/PrototypeVfx.cs', 'Visuals/BomberExplosionVfx.cs', 'Visuals/HeroSkillVfx.VoidKnight.cs',
              'Characters/VoidKnightBalance.cs', 'Characters/VoidKnightState.cs',
              'Characters/HeroAbilityExecution.VoidKnight.cs', 'Multiplayer/CoopHeroStateSync.Vfx.cs', 'Characters/HeroLocomotion.cs', 'Visuals/HeroPresentationBuffer.cs', 'UI/TargetIndicatorHUD.cs']
    names += ['Visuals/HeroSkillVfx.NerokOriginal.cs', 'Visuals/EpicVfxLibrary.cs', 'Visuals/HeroVfxPool.EpicToon.cs', 'Visuals/HeroAbilityReadabilityBoost.cs', 'Visuals/HeroAttackReadabilityPresentation.cs']
    engine = (shared / 'EngineStubs.cs').read_text()
    engine = remove_class(engine, 'public sealed partial class HeroSkillVfx:')
    engine = remove_class(engine, 'public static class CoopHeroStateSync ')
    engine = remove_class(engine, 'public class HeroLocomotion:')
    engine = remove_class(engine, 'public class HeroAbilityReadabilityBoost:')
    engine = remove_class(engine, 'public class HeroAttackReadabilityPresentation:')
    engine = engine.replace('public struct Quaternion {', 'public struct Quaternion {public static Quaternion Slerp(Quaternion a,Quaternion b,float t){var q=System.Numerics.Quaternion.Slerp(new(a.x,a.y,a.z,a.w),new(b.x,b.y,b.z,b.w),t);return new Quaternion{x=q.X,y=q.Y,z=q.Z,w=q.W};}')
    engine = engine.replace('public class GUIStyle {', 'public enum FontStyle{Bold}public class GUIStyleState{public Color textColor;}public class GUIStyle {public object font;public FontStyle fontStyle;public GUIStyleState normal=new();')
    engine = engine.replace('public static class GUI {', 'public static class GUI {public static void Label(Rect r,string text,GUIStyle style){}')
    engine = engine.replace('public static class StylizedArtDirection {', 'public static class StylizedArtDirection {public static UnityEngine.Color LumenEnergy=>UnityEngine.Color.white;public static UnityEngine.Color WarmAccent=>UnityEngine.Color.white;')
    engine = engine.replace('public enum DimensionSound{Kill}', 'public enum DimensionSound{Kill,HeroDeath,Shield,MechGun}')
    engine = engine.replace('public static class DimensionAudio {', 'public static class DimensionAudio {public static void PlayAt(DimensionSound sound,UnityEngine.Vector3 p,float gain,float elapsed=0){}')

    engine = engine.replace('public class HeroMotor:UnityEngine.MonoBehaviour {', 'public class HeroMotor:UnityEngine.MonoBehaviour {public float MoveSpeed=>6.5f;')
    engine = engine.replace('public class LaneMinion:UnityEngine.MonoBehaviour {', 'public class LaneMinion:UnityEngine.MonoBehaviour {public float RunningSpeed=>6.5f;')
    engine = engine.replace('public float CooldownMultiplier=1;', 'public float CooldownMultiplier=1,MoveSpeedMultiplier=1;')
    engine = engine.replace('public const int Units=8,', 'public static bool ContainsRoundedArena(UnityEngine.Vector3 p,float a,float b)=>Math.Abs(p.x)<=a&&Math.Abs(p.z)<=b;public const int MovementMask=1,Units=8,')
    engine = engine.replace('public class CharacterController:Component {', 'public class CharacterController:Component {public bool enabled=true;public float minMoveDistance,stepOffset,skinWidth=.01f;public void Move(Vector3 p){transform.position+=p;}')
    engine = engine.replace('public class Transform:Component {', 'public class Transform:Component {public Vector3 lossyScale=>localScale;public Vector3 up=>Vector3.up;')
    engine = engine.replace('public Vector3 TransformPoint(Vector3 p)=>position+rotation*p;', 'public Vector3 TransformPoint(Vector3 p)=>position+rotation*new Vector3(p.x*localScale.x,p.y*localScale.y,p.z*localScale.z);')
    engine = engine.replace('public struct Quaternion {', 'public struct Quaternion {public static Quaternion RotateTowards(Quaternion a,Quaternion b,float d)=>b;')
    engine = engine.replace('public struct Vector3 {', 'public struct Vector3 {public static Vector3 ProjectOnPlane(Vector3 p,Vector3 n)=>p-n*Dot(p,n);')
    engine = engine.replace('public Collider collider;public float distance;', 'public Vector3 normal;public Collider collider;public float distance;')
    engine = engine.replace('public struct Bounds {', 'public struct Bounds {public Vector3 min=>center-Vector3.one;public Vector3 max=>center+Vector3.one;public void Expand(Vector3 p){}')
    engine = engine.replace('public static class Physics {', 'public static class Physics {public static int CapsuleCastNonAlloc(Vector3 top,Vector3 bottom,float radius,Vector3 direction,RaycastHit[] hits,float distance,int mask,QueryTriggerInteraction q){int n=SphereCastNonAlloc((top+bottom)*.5f,radius,direction,hits,distance,mask,q);for(int i=0;i<n;i++)hits[i].normal=-direction;return n;}public static RaycastHit[] CapsuleCastAll(Vector3 top,Vector3 bottom,float radius,Vector3 direction,float distance,int mask,QueryTriggerInteraction q){var hits=new RaycastHit[1024];int n=CapsuleCastNonAlloc(top,bottom,radius,direction,hits,distance,mask,q);return hits.Take(n).ToArray();}')
    engine = engine.replace('public static class Debug {', 'public static class Debug {public static int ErrorCount;')
    engine = engine.replace('public static void LogError(object x)=>Console.WriteLine(x);', 'public static void LogError(object x){ErrorCount++;}')
    engine = engine.replace('public class GameObject:Object {', 'public class GameObject:Object {public bool TryGetComponent<T>(out T value) where T:class {value=GetComponent<T>();return value!=null;}public T GetComponentInParent<T>()where T:class=>GetComponent<T>();')
    engine = engine.replace('public struct Color {', 'public struct Color {public static Color operator *(Color a,Color b)=>new(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);')
    engine = engine.replace('public class SerializeField:Attribute{}', 'public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}')
    engine = engine.replace('public static bool CanSimulateHostGameplay=>', 'public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
    engine = engine.replace('public float DashSeconds;', 'public int AbilityPulses;public float Seeked;public void SeekAction(float seconds){Seeked=seconds;}public void PulseBasic(float w){}public void PulseAttack(bool skill){}public void Empower(float d){}public float DashSeconds;')
    engine = engine.replace('public void PulseAbility(int slot,float windup){}','public void PulseAbility(int slot,float windup){AbilityPulses++;}')
    engine = engine.replace('public static float SurfaceDetail=>1;', 'public static float SurfaceDetail=>1;public static int Count(int n)=>n;')
    engine = engine.replace('public bool IsListening,IsServer,IsConnectedClient;', 'public bool IsListening,IsServer,IsConnectedClient;public Clock ServerTime=new();public List<ulong> ConnectedClientsIds=new(){0,7};')
    engine = engine.replace('public enum NetworkDelivery{', 'public class Clock {public double Time;}public enum NetworkDelivery{')
    engine = engine.replace('public byte[] LastPacket;', 'public List<(string Name,byte[] Data,ulong Client)> Packets=new();public byte[] LastPacket;')
    engine = engine.replace('LastMessage=s;LastPacket=w.Data;', 'LastMessage=s;LastPacket=w.Data;Packets.Add((s,w.Data,id));')
    engine = engine.replace('public void WriteValueSafe(int i)=>', 'public void WriteValueSafe(double d)=>writer.Write(d);public void WriteValueSafe(int i)=>')
    engine = engine.replace('public void ReadValueSafe(out int i)=>', 'public void ReadValueSafe(out double d)=>d=reader.ReadDouble();public void ReadValueSafe(out int i)=>')
    # Minimal particle APIs for testing the real replacement scheduler; native rendering is separate.
    engine = engine.replace('public class MeshRenderer:Component {', 'public class MeshRenderer:Renderer {} public class Renderer:Component {')
    engine = engine.replace('public class Transform:Component {', 'public class Transform:Component {public Transform Find(string n)=>GameObject.All.Where(g=>g.transform.parent==this&&g.name==n).Select(g=>g.transform).FirstOrDefault();public Vector3 localPosition;public Quaternion localRotation;')
    engine = engine.replace('public static class Resources {', 'public static class Resources {public static object EpicLibrary;')
    engine = engine.replace('where T:class{if(s=="CombatVfx/AttackAtlas"', 'where T:class{if(s=="CombatVfx/EpicToon/Library")return EpicLibrary as T;if(s=="CombatVfx/AttackAtlas"')
    engine = engine.replace('public class Object {', 'public class Object {public static GameObject Instantiate(GameObject source,Transform parent,bool world){var clone=new GameObject(source.name);clone.transform.SetParent(parent,world);clone.AddComponent<ParticleSystem>();clone.AddComponent<ParticleSystemRenderer>();return clone;}')
    with tempfile.TemporaryDirectory(prefix='df-vfx-') as directory:
        directory = Path(directory)
        fixtures = directory / 'Engine.cs'
        fixtures.write_text(engine + (here / 'EpicParticleFixtures.cs').read_text())
        # Compile the production snapshot body and expiry properties without replacing the
        # combat suite's separate fake damage engine. This isolates network shield lifetime.
        health = (root / 'Assets/Alterverse/Scripts/Combat/Health.cs').read_text()
        capacity = health[health.index('private float HeroBaseShieldCapacity'):health.index('private const float HeroBaseShieldRechargeDelay')]
        snapshot = health[health.index('public void ApplyNetworkSnapshot('):health.index('public void RestoreFull()')]
        add_shield = health[health.index('public void AddShield(float amount, float duration)'):health.index('private void ClearTransient()')]
        transient = re.search(r'private float TransientShield[^;]+;', health)[0]
        shield = re.search(r'public float Shield[^;]+;', health)[0]
        replica = directory / 'NetworkHealthReplica.cs'
        replica.write_text('using System;using UnityEngine;namespace Alterverse.Combat {public sealed class NetworkHealthReplica:MonoBehaviour {' +
                           'private float shield,shieldUntil,heroBaseShield;private bool heroBaseShieldEnabled;private float heroBaseShieldRechargeAt;' +
                           'private float maxHealth=1000;public float Current=1000;public bool IsDead;public event Action<float,float> Changed;' +
                           capacity + transient + shield + add_shield + snapshot + '}}')
        output = directory / 'Regression.dll'
        files = [fixtures, replica, here / 'Regression.cs', here / 'EpicToonRegression.cs', here / 'AttackAtlasRegression.cs', here / 'MonsterPresentationFixtures.cs', here / 'NetworkFixtures.cs',
                 *[root / 'Assets/Alterverse/Scripts' / n for n in names]]
        if args.negative_control:
            original = root / 'Assets/Alterverse/Scripts/Visuals/HeroVfxPool.EpicToon.cs'
            disabled = directory / 'EpicDisabled.cs'
            content = original.read_text()
            needle = 'if(epicLibrary==null)return false;'
            assert content.count(needle) == 1
            disabled.write_text(content.replace(needle, 'return false; // negative control: no native replacement'))
            files = [disabled if file == original else file for file in files]
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-langversion:9',
                        '-out:' + str(output), *['-r:' + str(p) for p in refs.glob('*.dll')], *map(str, files)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    main()

