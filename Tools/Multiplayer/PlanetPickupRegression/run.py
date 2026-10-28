#!/usr/bin/env python3
"""Exercise production planet/pickup event predicates with the real CoopAuthority.

Uses transport substitutes; does not replace a Unity server/client match test.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[3]
scripts = root / 'Assets/Alterverse/Scripts'
events = {
    'Match/DimensionPlanetDirector.cs': ['NetworkPlanetSpawned'],
    'Match/DimensionPlanet.cs': ['NetworkBuffGranted'],
    'Combat/FractureCorePickup.cs': ['NetworkSpawned', 'NetworkClaimed'],
    'Combat/PlanetMeteorShower.cs': ['NetworkMeteorSpawned'],
    'Match/PlanetPulseAttack.cs': ['NetworkPulseStarted', 'NetworkPulseDetonated'],
}
predicates = []
for path, names in events.items():
    source = (scripts / path).read_text()
    for name in names:
        # Allow the pickup serial assignment before its spawn notification.
        pattern = r'if\s*\(([^{};]+)\)\s*(?:\{\s*pickup\.networkId=coopSerial\+\+;\s*)?' + name + r'\?\.Invoke\('
        matches = re.findall(pattern, source)
        if len(matches) != 1:
            raise AssertionError(f'Expected one event predicate for {path}:{name}')
        predicates.append(f'Check(({matches[0]}) == expected, "{name}");')

fixture = r'''
using System;
using Alterverse.Multiplayer;
using Unity.Netcode;
namespace UnityEngine {
 public enum RuntimeInitializeLoadType {SubsystemRegistration}
 [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute:Attribute {
  public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value){}
 }
}
namespace Unity.Netcode {public class NetworkManager {
 public static NetworkManager Singleton;public bool IsListening,IsServer;
}}
namespace Alterverse.AI {public static class BotDifficultySettings {
 public static int OnlineDifficulty=2;public static void BeginMatchOverride(int value){}
 public static void EndMatchOverride(){}
}}
namespace Alterverse.Match {public static class ArenaVariantRuntime {
 public static void BeginCoopMatch(int value){}
}}
namespace Alterverse.Multiplayer {
 public class CoopMatchPlan {public int Difficulty,Arena;}
 public class OnlineMatchPlan:CoopMatchPlan {}
}
static class Regression {
 static int checks;
 static void Check(bool value,string name) {if(!value)throw new Exception(name);checks++;}
 static void Events(bool expected,bool announceNetwork=true,bool authoritative=true,int networkId=1) {
  PREDICATES
 }
 static void Mode(bool host,bool server,bool expected) {
  CoopAuthority.EndMatch();
  NetworkManager.Singleton=new NetworkManager {IsListening=true,IsServer=server};
  CoopAuthority.Prepare(new OnlineMatchPlan());CoopAuthority.BeginPreparedMatch(host);
  Events(expected);
  Check(CoopAuthority.CanMutateAuthoritativeState==expected,"Authoritative mutations");
  if(!expected)Check(!CoopAuthority.CanSimulateHostGameplay,"Client cannot claim/heal locally");
 }
 static void Main() {
  NetworkManager.Singleton=null;CoopAuthority.EndMatch();Events(false);
  Check(CoopAuthority.CanSimulateHostGameplay,"Offline gameplay still simulates");
  Mode(false,true,true); // Dedicated server: the original regression.
  Events(true,false,false,-1); // Individual optional event guards checked below instead.
  Mode(true,true,true); // Relay host remains supported.
  Mode(false,false,false); // Remote player cannot publish authoritative events.
  Mode(true,false,false); // A listening transport takes precedence over host hint.
  CoopAuthority.EndMatch();NetworkManager.Singleton=null;
  CoopAuthority.Prepare(new CoopMatchPlan());CoopAuthority.BeginPreparedMatch(true);Events(true);
  CoopAuthority.EndMatch();CoopAuthority.Prepare(new CoopMatchPlan());
  CoopAuthority.BeginPreparedMatch(false);Events(false);
  Console.WriteLine("PASS: "+checks+" planet/pickup authority checks (transport substitutes)");
 }
}
'''.replace('PREDICATES', '\n'.join(predicates))
# Optional guards have different meanings per event, so test them independently.
optional = []
for predicate in predicates:
    if 'announceNetwork' in predicate:
        optional.append('Check(!(' + re.search(r'Check\(\((.*)\) == expected', predicate).group(1) + '), "Replica must not rebroadcast");')
    if 'authoritative&&' in predicate:
        optional.append('Check(!(' + re.search(r'Check\(\((.*)\) == expected', predicate).group(1) + '), "Visual detonation must not rebroadcast");')
    if 'networkId>=' in predicate:
        optional.append('Check(!(' + re.search(r'Check\(\((.*)\) == expected', predicate).group(1) + '), "Unassigned pickup must not publish claim");')
fixture = fixture.replace('Events(true,false,false,-1); // Individual optional event guards checked below instead.',
    '{bool announceNetwork=false,authoritative=false;int networkId=-1;' + ''.join(optional) + '}')
runtime = args.dotnet_root.resolve()
sdk = sorted((runtime / 'sdk').iterdir())[-1]
framework = sorted((runtime / 'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-planet-pickup-') as temp:
    temp = Path(temp)
    source = temp / 'Regression.cs'
    source.write_text(fixture)
    output = temp / 'Regression.dll'
    response = temp / 'compile.rsp'
    response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9', '-out:' + str(output),
        *['-r:' + str(p) for p in framework.glob('*.dll')], str(source),
        str(scripts / 'Multiplayer/CoopAuthority.cs')]))
    subprocess.run([str(runtime / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': framework.name}}}))
    subprocess.run([str(runtime / 'dotnet'), str(output)], check=True)
