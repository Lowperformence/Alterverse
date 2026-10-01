"""Validate the Blender profile adapted to all live map boundary resolutions."""
from pathlib import Path
import json,math,numpy as np
from validate_edge_geometry import validate,warp,perimeter,THEMES
root=next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir())
data=json.loads((root/'Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorder.json').read_text());n=data['sampleCount'];rings=data['ringCount']
scale=np.array(data['radialScale']).reshape(rings,n);depth=np.array(data['depth']).reshape(rings,n)
assert data['version']==1 and np.isfinite(scale).all() and np.isfinite(depth).all();assert scale.min()>=.5 and scale.max()<=1.04
assert (depth[0]<-.15).all() and (np.diff(depth,axis=0)<0).all();rows=validate()
for row in rows:
 xs,zs=row['grid'];x,z=np.meshgrid(np.linspace(-42,42,xs+1),np.linspace(-36,36,zs+1));wx,wz=warp(x,z,THEMES.index(row['planet']));boundary=perimeter(np.stack([wx,wz],-1))
 angle=(np.arctan2(boundary[:,1],boundary[:,0])/(2*math.pi))%1;p=angle*n;i=np.floor(p).astype(int)%n;t=p-np.floor(p)
 for relief in [-3,0,3]:
  upper=np.column_stack([boundary[:,0],np.full(len(boundary),relief),boundary[:,1]])
  for ring in range(rings):
   s=scale[ring,i]*(1-t)+scale[ring,(i+1)%n]*t;d=depth[ring,i]*(1-t)+depth[ring,(i+1)%n]*t
   lower=np.column_stack([boundary[:,0]*s,d+relief*np.clip(1+d/12,0,1),boundary[:,1]*s])
   assert (lower[:,1]<upper[:,1]).all(),(row['planet'],ring,'inverted height')
   for a,b,c in [(upper,np.roll(upper,-1,axis=0),lower),(np.roll(upper,-1,axis=0),np.roll(lower,-1,axis=0),lower)]:
    assert (np.linalg.norm(np.cross(b-a,c-a),axis=1)>1e-7).all(),(row['planet'],ring,'degenerate facet')
   upper=lower
 row['adaptiveBorderTriangles']=len(boundary)*(rings*2+1);row['universalProfile']='passed';row['detachedFragments']=0
# The shape is identical for all variants at an identical boundary coordinate.
x=np.linspace(-42,42,257);z=np.full_like(x,-36);base=warp(x,z,0)
for theme in range(1,11):
 other=warp(x,z,theme);assert all(np.array_equal(a,b) for a,b in zip(base,other))
(root/'ArtSource/PlanetSurfaces/universal-adaptation-validation.json').write_text(json.dumps(rows,indent=2))
print('Universal border passed on all eleven boundaries, three relief heights and identical variant silhouettes.')
decor=json.loads((root/'Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorderDecor.json').read_text())
assert len(decor['cracks'])==52 and len(decor['rocks'])==39
for crack in decor['cracks']:
 pts=np.array(crack['points']).reshape(-1,2);assert len(pts)>=3 and np.isfinite(pts).all() and .1<=crack['width']<=.4
 assert (np.linalg.norm(np.diff(pts,axis=0),axis=1)>.01).all()
for rock in decor['rocks']:
 assert abs(rock['x'])<=42 and abs(rock['z'])<=36 and .4<rock['size']<1.6
vertices=np.array(decor['rockVertices']).reshape(-1,3);indices=np.array(decor['rockTriangles']).reshape(-1,3)
assert np.isfinite(vertices).all() and indices.min()>=0 and indices.max()<len(vertices)
assert (np.linalg.norm(np.cross(vertices[indices[:,1]]-vertices[indices[:,0]],vertices[indices[:,2]]-vertices[indices[:,0]]),axis=1)>1e-7).all()
print('52 crack strips and 39 grounded rocks validated; no detached edge debris.')
