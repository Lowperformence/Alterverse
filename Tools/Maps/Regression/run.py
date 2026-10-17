#!/usr/bin/env python3
"""Compile production arena layout/collision math against minimal engine fixtures.
Does not validate Unity physics, rendering, or a live client/server session.
"""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];dotnet=a.dotnet_root.resolve()
sdk=sorted((dotnet/'sdk').iterdir())[-1];runtime=sorted((dotnet/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-map-layout-') as d:
 d=Path(d);out=d/'Regression.dll';rsp=d/'compile.rsp'
 source=root/'Assets/Alterverse/Scripts/Combat/GameplayCollision.cs'
 # Compile the actual offline and online spawn methods as well as the shared geometry.
 wrappers=d/'SpawnRoutes.cs'
 parts=['using Alterverse.Combat;using UnityEngine;namespace Alterverse.Multiplayer {public static class MatchSession {public const int TeamSize=4;}}']
 for name,path in [('Offline','Prototype/FiveVsFiveTestRoster.cs'),('Online','Multiplayer/CoopRosterBuilder.cs')]:
  text=(root/'Assets/Alterverse/Scripts'/path).read_text()
  method=re.search(r'(?:private|public) static Vector3 SafeSpawn\(int team,int index\)\s*\{([^}]+)\}',text).group(1)
  parts.append('public static class '+name+' {public static Vector3 SafeSpawn(int team,int index) {'+method.replace('MatchSession.TeamSize','Alterverse.Multiplayer.MatchSession.TeamSize')+'}}')
 wrappers.write_text('\n'.join(parts))
 rsp.write_text('\n'.join(['-nologo','-target:exe','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in runtime.glob('*.dll')],str(source),str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(wrappers)]))
 subprocess.run([str(dotnet/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(dotnet/'dotnet'),str(out)],check=True)
