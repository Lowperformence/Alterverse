#!/usr/bin/env python3
"""Compile production respawn scoreboard methods against controlled GUI/discovery fixtures.
This measures work performed by these methods, not native Unity rendering or Android FPS.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def block(text, declaration):
    start = text.index(declaration)
    end = text.index('{', start) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
text = (root / 'Assets/Alterverse/Scripts/Match/MatchScoreboard.cs').read_text()
methods = ['private sealed class Entry', 'private void RefreshLocalRespawn()',
           'private TeamMember LocalPlayerMember()', 'private float Performance(',
           'private void UpdateRespawnBoard()', 'private void RefreshRespawnBoard()',
           'private void DrawHud()', 'private void DrawTeam(', 'private void RefreshRoster()']
fields = text[text.index('private CoopHeroSlot localHeroSlot;'):text.index('public bool ResultCommitted')]
score_start = text.index('private static float PerformanceScore(')
score = text[score_start:text.index(';', score_start) + 1]
production = 'using System.Collections.Generic;using UnityEngine;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Multiplayer;using Alterverse.Profile;using Alterverse.UI;namespace Alterverse.Match {public sealed partial class MatchScoreboard {' + fields + ''.join(block(text, m) for m in methods) + score + '}}'
runtime = args.dotnet_root.resolve()
refs = sorted((runtime / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
csc = sorted((runtime / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
version = sorted((runtime / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix='df-respawn-hud-') as temporary:
    temporary = Path(temporary)
    source = temporary / 'ScoreboardMethods.cs'
    source.write_text(production)
    output = temporary / 'Regression.dll'
    response = temporary / 'compile.rsp'
    response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9', '-nowarn:0649', '-out:' + str(output),
                                  *['-r:' + str(p) for p in refs.glob('*.dll')], str(source), str(here / 'Regression.cs')]))
    subprocess.run([str(runtime / 'dotnet'), str(csc), '@' + str(response)], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}))
    subprocess.run([str(runtime / 'dotnet'), str(output)], check=True)
