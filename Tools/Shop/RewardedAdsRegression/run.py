"""Compile the real rewarded service, journal, account cache and wallet against a fake ad SDK.
Does not call Google or grant real rewards. Native SDK/device QA is still required.
"""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path)
a = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
manifest = json.loads((root / 'Packages/manifest.json').read_text())
assert manifest['dependencies']['com.google.ads.mobile'] == '11.5.0'
assert manifest['dependencies']['com.google.external-dependency-manager'] == '1.2.188'
assert 'com.unity.external-dependency-manager' not in manifest['dependencies']
assert any('com.google.ads.mobile' in r['scopes'] for r in manifest['scopedRegistries'])
settings = (root / 'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset').read_text()
assert 'adMobAndroidAppId: ca-app-pub-1716027263297702~9368994561' in settings
assert 'guid: a187246822bbb47529482707f3e0eff8' in settings
print('PASS: Existing resolver preserved, SDK registry and Android app ID configured.')
if not a.dotnet_root:
    print('Native Unity and C# execution not available without --dotnet-root.')
    raise SystemExit(0)

src = root / 'Assets/Alterverse/Scripts'
files = [src / name for name in (
    'Shop/RewardedShardAds.cs', 'Shop/RewardedShardPayout.cs', 'Shop/PaidShardCommerce.cs',
    'Profile/AccountProfilePrefs.cs', 'Profile/PlayerWallet.cs', 'Profile/ProfileTransaction.cs')]
sdk = sorted((a.dotnet_root / 'sdk').iterdir())[-1]
refs = sorted((a.dotnet_root / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
version = sorted((a.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
for defines in ('UNITY_ANDROID', 'UNITY_ANDROID,DEVELOPMENT_BUILD', 'UNITY_ANDROID,UNITY_EDITOR', 'UNITY_SERVER'):
    with tempfile.TemporaryDirectory(prefix='df-ads-regression-') as folder:
        folder = Path(folder)
        dll = folder / 'Regression.dll'
        rsp = folder / 'compile.rsp'
        rsp.write_text('\n'.join([
            '-nologo', '-langversion:9', '-nowarn:0649,0067', '-define:' + defines, '-out:' + str(dll),
            *['-r:' + str(x) for x in refs.glob('*.dll')],
            str(here.parent / 'AccountRegression/EngineStubs.cs'), str(here / 'AdSdkStubs.cs'),
            str(here / 'Regression.cs'), *map(str, files)]))
        subprocess.run([str(a.dotnet_root / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(rsp)], check=True)
        dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}))
        print('Testing:', defines, flush=True)
        subprocess.run([str(a.dotnet_root / 'dotnet'), str(dll)], check=True)
