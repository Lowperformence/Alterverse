"""Compile real Android store, Play Games session and leaderboard against lost-callback fixtures.
This does not run Unity, the native Google SDK, Play checkout or a device.
"""
import argparse
import json
import re
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path, required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[3]
here = Path(__file__).resolve().parent
dotnet = a.dotnet_root.resolve()
csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
version = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
sources = [root / 'Assets/Alterverse/Scripts' / name for name in
           ['Shop/GooglePlayShardStore.cs', 'Profile/GooglePlayAccountSession.cs', 'UI/GooglePlayLevelLeaderboard.cs']]
with tempfile.TemporaryDirectory(prefix='df-google-connection-') as temp:
    temp = Path(temp)
    for mode in ['production', 'internal-test']:
        output = temp / (mode + '.dll')
        defines = 'UNITY_ANDROID' + (';DF_GOOGLE_PLAY_SHARD_TEST' if mode == 'internal-test' else '')
        args = ['-nologo', '-langversion:9', '-nowarn:0649,0067', '-define:' + defines,
                '-out:' + str(output), *['-r:' + str(ref) for ref in refs.glob('*.dll')],
                str(here / 'EngineStubs.cs'), str(here / 'Regression.cs'), *map(str, sources)]
        rsp = temp / (mode + '.rsp')
        rsp.write_text('\n'.join(args))
        subprocess.run([str(dotnet / 'dotnet'), str(csc), '@' + str(rsp)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)
    # Also compile the actual non-Android conditional branches.
    args = ['-nologo', '-target:library', '-langversion:9', '-nowarn:0649,0067',
            '-out:' + str(temp / 'editor.dll'), *['-r:' + str(ref) for ref in refs.glob('*.dll')],
            str(here / 'EngineStubs.cs'), *map(str, sources)]
    rsp = temp / 'editor.rsp'
    rsp.write_text('\n'.join(args))
    subprocess.run([str(dotnet / 'dotnet'), str(csc), '@' + str(rsp)], check=True)

manifest = ET.parse(root / 'Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml')
ns = '{http://schemas.android.com/apk/res/android}'
app_id_source = next(x.attrib[ns + 'value'] for x in manifest.findall('application/meta-data')
              if x.attrib[ns + 'name'] == 'com.google.android.gms.games.APP_ID')
# Android decodes these escapes when linking the manifest. Validate the runtime
# value, not the literal escape spelling: \u0020 introduced a leading space.
app_id = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1], 16)), app_id_source)
assert re.fullmatch(r'[0-9]+', app_id), 'Native App ID must contain digits only, without whitespace'
assert app_id == '1029475752167', 'Native manifest must identify the actual published game'
assert app_id_source.startswith(r'\u003'), 'Keep the numeric metadata typed as a string'
print('PASS Android manifest digits-only App ID and non-Android compilation')
