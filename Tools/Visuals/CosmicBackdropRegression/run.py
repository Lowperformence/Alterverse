#!/usr/bin/env python3
"""Compile actual production C# against engine fixtures; does not replace Unity/device QA."""
import argparse
import json
import re
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = args.source_root or here.parents[2]
    menu_shader = (root/'Assets/Alterverse/Resources/FrontEnd/CosmicMenuEffect.shader').read_text()
    assert len(re.findall(r'\bPass\s*\{', menu_shader)) == 1, 'Menu shader must have exactly one isolated pass'
    assert 'PreviewType' not in menu_shader and 'FragSky' not in menu_shader
    map_shader = (root/'Assets/Alterverse/Resources/MapPanoramas/PhotoSkybox.shader').read_text()
    assert len(re.findall(r'\bPass\s*\{', map_shader)) == 1 and 'ZTest Always' not in map_shader
    arena = (root/'Assets/Alterverse/Scripts/Visuals/PhotoArenaPanorama.cs').read_text()
    assert 'CosmicSkyMaterial' not in arena and 'ForArena' not in arena
    assert 'MapPanoramas/Universe' in arena and 'MapPanoramas/PhotoSkybox' in arena
    print('Shader isolation: arena uses only the restored single-pass photo skybox; menu shader remains separate.', flush=True)
    dotnet = args.dotnet_root.resolve()
    refs = sorted((dotnet/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc = sorted((dotnet/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime = sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1].name
    sources = [root/'Assets/Alterverse/Scripts'/name for name in (
        'Visuals/CosmicSkyMaterial.cs', 'Visuals/PhotoArenaPanorama.cs',
        'UI/MenuUniverseBackground.cs', 'UI/MenuCosmicBackdrop.cs')]
    sources += [root/'Tools/UI/MenuBackgroundRegression/Fixtures.cs', here/'Fixtures.cs', here/'Regression.cs',
                root/'Assets/Alterverse/Editor/CosmicSkyImportSettings.cs']
    with tempfile.TemporaryDirectory(prefix='df-cosmic-') as temporary:
        for server in (False, True):
            output = Path(temporary)/('Server.dll' if server else 'Client.dll')
            command = [str(dotnet/'dotnet'), str(csc), '-nologo', '-debug:portable', '-langversion:9',
                       '-define:UNITY_EDITOR'+(',UNITY_SERVER' if server else ''),
                       '-out:'+str(output), *['-r:'+str(p) for p in refs.glob('*.dll')], *map(str,sources)]
            subprocess.run(command, check=True)
            output.with_suffix('.runtimeconfig.json').write_text(json.dumps({
                'runtimeOptions': {'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
            subprocess.run([str(dotnet/'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode)

