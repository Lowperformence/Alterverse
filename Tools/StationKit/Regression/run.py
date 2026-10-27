"""Validate actual Blender exports and compile the client/server visual adapter with controlled fixtures."""
import argparse,json,math,subprocess,tempfile,gzip
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2];source=root/'Assets/Alterverse/Scripts/Visuals'
library=root/'Assets/Alterverse/Resources/StationKit/StationBuildings.bytes';data=json.loads(gzip.decompress(library.read_bytes()));assert data['version']==1
assert {m['name'] for m in data['models']}=={'core','deck','bunker','trench','healpad','healnode'}
stats={}
for model in data['models']:
 name=model['name'];triangles=0;points=[]
 for g in model['groups']:
  v,n,uv,t=g['vertices'],g['normals'],g['uv'],g['triangles'];assert len(v)==len(n) and len(v)%3==0 and len(uv)==len(v)//3*2
  assert all(math.isfinite(x) for x in v+n+uv);assert len(t)%3==0 and all(0<=i<len(v)//3 for i in t)
  assert g['mount'] in {'static','turret','available'};triangles+=len(t)//3
  for i in range(0,len(v),3):
   point=v[i:i+3]
   if g['mount']=='turret':point=[point[0],point[1]+2.38,point[2]+.22]
   points.append(point)
  # Export handedness: triangle winding must agree with its exported normals.
  for i in range(0,len(t),3):
   pts=[v[t[i+j]*3:t[i+j]*3+3] for j in range(3)];u=[pts[1][j]-pts[0][j] for j in range(3)];w=[pts[2][j]-pts[0][j] for j in range(3)]
   cross=[u[1]*w[2]-u[2]*w[1],u[2]*w[0]-u[0]*w[2],u[0]*w[1]-u[1]*w[0]];norm=n[t[i]*3:t[i]*3+3]
   assert sum(cross[j]*norm[j] for j in range(3))>=-1e-6,(name,'inverted triangle')
 assert triangles<=3000,(name,'mobile triangle budget');stats[name]=triangles
 if name in {'healpad','healnode'}:assert max(p[1] for p in points)<= (.06 if name=='healpad' else .04)
 if name=='bunker':assert max(abs(p[0]) for p in points)<6.45/2 and max(abs(p[2]) for p in points)<5.15/2
 if name=='trench':assert max(abs(p[0]) for p in points)<7.25/2 and max(abs(p[2]) for p in points)<5.10/2
 if name=='core':assert max(math.hypot(p[0],p[2]) for p in points)<1.62
print('PASS: native Blender geometry, indices, normals, UVs, height/footprint limits and mobile budgets:',stats,flush=True)
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-station-kit-') as tmp:
 temp=Path(tmp);output=temp/'Regression.dll';rsp=temp/'compile.rsp'
 files=[here/'Fixtures.cs',here/'Regression.cs',source/'StationBuildingRuntime.cs',source/'StationBuildingVisual.cs']
 args=['-nologo','-target:exe','-langversion:9','-out:'+str(output),*['-r:'+str(f) for f in runtime.glob('*.dll')],*map(str,files)]
 rsp.write_text('\n'.join(args));subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(output),str(library)],check=True)
 rsp.write_text(rsp.read_text().replace('-target:exe','-target:library\n-define:UNITY_SERVER').replace(str(output),str(temp/'Server.dll')))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
