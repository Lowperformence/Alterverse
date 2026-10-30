"""Exercise production Health membership, world-key configuration and discovery caching.

Unity components are fixtures; this does not measure native server or Android FPS.
--source-root can run the previous revision as a negative control.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def block(text, declaration):
    start = text.index(declaration)
    arrow = text.find('=>', start, text.index('{', start))
    if arrow >= 0:
        return text[start:text.index(';', arrow) + 1]
    end = text.index('{', start) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
parser.add_argument('--source-root', type=Path)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = args.source_root or here.parents[2]
src = root / 'Assets/Alterverse/Scripts'
health = (src / 'Combat/Health.cs').read_text()
registry = health[health.index('private static readonly System.Collections.Generic.List<Health> active'):health.index('[RuntimeInitializeOnLoadMethod')]
if 'RegistryVersion' not in registry:
    registry += 'public static uint RegistryVersion {get;private set;}'
health_code = 'using UnityEngine;namespace Alterverse.Combat {public sealed partial class Health:MonoBehaviour {' + registry + ''.join(
    block(health, d) for d in ['private static void ResetRegistry()', 'private static void RebuildRegistry()', 'private void OnEnable()', 'private void OnDisable()']) + '}}'
key = (src / 'Multiplayer/CoopWorldEntityKey.cs').read_text()
key_code = 'using UnityEngine;using Alterverse.Combat;namespace Alterverse.Multiplayer {public sealed partial class CoopWorldEntityKey:MonoBehaviour {' + block(key, 'public void Configure(') + '}}'
world = (src / 'Multiplayer/CoopWorldStateSync.cs').read_text()
start = world.index('public sealed class CoopWorldStateSync')
world_code = 'using System.Collections.Generic;using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;using Unity.Netcode;namespace Alterverse.Multiplayer {' + world[start:world.index('private void OnDisable()')] + block(world, 'private void RefreshEntities()') + 'public void Refresh()=>RefreshEntities();public bool Contains(string key)=>entities.ContainsKey(key);}}'
sdk = args.dotnet_root.resolve()
refs = sorted((sdk / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc = sorted((sdk / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
runtime = sorted((sdk / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix='df-entity-cache-') as temporary:
    temporary = Path(temporary)
    generated = []
    for name, text in [('Registry.cs', health_code), ('Key.cs', key_code), ('Discovery.cs', world_code)]:
        path = temporary / name
        path.write_text(text)
        generated.append(path)
    binary = temporary / 'Regression.dll'
    subprocess.run([str(sdk / 'dotnet'), str(csc), '-nologo', '-langversion:9', '-nowarn:0649,0169,0414', '-out:' + str(binary),
                    *['-r:' + str(p) for p in refs.glob('*.dll')], *map(str, generated), str(here / 'Regression.cs')], check=True)
    binary.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
    subprocess.run([str(sdk / 'dotnet'), str(binary)], check=True)
