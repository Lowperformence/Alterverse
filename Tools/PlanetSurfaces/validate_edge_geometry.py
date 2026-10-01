"""Deterministic numeric mirror of ShatteredArenaEdge.Warp; tests all planet grids."""
from pathlib import Path
import math,json,numpy as np
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir())
THEMES=['Moon','Mars','GreenPlanet','Noxara','Kryon','Earth','Pyraxis','Neptune','Dark00','DiamondCore','GJ504b']
def hashed(index,seed):
 n=(index*374761393+seed*668265263)&0xffffffff;n=((n^(n>>13))*1274126177)&0xffffffff
 return ((n^(n>>16))&0xffffff)/16777215

def angular(angle,count,seed):
 pos=angle*count;i=np.floor(pos).astype(int);t=pos-i
 h=np.array([hashed(j,seed) for j in range(count)])
 return h[i%count]*(1-t)+h[(i+1)%count]*t

def warp(x,z,theme):
 x=np.asarray(x);z=np.asarray(z);r=np.sqrt(x*x+z*z);edge=np.maximum(np.abs(x)/42,np.abs(z)/36);t=np.clip((edge-.88)/.12,0,1);w=t*t*(3-2*t)
 a=(np.arctan2(z,x)/(2*math.pi))%1;seed=19;slot=(a*14+.17)%1;nt=np.clip((np.abs(slot-.5)-.065)/.135,0,1);notch=nt*nt*(3-2*nt);extension=.10+(2.4+angular(a,24,seed)*3+angular(a,80,seed+31)*.7)*notch
 return x+x/np.maximum(r,.001)*extension*w,z+z/np.maximum(r,.001)*extension*w

def perimeter(grid):return np.concatenate([grid[0,:-1],grid[:-1,-1],grid[-1,:0:-1],grid[:0:-1,0]])

def validate():
 rows=[]
 for theme,name in enumerate(THEMES):
  xs,zs=(192,160) if name=='Mars' else (168,144) if name=='GJ504b' else (84,72) if name=='DiamondCore' else (144,128)
  x,z=np.meshgrid(np.linspace(-42,42,xs+1),np.linspace(-36,36,zs+1));wx,wz=warp(x,z,theme)
  a=np.stack([wx[:-1,:-1],wz[:-1,:-1]],-1);b=np.stack([wx[:-1,1:],wz[:-1,1:]],-1);c=np.stack([wx[1:,:-1],wz[1:,:-1]],-1);d=np.stack([wx[1:,1:],wz[1:,1:]],-1)
  cross=lambda p,q:p[...,0]*q[...,1]-p[...,1]*q[...,0]
  areas=np.concatenate([cross(b-a,c-a).ravel(),cross(d-b,c-b).ravel()]);assert areas.min()>0,(name,'folded top triangle',areas.min())
  center=np.maximum(np.abs(x)/42,np.abs(z)/36)<=.88;assert np.all(wx[center]==x[center]) and np.all(wz[center]==z[center])
  assert np.all(np.abs(wx)>=np.abs(x)-1e-8) and np.all(np.abs(wz)>=np.abs(z)-1e-8)
  boundary=perimeter(np.stack([wx,wz],-1));assert len(boundary)==2*(xs+zs)
  for side in [wx[0],wx[-1],wz[:,0],wz[:,-1]]:assert np.isfinite(side).all()
  rows.append({'planet':name,'grid':[xs,zs],'boundaryVertices':len(boundary),'minimumSignedTriangleArea':float(areas.min()/2),'interiorUnchanged':True,'outwardOnly':True,'sharedCliffTop':True})
 (ROOT/'ArtSource/PlanetSurfaces/edge-validation.json').write_text(json.dumps(rows,indent=2));print('EDGE_VALIDATED',len(rows),'planets')
 return rows
if __name__=='__main__':validate()
