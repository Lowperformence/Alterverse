#!/usr/bin/env python3
"""Compile the production roster refresh against deterministic scene-discovery fixtures."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
text = (root / 'Assets/Alterverse/Scripts/Multiplayer/CoopHeroStateSync.cs').read_text()
start = text.index('private void RefreshSlots(')
end = text.index('private void Attach(', start)
production = text[start:end]
engine = """using System;using System.Collections.Generic;
namespace UnityEngine {
 public enum FindObjectsInactive {Exclude}
 public class Component {public GameObject gameObject;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();}
 public class MonoBehaviour:Component {}
 public class GameObject {
  readonly Dictionary<Type,object> components=new();
  public T AddComponent<T>()where T:Component,new(){var value=new T{gameObject=this};components[typeof(T)]=value;return value;}
  public T GetComponent<T>()where T:class=>components.TryGetValue(typeof(T),out var value)?value as T:null;
 }
 public static class Time {public static float unscaledTime;}
}
namespace Alterverse.Multiplayer {
 public class CoopMatchPlan {}
 public static class MatchSession {public const int MaxPlayers=8;}
 public static class CoopAuthority {public static bool Active,LocalHasServerAuthority;public static CoopMatchPlan ActivePlan=new();}
 public sealed class CoopHeroSlot:UnityEngine.Component {public int Slot;public bool IsLocalHuman;public string PlayerId;}
 public sealed class CoopHeroReplicaPresentation:UnityEngine.Component {public void Refresh(){}}
}
"""
runtime = args.dotnet_root.resolve()
refs = sorted((runtime / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc = sorted((runtime / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
version = sorted((runtime / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix='df-roster-cache-') as temporary:
    temporary = Path(temporary)
    (temporary / 'Engine.cs').write_text(engine)
    (temporary / 'Refresh.cs').write_text('using UnityEngine; namespace Alterverse.Multiplayer { public sealed partial class CoopHeroStateSync : MonoBehaviour {' + production + '}}')
    output = temporary / 'Regression.dll'
    subprocess.run([str(runtime / 'dotnet'), str(csc), '-nologo', '-target:exe', '-langversion:9', '-nowarn:0649,0067', '-out:' + str(output),
                    *['-r:' + str(p) for p in refs.glob('*.dll')], str(temporary / 'Engine.cs'), str(temporary / 'Refresh.cs'), str(here / 'Regression.cs')], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}))
    subprocess.run([str(runtime / 'dotnet'), str(output)], check=True)
