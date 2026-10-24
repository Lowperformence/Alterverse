#!/usr/bin/env python3
"""Focused bomber import/playback contract tests. Controlled engine fixtures, not Unity/GPU QA."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path, required=True)
a = p.parse_args()
dotnet = a.dotnet_root.resolve()
here = Path(__file__).resolve().parent
root = here.parents[2]
src = root / 'Assets/Alterverse'
bomber = (src / 'Scripts/Combat/SpaceBomberEvent.cs').read_text()
arenas = (src / 'Scripts/Match/ArenaVariantRuntime.cs').read_text()
arena_enum = re.search(r'public enum ArenaVariant\s*\{[^}]+\}', arenas)[0]
start = bomber.index('            if(resolved)return;resolved=true;', bomber.index('private void ResolveImpact()'))
end = bomber.index('            // Track damaged Health components', start)
presentation = bomber[start:end]
assert 'BomberNukeVfx.TryPlay(impact,ImpactRadius)' in presentation
assert 'PrototypeVfx.Ring' not in presentation
assert 'private const float ImpactRadius=3.24f;' in bomber
shader = (src / 'Resources/CombatVfx/BomberNukeParticles.shader').read_text()
assert 'input.positionWS.xz - _AttackClip.xy' in shader
assert 'clip(_AttackClip.z * _AttackClip.z - dot(offset, offset))' in shader
refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-bomber-nuke-') as temp:
    temp = Path(temp)
    bridge = temp / 'Detonation.cs'
    bridge.write_text('using UnityEngine;using Alterverse.Visuals;namespace Alterverse.Combat {'
        'public sealed class Detonation {private bool resolved;private const float ImpactRadius=3.24f;'
        'private Vector3 target=new(4,0,-9);public void Resolve(){' + presentation + '}}}' +
        'namespace Alterverse.Match {' + arena_enum + '}')
    output = temp / 'Regression.dll'
    subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-noconfig', '-define:UNITY_EDITOR', '-langversion:latest',
        '-out:' + str(output), *['-r:' + str(x) for x in refs.glob('*.dll')],
        str(here / 'Fixtures.cs'), str(here / 'Regression.cs'), str(bridge),
        str(src / 'Scripts/Visuals/BomberNukeVfx.cs'), str(src / 'Editor/BomberNukeBuilder.cs')], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
    subprocess.run([str(dotnet / 'dotnet'), str(output)], cwd=temp, check=True)
