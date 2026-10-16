"""Compile production ranked/network classes and run roster invariants with engine substitutes."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path)
p.add_argument('--mono-root',type=Path)
p.add_argument('--roslyn',type=Path)
a=p.parse_args()
if not a.dotnet_root and not (a.mono_root and a.roslyn):
    p.error('Supply --dotnet-root, or --mono-root and --roslyn')
here=Path(__file__).resolve().parent
root=here.parents[2]
source=root/'Assets/Alterverse/Scripts'
names=['Profile/SafeIdentityCatalog.cs','Profile/PlayerProfile.cs','Profile/AccountProfilePrefs.cs','Profile/SocialService.cs','Multiplayer/MatchSession.cs','Multiplayer/CoopLobbyService.cs','Multiplayer/OnlinePartyService.cs','Multiplayer/OnlinePlanHandshake.cs','Multiplayer/EdgegapOnlineFlow.cs','Multiplayer/MultiplayerGate.cs','Multiplayer/CoopMatchPlan.cs','Multiplayer/OnlineMatchPlan.cs','Prototype/FiveVsFiveTestRoster.cs','Combat/TeamMember.cs','Multiplayer/CoopPlanHandshake.cs',
       'Multiplayer/RankedAvailability.cs','Multiplayer/RankedService.cs','Multiplayer/RankedMatchReporter.cs','Multiplayer/EdgegapMatchmakerClient.cs',
       'Multiplayer/DedicatedOnlineMatchHandshake.cs','Multiplayer/EdgegapDedicatedServerLifecycle.cs','UI/RankedPanel.cs']
with tempfile.TemporaryDirectory(prefix='df-ranked-') as directory:
    menu=(source/'UI/MainMenuController.cs').read_text()
    start=menu.index('        private async System.Threading.Tasks.Task BeginOnlineMatchmakingAsync()')
    end=menu.index('        private async System.Threading.Tasks.Task BeginOfflineMatchAsync()',start)
    ui=Path(directory)/'OnlineMenuEntry.cs'
    ui.write_text('using Alterverse.Multiplayer;using UnityEngine;namespace Alterverse.UI {public sealed partial class MainMenuController {'+menu[start:end]+'}}')
    if not a.dotnet_root:
        runtime=a.mono_root/'usr/lib/mono/4.5'
        env=dict(os.environ, DF_TEST_IDENTITY_CATALOG=str(root/'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json'), MONO_PATH=str(runtime)+':'+str(runtime/'Facades'),
                 MONO_CFG_DIR=str(a.mono_root/'etc'),
                 LD_LIBRARY_PATH=str(a.mono_root/'usr/lib')+':'+str(a.mono_root/'usr/lib/x86_64-linux-gnu'))
        output=Path(directory)/'RankedRegression.exe'
        subprocess.run([str(a.mono_root/'usr/bin/mono-sgen'),str(a.roslyn),'-nologo','-noconfig','-nowarn:0649,0067',
            '-langversion:9','-define:MONO_FIXTURE','-out:'+str(output),
            '-r:'+str(runtime/'System.dll'),'-r:'+str(runtime/'System.Core.dll'),
            '-r:'+str(runtime/'System.Runtime.Serialization.dll'),
            '-r:'+str(runtime/'System.Xml.dll'),
            str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(here/'FailureRegression.cs'),str(here/'IdentityRegression.cs'),str(here/'TeamFormatRegression.cs'),str(here/'LobbyServiceStubs.cs'),str(here/'LobbyRegression.cs'),str(ui),
            *[str(source/name) for name in names]],env=env,check=True)
        subprocess.run([str(a.mono_root/'usr/bin/mono-sgen'),str(output)],env=env,check=True)
        raise SystemExit(0)
    sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1]
    runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
    output=Path(directory)/'RankedRegression.dll'
    response=Path(directory)/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0067','-target:exe','-langversion:9','-out:'+str(output),
        *['-r:'+str(path) for path in runtime.glob('*.dll')],str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(here/'FailureRegression.cs'),str(here/'IdentityRegression.cs'),str(here/'TeamFormatRegression.cs'),str(here/'LobbyServiceStubs.cs'),str(here/'LobbyRegression.cs'),str(ui),
        *[str(source/name) for name in names]]))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],env=dict(os.environ,DF_TEST_IDENTITY_CATALOG=str(root/'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json')),check=True)
