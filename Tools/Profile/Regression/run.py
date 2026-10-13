"""Compile production lifetime statistics and reward transactions against engine substitutes."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--mono-root', type=Path)
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
source = root / 'Assets/Alterverse/Scripts'
files = ['Profile/MatchRewardService.cs', 'Profile/ProfileTransaction.cs',
         'Profile/ProgressionSummary.cs', 'Match/MatchRewardService.cs']
env = dict(os.environ)
if args.mono_root:
    runtime = args.mono_root / 'usr/lib/mono/4.5'
    env.update(MONO_PATH=str(runtime) + ':' + str(runtime/'Facades'),
               MONO_CFG_DIR=str(args.mono_root/'etc'),
               LD_LIBRARY_PATH=str(args.mono_root/'usr/lib') + ':' + str(args.mono_root/'usr/lib/x86_64-linux-gnu'))
    mono = str(args.mono_root/'usr/bin/mono-sgen')
    compiler = [mono, str(runtime/'mcs.exe')]
else:
    mono, mcs = shutil.which('mono'), shutil.which('mcs')
    if not mono or not mcs:
        parser.error('Install Mono, or supply --mono-root')
    compiler = [mcs]
with tempfile.TemporaryDirectory(prefix='df-profile-') as directory:
    output = Path(directory)/'ProfileRegression.exe'
    subprocess.run([*compiler, '-langversion:7.2', '-out:' + str(output),
                    str(here/'EngineStubs.cs'), str(here/'Regression.cs'),
                    *[str(source/file) for file in files]], env=env, check=True)
    subprocess.run([mono, str(output)], env=env, check=True)

# Validate the production wiring for solo and all authority-backed network modes.
scoreboard = (source/'Match/MatchScoreboard.cs').read_text()
assert 'playerDeaths=networkResult.Deaths' in scoreboard
assert 'playerDeaths=playerEntry?.Deaths??0' in scoreboard
assert 'deaths:playerDeaths' in scoreboard
menu = (source/'UI/MainMenuController.cs').read_text()
profile = menu.split('private void DrawProfile()', 1)[1].split('private void DrawQuests()', 1)[0]
assert '"K/D",ProgressionSummary.KillDeathRatioLabel' in profile
assert '"ASSISTS"' not in profile and '"OBJECTIVES"' not in profile
keys = json.loads((root/'Assets/Alterverse/Resources/Commerce/LegacyProfileKeys.json').read_text())['profile']
assert sum(entry == {'key': 'DF_LifetimeDeaths', 'kind': 0} for entry in keys) == 1
for key in ('DF_KDKills_v1', 'DF_KDDeaths_v1'):
    assert sum(entry == {'key': key, 'kind': 0} for entry in keys) == 1
print('PASS: solo/network death forwarding, profile rows and account import registry.')
