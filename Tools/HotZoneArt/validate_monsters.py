"""Independent checks of the exported Blender geometry and runtime fitting contract."""
import json,math
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[2];data=json.loads((ROOT/'Assets/Alterverse/Resources/AuthoredHotZone/Parts.json').read_text());rows=[]
assert data['version']==1 and len(data['parts'])==35
for part in data['parts']:
 v=np.array(part['vertices']).reshape(-1,3);n=np.array(part['normals']).reshape(-1,3);t=np.array(part['triangles']).reshape(-1,3);uv=np.array(part['uv']).reshape(-1,2)
 assert np.isfinite(v).all() and np.isfinite(n).all() and np.isfinite(uv).all()
 assert np.allclose(v.min(axis=0),-.5,atol=1e-7) and np.allclose(v.max(axis=0),.5,atol=1e-7)
 assert t.min()>=0 and t.max()<len(v) and len(n)==len(v)==len(uv)
 assert np.allclose(np.linalg.norm(n,axis=1),1,atol=2e-6)
 cross=np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]]);assert (np.linalg.norm(cross,axis=1)>1e-9).all()
 assert (np.einsum('ij,ij->i',cross,n[t].mean(axis=1))>0).all(),part['name']
 for center,size in [(np.array([0,0,0]),np.array([1,2,1])),(np.array([.13,.2,-.05]),np.array([2.1,.08,1.4]))]:
  fitted=center+v*size;assert np.allclose(fitted.min(axis=0),center-size*.5) and np.allclose(fitted.max(axis=0),center+size*.5)
 rows.append({'part':part['name'],'triangles':len(t),'boundsFit':'passed','winding':'passed','uv':'passed'})
# Custom facet fan keeps original vertices, hence extrema, and clamps inset points.
rng=np.random.default_rng(93)
for _ in range(1000):
 source=rng.uniform(-2,2,(3,3));lo=source.min(axis=0);hi=source.max(axis=0);normal=np.cross(source[1]-source[0],source[2]-source[0]);normal/=np.linalg.norm(normal)
 center=(source*np.array(data['surfaceInset']['barycentric'])[:,None]).sum(axis=0)-normal*min(hi-lo)*data['surfaceInset']['inset'];center=np.clip(center,lo,hi);fan=np.vstack([source,center]);assert np.array_equal(fan.min(axis=0),lo) and np.array_equal(fan.max(axis=0),hi)
(ROOT/'ArtSource/HotZoneMonsters/runtime-fit-validation.json').write_text(json.dumps({'parts':rows,'customFacetTrials':1000,'result':'passed'},indent=2));print('HOTZONE_VALIDATED: 35 parts, winding, UVs, original bounds and 1000 custom facets.')
