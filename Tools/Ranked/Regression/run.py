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
       'Multiplayer/DedicatedOnlineMatchHandshake.cs','Multiplayer/EdgegapDedicatedServerLifecycle.cs','UI/RankedPanel.cs','Match/MatchExit.cs','Match/PostMatchAutoLoop.cs','Multiplayer/NetworkRoundLoop.cs','Multiplayer/CoopConnectionWatchdog.cs','Multiplayer/CoopWorldStateSync.cs','Multiplayer/WorldSnapshotHistory.cs','Multiplayer/SnapshotCadence.cs']
with tempfile.TemporaryDirectory(prefix='df-ranked-') as directory:
    # Keep roster fixtures on the production layout rather than duplicating spawn formulas.
    geometry=(source/'Combat/GameplayCollision.cs').read_text()
    start=geometry.index('    public static class ArenaLayout')
    opening=geometry.index('{',start);depth=1;end=opening+1
    while depth:
        depth+=(geometry[end]=='{')-(geometry[end]=='}');end+=1
    layout=Path(directory)/'ArenaLayout.cs'
    layout.write_text('using UnityEngine;namespace Alterverse.Combat {'+geometry[start:end]+'}')
    menu=(source/'UI/MainMenuController.cs').read_text()
    start=menu.index('        private async System.Threading.Tasks.Task BeginOnlineMatchmakingAsync()')
    end=menu.index('        private async System.Threading.Tasks.Task BeginOfflineMatchAsync()',start)
    ui=Path(directory)/'OnlineMenuEntry.cs'
    ui.write_text('using Alterverse.Multiplayer;using UnityEngine;namespace Alterverse.UI {public sealed partial class MainMenuController {'+menu[start:end]+'}}')
    # Compile real Hotzone state transitions and their server/client message handlers.
    # Terrain mesh generation and physics scoring are controlled boundaries, not native tests.
    import re
    def method(text, declaration):
        start=text.index(declaration);end=text.index('{',start)+1;depth=1
        while depth:
            depth+=(text[end]=='{')-(text[end]=='}');end+=1
        return text[start:end]
    hot=(source/'Match/HotZoneDirector.cs').read_text()
    net=(source/'Multiplayer/CoopWorldEventSync.cs').read_text()
    hotcode=Path(directory)/'HotzoneTransitions.cs'
    hotcode.write_text('using UnityEngine;using Alterverse.Combat;namespace Alterverse.Match {public sealed partial class HotZoneDirector {'+
        ''.join(re.findall(r'public const (?:float|int) [^;]+;',hot))+
        ''.join(re.findall(r'public static event [^;]+;',hot))+
        ''.join(method(hot,d) for d in ['private void Open()','private void Resolve()','public void ApplyNetworkOpen(',
            'public void ApplyNetworkResolve(','public void ApplyNetworkProgress('])+'}}'+
        'namespace Alterverse.Multiplayer {using Unity.Collections;using Unity.Netcode;using Alterverse.Match;public sealed partial class CoopWorldEventSync {'+
        ''.join(re.findall(r'private const string Hot[^;]+;',net))+
        ''.join(method(net,d) for d in ['private void OnHostHotOpened(','private void OnHostHotResolved(',
            'private void OnHostHotProgress(','private bool CanBroadcast()','private void SendAll(',
            'private void OnHotOpen(','private void OnHotResolve(','private void OnHotProgress(',
            'private static bool ValidServerMessage('])+'}}')
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
            str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(here/'FailureRegression.cs'),str(here/'IdentityRegression.cs'),str(here/'GoogleNameRegression.cs'),str(here/'BotLobbyRegression.cs'),str(here/'TeamFormatRegression.cs'),str(here/'FingerprintRegression.cs'),str(here/'LobbyServiceStubs.cs'),str(here/'LobbyRegression.cs'),str(here/'LifecycleFixtures.cs'),str(here/'LifecycleRegression.cs'),str(hotcode),str(ui),str(layout),
            *[str(source/name) for name in names]],env=env,check=True)
        subprocess.run([str(a.mono_root/'usr/bin/mono-sgen'),str(output)],env=env,check=True)
        raise SystemExit(0)
    sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1]
    runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
    output=Path(directory)/'RankedRegression.dll'
    response=Path(directory)/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-nowarn:0649,0067','-target:exe','-langversion:9','-out:'+str(output),
        *['-r:'+str(path) for path in runtime.glob('*.dll')],str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(here/'FailureRegression.cs'),str(here/'IdentityRegression.cs'),str(here/'GoogleNameRegression.cs'),str(here/'BotLobbyRegression.cs'),str(here/'TeamFormatRegression.cs'),str(here/'FingerprintRegression.cs'),str(here/'LobbyServiceStubs.cs'),str(here/'LobbyRegression.cs'),str(here/'LifecycleFixtures.cs'),str(here/'LifecycleRegression.cs'),str(hotcode),str(ui),str(layout),
        *[str(source/name) for name in names]]))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],env=dict(os.environ,DF_TEST_IDENTITY_CATALOG=str(root/'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json')),check=True)



