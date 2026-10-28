#!/usr/bin/env python3
"""Compile the production Halloween event, visual and reward sources against engine/transport fixtures."""
import argparse,json,subprocess,tempfile
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[3]
runtime=args.dotnet_root.resolve()
sdk=sorted((runtime/'sdk').iterdir())[-1]
framework=sorted((runtime/'shared/Microsoft.NETCore.App').iterdir())[-1]
scripts=root/'Assets/Alterverse/Scripts'
production=['Match/HalloweenPumpkinRules.cs','Match/HalloweenPumpkinEvent.cs','Match/HalloweenPumpkinEvent.Network.cs',
    'Visuals/HalloweenPumpkinVisual.cs','Profile/HalloweenPumpkinRewards.cs','Profile/ProfileTransaction.cs','Profile/PlayerWallet.cs',
    'Multiplayer/CoopAuthority.cs','Multiplayer/CoopHeroSlot.cs','Multiplayer/MatchNetworkProtocol.cs','Combat/GameplayCollision.cs']
with tempfile.TemporaryDirectory(prefix='df-halloween-') as tmp:
    tmp=Path(tmp);out=tmp/'Regression.dll'
    sources=[Path(__file__).with_name('EngineFixtures.cs'),Path(__file__).with_name('Regression.cs')]+[scripts/p for p in production]
    response=tmp/'compile.rsp'
    response.write_text('\n'.join(['-nologo','-target:exe','-langversion:9','-out:'+str(out),
        *['-r:'+str(p) for p in framework.glob('*.dll')],*map(str,sources)]))
    subprocess.run([str(runtime/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':framework.name}}}))
    subprocess.run([str(runtime/'dotnet'),str(out)],check=True)
