"""Deterministic software preview of the exact game mesh (not image generation)."""
import json,gzip,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/NerokArmor'
d=json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/CyberShark/Armor20261012.bytes').read_bytes()))
v=np.array(d['vertices']).reshape(-1,3);normals=np.array(d['normals']).reshape(-1,3)
palette=np.array([[.06,.085,.10],[.22,.38,.38],[.49,.23,.105],[.11,.047,.025],[.56,.60,.56],[.27,.032,.026]])
def unit(a):return a/np.linalg.norm(a)
def render(name,eye):
 global v
 original=v.copy()
 if name=='exploded':
  for i,b in enumerate(d['boneIndices']):
   bone=d['boneNames'][b];side=-1 if bone.endswith('L') else 1
   if bone.startswith('UpperArm'):v[i]+=np.array([side*.4,.10,0])
   elif bone.startswith('Forearm'):v[i]+=np.array([side*.5,-.025,0])
   elif bone.startswith('Shin'):v[i]+=np.array([side*.3,-.07,0])
   elif bone.startswith('Thigh'):v[i]+=np.array([side*.20,-.08,0])
 W,H=1000,1100;target=np.array([0,1.,0]);f=unit(np.array(eye)-target);right=unit(np.cross([0,1,0],f));up=np.cross(f,right);q=v-target
 proj=np.column_stack([q@right,q@up,q@f]);scale=H/2.25
 xy=np.column_stack([W*.5+proj[:,0]*scale,H*.52-proj[:,1]*scale]);z=proj[:,2]
 bg=np.zeros((H,W,3),float);yy=np.linspace(0,1,H)[:,None,None];bg[:]=np.array([.15,.19,.205])*(1-yy*.35)
 depth=np.full((H,W),-np.inf)
 light=unit(np.array([-3,5,5]));fill=unit(np.array([4,2,-1]));half=unit(light+f)
 for mat,sub in enumerate(d['submeshes']):
  tris=np.array(sub['triangles']).reshape(-1,3)
  for t in tris:
   p=xy[t];lo=np.maximum(np.floor(p.min(axis=0)).astype(int),[0,0]);hi=np.minimum(np.ceil(p.max(axis=0)).astype(int),[W-1,H-1])
   if np.any(lo>hi):continue
   a,b,c=p;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
   if abs(den)<1e-8:continue
   xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
   wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
   wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
   zz=wa*z[t[0]]+wb*z[t[1]]+wc*z[t[2]];sl=np.s_[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
   mask=(wa>=-1e-5)&(wb>=-1e-5)&(wc>=-1e-5)&(zz>depth[sl])
   if not mask.any():continue
   ns=wa[...,None]*normals[t[0]]+wb[...,None]*normals[t[1]]+wc[...,None]*normals[t[2]]
   ns/=np.maximum(np.linalg.norm(ns,axis=2,keepdims=True),1e-9)
   diffuse=.22+.66*np.maximum(0,ns@light)+.2*np.maximum(0,ns@fill)
   spec=np.maximum(0,ns@half)**(35 if mat in [0,1,2,4] else 14)*(.20 if mat in [0,1,2,4] else .03)
   col=np.clip((palette[mat]*diffuse[...,None]+spec[...,None])**.4545,0,1)
   depth[sl][mask]=zz[mask];bg[sl][mask]=col[mask]
 im=Image.fromarray((np.clip(bg,0,1)*255).astype(np.uint8));draw=ImageDraw.Draw(im)
 draw.text((30,26),'NEROK / NEW ARMOR ONLY / '+name.upper(),fill=(215,220,215))
 im.save(ART/('armor-'+name+'.png'));v=original
for name,eye in [('front',[0,2.05,6]),('three-quarter',[3,2.8,5]),('back',[2,2.6,-5]),('exploded',[0,2.05,6])]:render(name,eye);print('Rendered',name,flush=True)
