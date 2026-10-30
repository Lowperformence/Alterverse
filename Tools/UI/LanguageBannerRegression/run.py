"""Run production preference, picker and banner code against controlled engine/SDK boundaries.
Does not replace a Unity build or an Android-device ad/typography test.
"""
import argparse
import json
import re
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root', type=Path, required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
# Keep the controlled host API aligned with the actual partial class contract.
review_source = (root / 'Assets/Alterverse/Scripts/UI/MainMenuReviewPrompt.cs').read_text(encoding='utf-8')
assert re.search(r'public\s+static\s+bool\s+ReviewPromptVisible\b', review_source)
catalog_path = root / 'Assets/Alterverse/Resources/Localization/InterfaceLanguages.json'
catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
assert len(catalog['languages']) == 40
assert len({row['code'] for row in catalog['languages']}) == 40
assert len({key.lower() for key in catalog['keys']}) == len(catalog['keys'])
assert all(len(row['words']) == len(catalog['keys']) and all(row['words']) for row in catalog['languages'])
sdk = sorted((args.dotnet_root / 'sdk').iterdir())[-1]
refs = sorted((args.dotnet_root / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
runtime = sorted((args.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
sources = [root / 'Assets/Alterverse/Scripts' / p for p in (
    'Profile/GameLanguage.cs', 'UI/LanguagePicker.cs', 'UI/LocalizedText.cs',
    'UI/MenuBannerLayout.cs', 'UI/MainMenuAdvertising.cs', 'Shop/MenuBannerAds.cs')]
for defines in ('UNITY_ANDROID', 'UNITY_ANDROID,DEVELOPMENT_BUILD', 'UNITY_ANDROID,UNITY_EDITOR', 'UNITY_SERVER'):
    with tempfile.TemporaryDirectory(prefix='df-language-banner-') as tmp:
        tmp = Path(tmp)
        output = tmp / 'Regression.dll'
        rsp = tmp / 'compile.rsp'
        rsp.write_text('\n'.join([
            '-nologo', '-langversion:9', '-nowarn:0649,0414', '-define:' + defines,
            '-out:' + str(output), *['-r:' + str(p) for p in refs.glob('*.dll')],
            str(here / 'EngineStubs.cs'), str(here / 'Regression.cs'), *map(str, sources)]))
        subprocess.run([str(args.dotnet_root / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(rsp)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
        print('Testing:', defines, flush=True)
        subprocess.run([str(args.dotnet_root / 'dotnet'), str(output), str(catalog_path)], check=True)
