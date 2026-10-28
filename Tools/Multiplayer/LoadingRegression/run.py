"""Exercise shared online/offline loading UI and online reveal with controlled engine fixtures."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path, required=True)
a = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
source = root / 'Assets/Alterverse/Scripts'
sdk = sorted((a.dotnet_root / 'sdk').iterdir())[-1]
runtime = sorted((a.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-online-loading-') as directory:
    directory = Path(directory)
    # Compile the real camera reveal method; arena building itself needs native Unity assets.
    bootstrap = (source / 'Prototype/PrototypeBootstrap.cs').read_text()
    reveal = bootstrap[bootstrap.index('private void LateUpdate()'):bootstrap.index('public System.Collections.IEnumerator RestartPrototype()')]
    startup = bootstrap[bootstrap.index('private System.Collections.IEnumerator Start()'):bootstrap.index('private void LateUpdate()')]
    extracted = directory / 'CameraReveal.cs'
    extracted.write_text('using UnityEngine;using Alterverse.UI;using Alterverse.Match;using Alterverse.Combat;using Alterverse.Visuals;using Alterverse.Multiplayer;namespace Alterverse.Prototype {public sealed partial class PrototypeBootstrap {' + startup + reveal + '}}')
    menu = (source / 'UI/MainMenuController.cs').read_text()
    launch = menu[menu.index('private void DrawLaunch()'):menu.index('private void EnterMatch()')]
    local_entry = directory / 'LocalEntry.cs'
    local_entry.write_text('using Alterverse.Match;using Alterverse.Multiplayer;using Alterverse.Shop;namespace Alterverse.UI {public sealed partial class MainMenuController {' + launch + '}}')
    output = directory / 'Regression.dll'
    response = directory / 'compile.rsp'
    files = [here / 'EngineFixtures.cs', here / 'Regression.cs', extracted, local_entry,
             source / 'Multiplayer/OnlineMatchRuntime.cs', source / 'UI/OnlineMatchLoadingScreen.cs',
             source / 'UI/MatchLoadingPresentation.cs', source / 'UI/MatchEntryCurtain.cs',
             source / 'Content/LoadingTipContent.cs']
    response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9', '-out:' + str(output),
                                  *['-r:' + str(path) for path in runtime.glob('*.dll')], *map(str, files)]))
    subprocess.run([str(a.dotnet_root / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
    subprocess.run([str(a.dotnet_root / 'dotnet'), str(output)], check=True)



