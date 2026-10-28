#!/usr/bin/env python3
"""Exercise production reward spawning/snapshot receive and result capture against engine fixtures."""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
def block(text,declaration):
 start=text.index(declaration);end=text.index('{',start)+1;depth=1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
hot=(src/'Combat/HotZoneTank.cs').read_text()
world=(src/'Multiplayer/CoopWorldStateSync.cs').read_text()
result=(src/'Multiplayer/CoopMatchResultSync.cs').read_text()
score=(src/'Match/MatchScoreboard.cs').read_text()
# Also ensure resolve events cannot create unconfirmed rewards.
assert 'HotZoneTank.Spawn(' not in block((src/'Match/HotZoneDirector.cs').read_text(),'public void ApplyNetworkResolve(')
parts=['using System;using System.Collections.Generic;using UnityEngine;using Unity.Netcode;using Unity.Collections;using Alterverse.Combat;using Alterverse.Characters;using Alterverse.Match;using Alterverse.Multiplayer;']
parts.append('namespace Alterverse.Combat {public sealed partial class HotZoneTank {'+''.join(block(hot,d) for d in ['public static bool Spawn(', 'public static HotZoneTank EnsureNetworkReplica(', 'private static bool SpawnReward(', 'private static HotZoneTank CreateReward('])+'}}')
parts.append('namespace Alterverse.Multiplayer {public sealed partial class CoopWorldStateSync {'+''.join(block(world,d) for d in ['private void RefreshEntities(', 'private void OnState('])+'}}')
parts.append('namespace Alterverse.Multiplayer {public sealed partial class CoopMatchResultSync {'+block(result,'public readonly struct SlotResult')+block(result,'private void CaptureHostResult(')+'}}')
parts.append('namespace Alterverse.Match {public sealed partial class MatchScoreboard {'+block(score,'public void GetNetworkScore(')+'}}')
with tempfile.TemporaryDirectory(prefix='df-hotzone-result-') as d:
 d=Path(d);generated=d/'Production.cs';generated.write_text('\n'.join(parts));out=d/'Regression.dll'
 refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];csc=sorted((a.dotnet_root/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
 files=[generated,here/'Regression.cs',src/'Multiplayer/HotZoneRewardKey.cs']
 subprocess.run([str(a.dotnet_root/'dotnet'),str(csc),'-nologo','-langversion:9','-nowarn:0649','-out:'+str(out),*['-r:'+str(x) for x in refs.glob('*.dll')],*map(str,files)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(out)],check=True)

