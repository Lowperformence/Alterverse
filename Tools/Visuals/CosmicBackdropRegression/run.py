#!/usr/bin/env python3
"""Compile actual production C# against engine fixtures; does not replace Unity/device QA."""
import argparse
import json
import hashlib
import re
from pathlib import Path
import subprocess
import tempfile
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = args.source_root or here.parents[2]
    runtime_source = (root/'Assets/Alterverse/Scripts/Match/ArenaVariantRuntime.cs').read_text()
    enum = re.search(r'public enum ArenaVariant\s*\{([^}]+)\}', runtime_source)[1]
    arenas = [value.strip().split('=')[0].strip() for value in enum.split(',')]
    fixture = (here/'Fixtures.cs').read_text()
    fixture_enum = re.search(r'public enum ArenaVariant\s*\{([^}]+)\}', fixture)[1]
    assert arenas == [value.strip() for value in fixture_enum.split(',')], 'Fixture must cover the real arena catalog'
    manifest = json.loads((root/'ArtSource/CosmicSkies/manifest.json').read_text())
    assert {entry['name'] for entry in manifest['artwork']} == {'Menu', *arenas}
    hashes, guids = set(), set()
    for entry in manifest['artwork']:
        asset = root/entry['path']
        content = asset.read_bytes()
        assert hashlib.sha256(content).hexdigest() == entry['sha256']
        with Image.open(asset) as image:
            image.load()
            assert image.size == (1774,887) and image.mode in ('RGB','RGBA')
        guid = re.search(r'^guid: ([0-9a-f]{32})$', asset.with_suffix('.png.meta').read_text(), re.M)[1]
        assert guid not in guids and entry['sha256'] not in hashes, 'Artwork and GUIDs must be distinct'
        guids.add(guid);hashes.add(entry['sha256'])
    print(f'Artwork: {len(hashes)} distinct PNGs match the real arena catalog, dimensions, hashes and Unity GUIDs.', flush=True)
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
            command = [str(dotnet/'dotnet'), str(csc), '-nologo', '-langversion:9',
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
