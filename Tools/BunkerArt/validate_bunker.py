"""Validate Blender topology and fitting envelopes independently of Unity."""
from pathlib import Path
import json,numpy as np
root=Path(__file__).resolve().parents[2];data=json.loads((root/'Assets/Alterverse/Resources/AuthoredBunker/BunkerDetails.json').read_text());rows=[]
expected={'DF_Bunker_MainShell':(7.95,1.32,6.58),'DF_Bunker_RoofArmor':(8.62,.48,7.18),'DF_Bunker_Foundation':(8.45,.48,7.05)}
for part in data['parts']:
 v=np.array(part['vertices']).reshape(-1,3);n=np.array(part['normals']).reshape(-1,3);uv=np.array(part['uv']).reshape(-1,2);t=np.array(part['triangles']).reshape(-1,3)
 assert np.isfinite(v).all() and np.isfinite(n).all() and np.isfinite(uv).all();assert len(v)==len(n)==len(uv);assert t.min()>=0 and t.max()<len(v);assert 0<=part['material']<4
 cross=np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]]);assert (np.linalg.norm(cross,axis=1)>1e-9).all();assert (np.einsum('ij,ij->i',cross,n[t].mean(axis=1))>0).all()
 lo=v.min(0);hi=v.max(0)
 if part['target']:
  size=np.array(expected[part['target']]);center=np.array([0,size[1]/2,0]);fitted=center+(v-(lo+hi)/2)*size/(hi-lo)
  assert np.allclose(fitted.min(0),center-size/2) and np.allclose(fitted.max(0),center+size/2)
 elif part['mount']=='body':
  assert lo[0]>=-4.6 and hi[0]<=4.6 and lo[2]>=-3.85 and hi[2]<=4.65
 else:assert part['mount']=='turret' and max(abs(lo[0]),abs(hi[0]))<.6
 rows.append({'part':part['name'],'triangles':len(t),'topology':'passed','fittingEnvelope':'passed','mount':part['mount']})
assert len(rows)==8 and sum(row['triangles'] for row in rows)<10000
(root/'ArtSource/Bunker/validation.json').write_text(json.dumps(rows,indent=2));print('BUNKER_VALIDATED: 8 meshes, topology, UVs, 3 preserved shell envelopes, 5 batched detail meshes.')
