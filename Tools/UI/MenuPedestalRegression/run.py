#!/usr/bin/env python3
"""Compile the complete production showcase/grounding/pedestal with affine engine fixtures."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def extract(source, declaration):
    start = source.index(declaration)
    end = source.index('{', start)+1
    depth = 1
    while depth:
        depth += (source[end] == '{')-(source[end] == '}')
        end += 1
    return source[start:end]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    sdk = args.dotnet_root
    refs = sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((sdk/'shared/Microsoft.NETCore.App').iterdir())[-1].name
    sources = [root/'Assets/Alterverse/Scripts/UI'/name for name in
               ('MenuHeroShowcase.cs', 'MenuHeroGrounding.cs', 'MenuHeroPedestal.cs')]
    sources += [here/name for name in ('EngineStubs.cs', 'PresentationStubs.cs', 'Regression.cs')]
    with tempfile.TemporaryDirectory(prefix='df-menu-pedestal-') as folder:
        folder = Path(folder)
        solver = (root/'Assets/Alterverse/Scripts/Visuals/CombatMotion.cs').read_text()
        (folder/'Solve.cs').write_text('using UnityEngine;namespace Alterverse.Visuals {public static class CombatMotion {'+
                                      extract(solver,'        public static void Solve(')+'}}')
        output = folder/'Regression.dll'
        command = [str(sdk/'dotnet'),str(csc),'-nologo','-nowarn:0108,0067','-langversion:9','-out:'+str(output),
                   *['-r:'+str(p) for p in refs.glob('*.dll')],*map(str,sources),str(folder/'Solve.cs')]
        subprocess.run(command,check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0',
            'framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
        subprocess.run([str(sdk/'dotnet'),str(output)],check=True)


if __name__ == '__main__':
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode)
