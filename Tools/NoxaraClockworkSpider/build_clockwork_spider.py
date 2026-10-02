"""Original brass clockwork spider, bound to the existing articulated pivots."""
from pathlib import Path
import json, math, gzip
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/NoxaraClockworkSpider'
ART=ROOT/'ArtSource/HotZoneMonsters/NoxaraClockworkSpider'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
BONES={};MATRIX={}
def pose(path,parent,p=(0,0,0),rot=(0,0,0)):
 x,y,z=np.radians(rot);rx=np.array([[1,0,0],[0,np.cos(x),-np.sin(x)],[0,np.sin(x),np.cos(x)]]);ry=np.array([[np.cos(y),0,np.sin(y)],[0,1,0],[-np.sin(y),0,np.cos(y)]]);rz=np.array([[np.cos(z),-np.sin(z),0],[np.sin(z),np.cos(z),0],[0,0,1]])
 m=np.eye(4);m[:3,:3]=ry@rx@rz;m[:3,3]=p
 MATRIX[path]=(MATRIX[parent]@m) if parent is not None else m
 BONES[path]=None
pose('',None)
for si,s in enumerate([-1,1]):
 for leg in range(4):
  idx=si*4+leg;spread=[.48,.18,-.18,-.48][leg];yaw=math.degrees(math.atan2(-spread,s))
  h=f'DF_NoxaraSpiderLeg_{idx}';k=h+f'/DF_NoxaraSpiderKnee_{idx}';a=k+f'/DF_NoxaraSpiderAnkle_{idx}'
  pose(h,'',(s*.68,.30,.78-leg*.52),(0,yaw,25));pose(k,h,(1.08,0,0),(0,0,-70));pose(a,k,(1.18,0,0),(0,0,45))
BN=list(BONES);V=[];F=[];MI=[];BI=[];PARTS=[];GI=[];GROUP=0
COLORS=np.array([[.042,.035,.021],[.50,.36,.12],[.48,.53,.49],[.28,.80,.025],[.36,.13,.055]])
def unit(v):
 v=np.array(v,dtype=float);return v/max(np.linalg.norm(v),1e-12)
def add(name,vertices,faces,mat,bone):
 off=len(V);GI.extend([GROUP]*len(vertices));V.extend(np.array(vertices).tolist());BI.extend([BN.index(bone)]*len(vertices));F.extend([[off+i for i in f] for f in faces]);MI.extend([mat]*len(faces));PARTS.append({'name':name,'bone':bone,'material':mat,'vertices':len(vertices),'triangles':len(faces)})
def ell(name,c,s,mat,bone,seg=24,rings=12):
 c=np.array(c);s=np.array(s);verts=[c+np.array([0,s[1],0])]
 for row in range(1,rings):
  a=math.pi*row/rings
  for j in range(seg):
   b=2*math.pi*j/seg;verts.append(c+s*np.array([math.sin(a)*math.cos(b),math.cos(a),math.sin(a)*math.sin(b)]))
 verts.append(c-np.array([0,s[1],0]));faces=[]
 for j in range(seg):faces.append([0,1+(j+1)%seg,1+j])
 for row in range(rings-2):
  for j in range(seg):
   a=1+row*seg+j;b=1+row*seg+(j+1)%seg;faces.extend([[a,b,b+seg],[a,b+seg,a+seg]])
 last=len(verts)-1;base=1+(rings-2)*seg
 for j in range(seg):faces.append([base+j,base+(j+1)%seg,last])
 # Ensure consistent outward normals even if primitive construction order changes.
 for f in faces:
  a,b,d=[verts[i] for i in f]
  if np.dot(np.cross(b-a,d-a),(a+b+d)/3-c)<0:f.reverse()
 add(name,verts,faces,mat,bone)
def tube(name,points,radii,mat,bone,sides=8):
 p=np.array(points,float);verts=[];faces=[]
 if np.isscalar(radii):radii=[radii]*len(p)
 last_u=None
 for i,c in enumerate(p):
  tangent=unit(p[min(i+1,len(p)-1)]-p[max(i-1,0)])
  u=unit(np.cross(tangent,[0,0,1]))
  if np.linalg.norm(u)<.5:u=unit(np.cross(tangent,[1,0,0]))
  if last_u is not None and np.dot(u,last_u)<0:u=-u
  last_u=u;v=unit(np.cross(tangent,u))
  for j in range(sides):verts.append(c+radii[i]*(math.cos(j*2*math.pi/sides)*u+math.sin(j*2*math.pi/sides)*v))
 for row in range(len(p)-1):
  for j in range(sides):
   a=row*sides+j;b=row*sides+(j+1)%sides;faces.extend([[a,b,b+sides],[a,b+sides,a+sides]])
 for j in range(1,sides-1):faces.extend([[0,j+1,j],[(len(p)-1)*sides,(len(p)-1)*sides+j,(len(p)-1)*sides+j+1]])
 # tube basis produces outward side faces.
 add(name,verts,faces,mat,bone)
def ring(name,c,r,thickness,mat,bone,normal=(0,0,1),segments=32):
 n=unit(normal);u=unit(np.cross(n,[0,1,0]));
 if np.linalg.norm(u)<.5:u=np.array([1,0,0])
 v=unit(np.cross(n,u));pts=[np.array(c)+r*(math.cos(a)*u+math.sin(a)*v) for a in np.linspace(0,2*math.pi,segments+1)]
 tube(name,pts,thickness,mat,bone,8)
def shell(name,levels,mat,bone,sides=28):
 # levels = y, half-width, half-depth, z-center; smooth anatomical cross sections.
 verts=[];faces=[]
 for y,rx,rz,cz in levels:
  for a in np.linspace(0,2*math.pi,sides,endpoint=False):verts.append([rx*math.cos(a),y,cz+rz*math.sin(a)])
 for row in range(len(levels)-1):
  for j in range(sides):
   a=row*sides+j;b=row*sides+(j+1)%sides;faces.extend([[a,b,b+sides],[a,b+sides,a+sides]])
 for j in range(1,sides-1):faces.extend([[0,j+1,j],[(len(levels)-1)*sides,(len(levels)-1)*sides+j,(len(levels)-1)*sides+j+1]])
 # Ascending Y with +Z circumference needs reversed winding.
 faces=[list(reversed(f)) for f in faces]
 add(name,verts,faces,mat,bone)
def plate(name,outline,z,depth,mat,bone):
 # Curved, chamfered front rather than a flat box.
 xy=np.array(outline,float);center=xy.mean(axis=0);verts=[]
 for scale,d in [(1,-depth*.45),(.93,depth*.2),(.82,depth*.65)]:
  for x,y in center+(xy-center)*scale:verts.append([x,y,z+d-.08*x*x])
 verts.append([*center,z+depth*.88]);n=len(xy);faces=[]
 for row in range(2):
  for j in range(n):
   a=row*n+j;b=row*n+(j+1)%n;faces.extend([[a,b,b+n],[a,b+n,a+n]])
 for j in range(n):faces.append([2*n+j,2*n+(j+1)%n,3*n])
 # Auto orient front faces consistently towards +Z.
 for f in faces:
  a,b,c=[np.array(verts[i]) for i in f]
  if np.cross(b-a,c-a)[2]<0:f.reverse()
 add(name,verts,faces,mat,bone)
def limb(name,a,b,r,mat,bone):
 a=np.array(a);b=np.array(b);ts=[0,.12,.35,.68,.90,1];tube(name,[a+(b-a)*t for t in ts],[r*.64,r*.88,r,r*.94,r*.72,r*.60],mat,bone,20)


def bolts(name,points,bone,r=.009):
 for p in points:ell(name,p,(r,r,r*.65),4,bone,10,6)
def oval(name,c,rx,ry,r,mat,bone):
 tube(name,[np.array(c)+[rx*math.cos(a),ry*math.sin(a),0] for a in np.linspace(0,2*math.pi,49)],r,mat,bone)
def panel(name,outline,z,depth,bone):
 plate(name+' silver rim',outline,z,depth,2,bone)
 xy=np.array(outline);c=xy.mean(axis=0)
 plate(name+' dark inset',c+(xy-c)*.88,z+depth*.35,depth*.6,1,bone)
def scar(name,a,b,bone):tube(name,[a,b],.0022,4,bone,5)


def shard(name,a,b,width,depth,mat,bone):
 a=np.array(a);b=np.array(b);axis=unit(b-a);u=unit(np.cross(axis,[0,0,1]))
 if np.linalg.norm(u)<.5:u=np.array([1,0,0])
 v=unit(np.cross(axis,u));c=a+(b-a)*.35
 verts=[a-u*width*.45,a+u*width*.45,a-v*depth*.45,a+v*depth*.45,c-u*width,c+v*depth,c+u*width,c-v*depth,b]
 faces=[[0,4,5],[0,5,3],[3,5,6],[3,6,1],[1,6,7],[1,7,2],[2,7,4],[2,4,0],[4,8,5],[5,8,6],[6,8,7],[7,8,4]]
 center=np.mean(verts,axis=0)
 for f in faces:
  p,q,r=[verts[i] for i in f]
  if np.dot(np.cross(q-p,r-p),(p+q+r)/3-center)<0:f.reverse()
 add(name,verts,faces,mat,bone)
def fabric(name,rows,mat,bone,phase=0):
 # Tapered hanging panels with modeled folds, thickness and irregular hems.
 verts=[];n=13
 for y,left,right,z in rows:
  for j in range(n):
   t=j/(n-1);x=left+(right-left)*t
   verts.append([x,y,z+.014*math.sin(t*math.pi*4+phase)+.007*math.cos(t*math.pi*7)])
 faces=[]
 for row in range(len(rows)-1):
  for j in range(n-1):
   a=row*n+j;faces.extend([[a,a+1,a+1+n],[a,a+1+n,a+n]])
 # Render both sides with separate normals (no dependency on double-sided shader).
 off=len(verts);verts+= [[x,y,z-.006] for x,y,z in verts]
 faces+= [[c+off,b+off,a+off] for a,b,c in faces]
 add(name,verts,faces,mat,bone)


def beam(name,a,b,r,mat,bone):limb(name,a,b,r,mat,bone)
def hinge(name,c,r,bone,axis=(0,0,1)):
 ring(name+' rim',c,r,.024,2,bone,axis,24)
 ring(name+' gasket',c,r*.65,.018,0,bone,axis,20)
 ell(name+' hub',c,(r*.52,r*.52,r*.52),4,bone,16,8)
def dorsal(name,c,s,bone,mat=1):
 # Chamfered armor slab, front/back faces extruded along local Y.
 cx,cy,cz=c;rx,ry,rz=s
 xy=[(-rx*.73,-rz), (rx*.73,-rz),(rx,-rz*.6),(rx,rz*.6),(rx*.65,rz),(-rx*.65,rz),(-rx,rz*.6),(-rx,-rz*.6)]
 verts=[]
 for scale,y in [(1,cy-ry),(.96,cy),(.78,cy+ry)]:
  verts += [[cx+x*scale,y,cz+z*scale] for x,z in xy]
 verts.append([cx,cy+ry*1.1,cz]);faces=[]
 for row in range(2):
  for j in range(8):
   a=row*8+j;b=row*8+(j+1)%8;faces.extend([[a,b,b+8],[a,b+8,a+8]])
 for j in range(8):faces.append([16+j,16+(j+1)%8,24])
 center=np.array(c)
 for f in faces:
  a,b,d=[np.array(verts[i]) for i in f]
  if np.dot(np.cross(b-a,d-a),(a+b+d)/3-center)<0:f.reverse()
 add(name,verts,faces,mat,bone)

# Compact exposed brass gearbox and a long rear reagent cylinder.
ell('clockwork central body',(0,.43,.05),(.56,.22,.68),0,'',24,12)
dorsal('brass gearbox deck',(0,.60,.10),(.51,.075,.56),'',1)
for c,r in [((0,.69,.20),.29),((-.28,.69,-.08),.14),((.28,.69,-.10),.15)]:
 ring('gear wheel',c,r,.032,1,'',(0,1,0),32)
 ring('gear inner race',c,r*.61,.022,2,'',(0,1,0),24)
 for j in range(16):
  a=j*2*math.pi/16;q=np.array(c)+[r*math.cos(a),0,r*math.sin(a)]
  ell('gear tooth',q,(.044,.033,.035),1,'',8,4)
 ell('gear axle',c,(.055,.049,.055),4,'',14,8)
# Syringe/reagent chamber: green vessel inside a brass cage with silver end flanges.
limb('green reagent vessel',(0,.43,-.60),(0,.43,-2.14),.265,3,'')
for z in [-.60,-.73,-2.03,-2.15]:ring('chamber end flange',(0,.43,z),.292,.043,1,'',segments=32)
for side in [-1,1]:
 for dy in [-1,1]:tube('chamber cage rail',[(side*.216,.43+dy*.207,-.57),(side*.216,.43+dy*.207,-2.19)],.035,1,'',12)
for j in range(5):
 z=-.90-j*.21
 ring('glass reinforcement',(0,.43,z),.269,.009,2,'',segments=28)
 tube('reagent graduation mark',[(.272,.39,z),(.276,.48,z)],.006,0,'',7)
tube('rear injection spout',[(0,.43,-2.15),(0,.43,-2.43),(0,.43,-2.67)],[.095,.057,.009],2,'',12)
ring('rear valve rim',(0,.43,-2.28),.14,.026,1,'',segments=24)
# Paired telescope-like eyes, mandibles and thin front needle.
for s in [-1,1]:
 tube('sensor barrel',[(s*.21,.46,.56),(s*.21,.46,.93)],.098,1,'',16)
 ring('sensor silver rim',(s*.21,.46,.94),.093,.015,2,'',segments=24)
 ell('green optical lens',(s*.21,.46,.962),(.073,.073,.020),3,'',20,10)
 tube('clockwork mandible',[(s*.22,.26,.69),(s*.38,.18,.87),(s*.28,.13,1.12)],[.045,.031,.003],4,'',8)
 tube('small face antenna',[(s*.38,.51,.58),(s*.44,.69,.84)],[.014,.004],2,'',8)
for si,s in enumerate([-1,1]):
 for leg in range(4):
  idx=si*4+leg;h=f'DF_NoxaraSpiderLeg_{idx}';k=h+f'/DF_NoxaraSpiderKnee_{idx}';a=k+f'/DF_NoxaraSpiderAnkle_{idx}'
  hinge('hip brass bearing',(0,0,0),.12,h,(0,0,1))
  tube('upper brass rod',[(.10,0,0),(.99,0,0)],.043,1,h,12)
  tube('upper piston guide',[(.17,-.056,.01),(.84,-.056,.01)],.016,2,h,10)
  for xx in [.15,.31,.81,.99]:ring('upper ferrule',(xx,0,0),.060,.012,2,h,(1,0,0),20)
  dorsal('hip clockwork bracket',(.18,.017,0),(.17,.045,.073),h,1)
  hinge('elbow gear joint',(0,0,0),.103,k,(0,0,1))
  tube('lower copper leg',[(.09,0,0),(1.09,0,0)],.038,4,k,12)
  tube('lower silver tendon',[(.17,.049,0),(.98,.049,0)],.013,2,k,8)
  for xx in [.14,.28,.88,1.09]:ring('lower leg collar',(xx,0,0),.053,.013,1,k,(1,0,0),16)
  hinge('ankle hinge',(0,0,0),.070,a,(0,0,1))
  shard('pointed brass foot',(.01,0,0),(.25,-.052,.035),.044,.03,1,a)
  shard('foot steel tip',(.16,-.032,.023),(.31,-.065,.037),.025,.018,2,a)

localVertices=np.array(V);triangles=np.array(F,dtype=np.int32);normals=np.zeros_like(localVertices)
for ids in triangles:
 a,b,c=localVertices[ids];n=np.cross(b-a,c-a)
 for j in ids:normals[j]+=n
normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-10)
mesh={'version':1,'bonePaths':BN,'vertices':np.round(localVertices,6).ravel().tolist(),'normals':np.round(normals,6).ravel().tolist(),'boneIndices':BI,'submeshes':[{'triangles':triangles[np.array(MI)==m].ravel().tolist()} for m in range(5)]}
(OUT/'Model.bytes').write_bytes(gzip.compress(json.dumps(mesh,separators=(',',':')).encode(),mtime=0))
vertices=np.array([(MATRIX[BN[b]]@np.r_[v,1])[:3] for v,b in zip(localVertices,BI)])
normals=np.array([MATRIX[BN[b]][:3,:3]@n for n,b in zip(normals,BI)])
report={'vertices':len(V),'triangles':len(F),'bones':len(BN),'parts':len(PARTS),'bounds':[vertices.min(0).tolist(),vertices.max(0).tolist()]}
assert np.isfinite(vertices).all() and np.isfinite(normals).all() and triangles.max()<len(V) and len(V)<65535
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
# Offline rasterizer: render exact exported geometry with interpolated normals.
def render(yaw,name):
 w,h=1000,850;scale=135;angle=math.radians(yaw);tilt=math.radians(28)
 ry=np.array([[math.cos(angle),0,math.sin(angle)],[0,1,0],[-math.sin(angle),0,math.cos(angle)]])
 rx=np.array([[1,0,0],[0,math.cos(tilt),-math.sin(tilt)],[0,math.sin(tilt),math.cos(tilt)]])
 rotation=rx@ry;p=(vertices-np.array([0,.30,0]))@rotation.T;n=normals@rotation.T
 xy=np.column_stack([w/2+p[:,0]*scale,h/2-p[:,1]*scale]);z=p[:,2]
 yy,xx=np.mgrid[:h,:w];grad=np.clip(1-np.sqrt(((xx-w*.5)/w)**2+((yy-h*.44)/h)**2),0,1)
 img=np.empty((h,w,3));img[:]=np.array([.025,.035,.05])+grad[:,:,None]*np.array([.035,.045,.055]);depth=np.full((h,w),-1e9)
 lights=[(unit([-.6,.8,1]),np.array([1.0,.97,.88])),(unit([.7,.2,.7]),np.array([.34,.46,.56])),(unit([.1,.6,-1]),np.array([.15,.40,.29]))]
 for ids,material in zip(triangles,MI):
  pts=xy[ids];lo=np.maximum(np.floor(pts.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(pts.max(axis=0)).astype(int),[w-1,h-1])
  if (hi<lo).any():continue
  a,b,c=pts;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
  if abs(den)<1e-8:continue
  gy,gx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];u=((b[1]-c[1])*(gx-c[0])+(c[0]-b[0])*(gy-c[1]))/den;v=((c[1]-a[1])*(gx-c[0])+(a[0]-c[0])*(gy-c[1]))/den;t=1-u-v
  iz=u*z[ids[0]]+v*z[ids[1]]+t*z[ids[2]];mask=(u>=0)&(v>=0)&(t>=0)&(iz>depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1])
  if not mask.any():continue
  normal=u[:,:,None]*n[ids[0]]+v[:,:,None]*n[ids[1]]+t[:,:,None]*n[ids[2]];normal/=np.maximum(np.linalg.norm(normal,axis=2)[:,:,None],1e-9)
  color=np.broadcast_to(COLORS[material]*.30,(*u.shape,3)).copy()
  for direction,lc in lights:
   ndl=np.maximum(0,np.sum(normal*direction,axis=2));half=unit(direction+[0,0,1]);spec=np.maximum(0,np.sum(normal*half,axis=2))**(44 if material in [1,2,4] else 12)
   color+=COLORS[material]*ndl[:,:,None]*lc*.85+spec[:,:,None]*lc*(.30 if material in [1,2,4] else .06)
  if material==3:color+=COLORS[material]*.85
  patch=img[lo[1]:hi[1]+1,lo[0]:hi[0]+1];patch[mask]=color[mask];depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=iz[mask]
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'CLOCKWORK SPIDER / NOXARA',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-24,'front.png');render(150,'back.png')
