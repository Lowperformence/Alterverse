#!/usr/bin/env python3
"""Run production hazard timing/damage and headless presentation guards against deterministic engine fixtures.
This is not a Unity/native performance test. Rendering bodies are fixture boundaries.
"""
import argparse
import json
import re
import subprocess
import tempfile
from pathlib import Path

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

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--dotnet-root', type=Path, required=True)
    a = p.parse_args()
    here = Path(__file__).resolve().parent
    src = here.parents[2] / 'Assets/Alterverse/Scripts'
    header = 'using System;using System.Collections;using UnityEngine;using Alterverse.Match;using Alterverse.Combat;using Alterverse.Multiplayer;using Alterverse.Visuals;'
    parts = [header]
    hazards = [
        ('Combat/PlanetMeteorShower.cs', 'MeteorStrike', ['public static void Create(', 'private void Update()'], 'Shader ink='),
        ('Combat/NoxaraSpiderMeteorShower.cs', 'NoxaraMeteorStrike', ['public static void Create(', 'private bool OwnerAlive()', 'private void Update()', 'private void ResolveImpact()', 'private void Cleanup()'], 'Shader ink='),
        ('Combat/SpaceBomberEvent.cs', 'SpaceBombStrike', ['public static void Create(', 'private void Update()', 'private void ResolveImpact()', 'private void Cleanup()'], 'if(warningMaterial!=null)')]
    for path, name, methods, rendering_start in hazards:
        text = re.split(r'public sealed class ' + name + r'\s*:', (src / path).read_text(), maxsplit=1)[1]
        constants = re.search(r'private const float ImpactRadius=[^;]+;', text)[0]
        visual = block(text, 'private void BuildVisuals(')
        # Preserve the actual headless branch; replace only rendering with an observable fixture.
        visual = visual[:visual.index(rendering_start)] + 'VisualBuilds++;}'
        parts.append('namespace Alterverse.Combat {public sealed partial class ' + name + ' {' + constants + ''.join(block(text, d) for d in methods) + visual + '}}')
    bomber = (src / 'Combat/SpaceBomberEvent.cs').read_text()
    material = block(bomber, 'private void BuildSharedMaterials()')
    material = material[:material.index('Shader lit=')] + 'VisualBuilds++;materialsReady=true;}'
    parts.append('namespace Alterverse.Combat {public sealed partial class SpaceBomberDirector {' + material + '}}')
    with tempfile.TemporaryDirectory(prefix='df-server-hazard-') as temp:
        temp = Path(temp)
        generated = temp / 'Production.cs'
        generated.write_text('\n'.join(parts))
        output = temp / 'Regression.dll'
        dotnet = a.dotnet_root.resolve()
        refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
        csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
        runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '-nologo', '-langversion:9', '-nowarn:0649,0414', '-out:' + str(output),
            *['-r:' + str(x) for x in refs.glob('*.dll')], str(generated), str(here / 'Fixtures.cs'), str(here / 'Regression.cs')], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)

if __name__ == '__main__':
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode)
