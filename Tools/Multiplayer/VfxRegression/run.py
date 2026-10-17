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
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    shared = root / 'Tools/Aiming/Regression'
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    names = ast.literal_eval(re.search(r'^names=(\[[^\n]+\])', (shared / 'run.py').read_text(), re.M)[1])
    names += ['Visuals/HeroSkillVfx.cs', 'Visuals/HeroSkillVfx.VoidKnight.cs',
              'Characters/VoidKnightBalance.cs', 'Characters/VoidKnightState.cs',
              'Characters/HeroAbilityExecution.VoidKnight.cs', 'Multiplayer/CoopHeroStateSync.Vfx.cs']
    engine = (shared / 'EngineStubs.cs').read_text()
    engine = remove_class(engine, 'public sealed partial class HeroSkillVfx:')
    engine = remove_class(engine, 'public static class CoopHeroStateSync ')
    engine = engine.replace('public static class Debug {', 'public static class Debug {public static int ErrorCount;')
    engine = engine.replace('public static void LogError(object x)=>Console.WriteLine(x);', 'public static void LogError(object x){ErrorCount++;}')
    engine = engine.replace('public class GameObject:Object {', 'public class GameObject:Object {public bool TryGetComponent<T>(out T value) where T:class {value=GetComponent<T>();return value!=null;}public T GetComponentInParent<T>()where T:class=>GetComponent<T>();')
    engine = engine.replace('public struct Color {', 'public struct Color {public static Color operator *(Color a,Color b)=>new(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);')
    engine = engine.replace('public class SerializeField:Attribute{}', 'public class DisallowMultipleComponent:Attribute{} public class SerializeField:Attribute{}')
    engine = engine.replace('public static bool CanSimulateHostGameplay=>', 'public static bool CanMutateAuthoritativeState=>CanSimulateHostGameplay;public static bool CanSimulateHostGameplay=>')
    engine = engine.replace('public float DashSeconds;', 'public void PulseBasic(float w){}public void PulseAttack(bool skill){}public void Empower(float d){}public float DashSeconds;')
    engine = engine.replace('public static float SurfaceDetail=>1;', 'public static float SurfaceDetail=>1;public static int Count(int n)=>n;')
    engine = engine.replace('public bool IsListening,IsServer,IsConnectedClient;', 'public bool IsListening,IsServer,IsConnectedClient;public Clock ServerTime=new();public List<ulong> ConnectedClientsIds=new(){0,7};')
    engine = engine.replace('public enum NetworkDelivery{', 'public class Clock {public double Time;}public enum NetworkDelivery{')
    engine = engine.replace('public byte[] LastPacket;', 'public List<(string Name,byte[] Data,ulong Client)> Packets=new();public byte[] LastPacket;')
    engine = engine.replace('LastMessage=s;LastPacket=w.Data;', 'LastMessage=s;LastPacket=w.Data;Packets.Add((s,w.Data,id));')
    engine = engine.replace('public void WriteValueSafe(int i)=>', 'public void WriteValueSafe(double d)=>writer.Write(d);public void WriteValueSafe(int i)=>')
    engine = engine.replace('public void ReadValueSafe(out int i)=>', 'public void ReadValueSafe(out double d)=>d=reader.ReadDouble();public void ReadValueSafe(out int i)=>')
    with tempfile.TemporaryDirectory(prefix='df-vfx-') as directory:
        directory = Path(directory)
        fixtures = directory / 'Engine.cs'
        fixtures.write_text(engine)
        output = directory / 'Regression.dll'
        files = [fixtures, here / 'Regression.cs', here / 'NetworkFixtures.cs',
                 *[root / 'Assets/Alterverse/Scripts' / n for n in names]]
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-langversion:9',
                        '-out:' + str(output), *['-r:' + str(p) for p in refs.glob('*.dll')], *map(str, files)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    main()
