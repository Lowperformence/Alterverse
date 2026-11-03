"""Compile actual nebula/voice code against controlled engine and SDK boundaries.
Native rendering, Android audio devices and live Vivox are not simulated.
"""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];source=root/'Assets/Alterverse/Scripts'
runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
csc=sorted((a.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
names=['UI/GameInviteShare.cs','Combat/CosmicNebulaLayout.cs','Combat/CosmicNebulaField.cs','Combat/CosmicNebulaState.cs',
       'Combat/CombatVisibility.cs','Visuals/CosmicNebulaConcealment.cs','Multiplayer/PartyVoiceService.cs',
       'Multiplayer/OfflineLobbyRoster.cs','AI/BotHeroPool.cs']
with tempfile.TemporaryDirectory(prefix='df-lobby-') as temp:
    temp=Path(temp)
    for platform in ['UNITY_EDITOR','UNITY_ANDROID','UNITY_SERVER']:
        output=temp/(platform+'.dll');rsp=temp/'compile.rsp'
        rsp.write_text('\n'.join(['-nologo','-langversion:9','-nowarn:0649,0414,0067','-define:'+platform,
            '-out:'+str(output),*['-r:'+str(x) for x in runtime.glob('*.dll')],
            str(here/'EngineStubs.cs'),str(here/'Regression.cs'),*[str(source/n) for n in names]]))
        subprocess.run([str(a.dotnet_root/'dotnet'),str(csc),'@'+str(rsp)],check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{
            'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
        subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],check=True)
from entrance import run
run(a.dotnet_root,root)
from offline_ui import run as compile_offline_ui
compile_offline_ui(a.dotnet_root,root)
