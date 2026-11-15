"""Compile focused production killstreak logic with controlled engine boundaries."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet-root',type=Path,required=True)
args=parser.parse_args()
here=Path(__file__).resolve().parent
root=here.parents[2]
source=root/'Assets/Alterverse/Scripts'
runtime=sorted((args.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
csc=sorted((args.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]


def block(text, signature):
    start=text.index(signature)
    end=text.index('{',start)+1
    depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}')
        end+=1
    return text[start:end]


def run_csharp(directory,name,files):
    out=directory/(name+'.dll')
    rsp=directory/(name+'.rsp')
    rsp.write_text('\n'.join(['-nologo','-langversion:9','-nowarn:0649,0414','-out:'+str(out),
        *['-r:'+str(p) for p in runtime.glob('*.dll')],*map(str,files)]))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(csc),'@'+str(rsp)],check=True)
    out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0',
        'framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    subprocess.run([str(args.dotnet_root/'dotnet'),str(out)],check=True)


with tempfile.TemporaryDirectory(prefix='df-killstreak-') as tmp:
    tmp=Path(tmp)
    run_csharp(tmp,'Rewards',[source/'Combat/KillstreakRewardState.cs',here/'RewardRegression.cs'])
    # Run the network test against the actual production handlers; serialization,
    # Unity objects and authority are controlled fixtures, not live networking.
    commands=(source/'Multiplayer/CoopGameplayCommands.cs').read_text()
    methods=block(commands,'private bool TryReadAuthorizedActor(')
    fixture=(here/'TransportFixtures.cs').read_text().replace('/*AUTH_METHOD*/',methods)
    generated=tmp/'Transport.cs';generated.write_text(fixture)
    run_csharp(tmp,'Transport',[generated,source/'Multiplayer/KillstreakNetworkSync.cs'])

    # Verify that clients can discover each turret variant from a valid snapshot key.
    hud=(source/'UI/CombatFeedbackHUD.cs').read_text()
    methods='\n'.join(block(hud,s) for s in ['public static KillstreakBoltTurret EnsureNetworkReplica(',
        'private void FireBoltBasicAttack('])
    fixture=(here/'ReplicaFixtures.cs').read_text().replace('/*PRODUCTION_METHODS*/',methods)
    generated=tmp/'Replica.cs';generated.write_text(fixture)
    run_csharp(tmp,'Replica',[generated])

    # Verify the real steering decisions with controlled collision outcomes.
    fixture=(here/'MovementFixtures.cs').read_text().replace('/*PRODUCTION_METHOD*/',
        block(hud,'private void MoveToward('))
    generated=tmp/'Movement.cs';generated.write_text(fixture)
    run_csharp(tmp,'Movement',[generated])
