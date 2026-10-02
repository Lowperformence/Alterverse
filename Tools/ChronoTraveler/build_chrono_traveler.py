"""Original cyber-sniper geometry, referenced to Nyra's existing rest skeleton.
No Blender dependency. Exports indexed meshes used by ChronoTravelerAppearance and
renders the same triangles for geometry review (not a Unity/game screenshot).
"""
from pathlib import Path
import json, math, gzip
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/ChronoTraveler'
ART=ROOT/'ArtSource/Heroes/ChronoTraveler'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
# Unity convention: +Y up, +Z front. Measured proportions from the Nyra source rig.
BONES={
'Hips':((0,.918,0),(0,1.0506,0)),
'Spine':((0,1.0506,0),(0,1.224,0)),
'Chest':((0,1.224,0),(0,1.4484,0)),
'Head':((0,1.4484,0),(0,1.785,0)),
}
for side,sign in [('L',-1),('R',1)]:
 BONES['UpperArm'+side]=((sign*.1628,1.44534,-.01581),(sign*.24552,1.1832,-.02139))
 BONES['Forearm'+side]=(BONES['UpperArm'+side][1],(sign*.33176,.96696,.02604))
 BONES['Hand'+side]=(BONES['Forearm'+side][1],(sign*.3476,.8772,.04836))
 BONES['Thigh'+side]=((sign*.1222,.918,0),(sign*.1316,.4998,-.01))
 BONES['Shin'+side]=(BONES['Thigh'+side][1],(sign*.1692,.1224,-.02))
 BONES['Foot'+side]=(BONES['Shin'+side][1],(sign*.1692,.0612,.15))
BN=list(BONES);V=[];F=[];MI=[];BI=[];PARTS=[]
COLORS=np.array([[.018,.024,.029],[.062,.082,.10],[.30,.38,.42],[.02,.65,.94],[.003,.008,.012]])
# material ids: undersuit, graphite enamel, titanium, emerald energy, optical black

def unit(v):
 v=np.array(v,dtype=float);return v/max(np.linalg.norm(v),1e-12)
def add(name,vertices,faces,mat,bone):
 off=len(V);V.extend(np.array(vertices).tolist());BI.extend([BN.index(bone)]*len(vertices));F.extend([[off+i for i in f] for f in faces]);MI.extend([mat]*len(faces));PARTS.append({'name':name,'bone':bone,'material':mat,'vertices':len(vertices),'triangles':len(faces)})
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

# Closed graphite chrononaut armor, inspired by the user's full-face exosuit reference.
shell('pelvic undersuit',[(.84,.115,.08,0),(.94,.15,.10,0),(1.03,.125,.09,0)],0,'Hips')
shell('abdominal undersuit',[(.99,.12,.08,0),(1.11,.125,.084,0),(1.24,.16,.10,0)],0,'Spine')
shell('thoracic armor',[(1.15,.14,.086,0),(1.26,.205,.12,0),(1.38,.213,.115,0),(1.44,.142,.09,0)],1,'Chest')
limb('sealed neck',(0,1.40,0),(0,1.54,0),.056,0,'Head')
ell('smooth enclosed helmet',(0,1.643,.005),(.115,.156,.110),1,'Head',40,24)
ell('continuous black visor',(0,1.647,.082),(.085,.119,.045),4,'Head',36,24)
for sign,side in [(-1,'L'),(1,'R')]:
 plate('temple shell',[(sign*.076,1.75),(sign*.12,1.70),(sign*.122,1.58),(sign*.082,1.51),(sign*.062,1.56),(sign*.089,1.64)],.071,.018,2,'Head')
 tube('cyan temple sensor',[(sign*.098,1.62,.096),(sign*.084,1.565,.102)],.004,3,'Head',8)
 plate('cheek armor',[(sign*.032,1.56),(sign*.091,1.57),(sign*.084,1.515),(sign*.036,1.49)],.092,.016,1,'Head')
 plate('pectoral plate',[(sign*.012,1.40),(sign*.094,1.43),(sign*.187,1.35),(sign*.174,1.245),(sign*.041,1.235),(sign*.012,1.28)],.119,.024,2,'Chest')
 tube('chest cyan strip',[(sign*.019,1.393,.153),(sign*.019,1.31,.153)],.004,3,'Chest',8)
 for j in range(3):
  y=1.22-j*.064
  plate('articulated abdomen plate',[(sign*.01,y),(sign*.12,y+.022),(sign*.139,y-.014),(sign*.084,y-.045),(sign*.008,y-.043)],.097,.019,1,'Spine')
  tube('abdomen circuit',[(sign*.095,y-.035,.121),(sign*.127,y-.01,.119)],.0028,3,'Spine',6)
 plate('pelvis petal',[(sign*.018,1.01),(sign*.123,1.025),(sign*.148,.925),(sign*.080,.868),(sign*.024,.909)],.106,.02,1,'Hips')
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 ell('shoulder rotary joint',a,(.094,.096,.093),0,upper)
 ell('sculpted shoulder cap',a+[sign*.024,.025,-.005],(.112,.105,.100),1,upper)
 ring('shoulder bearing',a+[0,0,.086],.055,.007,2,upper)
 tube('shoulder light slash',[a+[-.033,-.019,.098],a+[.033,.024,.098]],.003,3,upper)
 limb('sealed upper arm',a,e,.077,0,upper)
 mid=(a+e)*.5;x=mid[0];y=mid[1]
 plate('bicep layered shell',[(x-.06,y+.08),(x+.06,y+.08),(x+.055,y-.07),(x,y-.10),(x-.055,y-.07)],.067,.02,1,upper)
 ell('elbow seal',e,(.065,.063,.063),0,fore)
 limb('sealed forearm',e,w,.065,0,fore)
 mid=(e+w)*.5;x=mid[0];y=mid[1]
 plate('forearm instrument shield',[(x-.056,y+.082),(x+.057,y+.082),(x+.051,y-.075),(x,y-.108),(x-.045,y-.075)],.068,.022,2,fore)
 plate('forearm black inset',[(x-.034,y+.043),(x+.034,y+.043),(x+.03,y-.06),(x-.026,y-.06)],.091,.011,1,fore)
 tube('wrist cyan display',[(x-.026,y-.046,.107),(x+.026,y-.046,.107)],.004,3,fore,8)
 palm=w+[sign*.014,-.049,.025]
 ell('sealed glove',palm,(.039,.051,.031),0,hand,20,12)
 for j in range(4):
  x=palm[0]+(j-1.5)*.017
  tube('articulated finger',[(x,palm[1]-.015,palm[2]+.025),(x,palm[1]-.064,palm[2]+.037),(x,palm[1]-.076,palm[2]+.012)],[.009,.008,.005],2,hand,8)
 tube('opposing thumb',[palm+[-sign*.027,.005,.017],palm+[-sign*.043,-.025,.046],palm+[-sign*.033,-.044,.052]],.01,1,hand,8)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 limb('thigh undersuit',h,k,.073,0,thigh);limb('shin undersuit',k,f,.060,0,shin)
 for name,bone,top,bottom,width in [('thigh',thigh,h,k,.06),('shin',shin,k,f,.047)]:
  x=(top[0]+bottom[0])*.5
  plate(name+' shell',[(x-width,top[1]-.06),(x+width,top[1]-.06),(x+width*.85,bottom[1]+.11),(x,bottom[1]+.065),(x-width*.85,bottom[1]+.11)],.064,.018,1,bone)
  tube(name+' cyan edge',[(x+sign*width*.75,top[1]-.10,.084),(x+sign*width*.65,bottom[1]+.14,.084)],.0028,3,bone,6)
 ell('knee cap',k+[0,0,.035],(.06,.066,.033),2,shin)
 ell('armored boot',f+[0,-.057,.061],(.064,.048,.122),1,foot,24,12)
 for j in range(3):tube('boot toe tread',[f+[-.051,-.034,.065+j*.025],f+[.051,-.034,.065+j*.025]],.003,2,foot,6)
 # Back-mounted recessed reactor and cooling ribs are visible in play.
 for j in range(5):tube('back cooling vane',[(sign*.025,1.23+j*.034,-.115),(sign*.14,1.23+j*.034,-.099)],.008,2,'Chest',8)
 tube('back cyan conduit',[(sign*.045,1.17,-.10),(sign*.055,1.40,-.114)],.003,3,'Chest',8)
plate('chin bridge',[(-.036,1.53),(.036,1.53),(.033,1.485),(-.033,1.485)],.106,.019,2,'Head')

vertices=np.array(V);triangles=np.array(F,dtype=np.int32);normals=np.zeros_like(vertices)
for i in range(len(triangles)):
 ids=triangles[i];a,b,c=vertices[ids];n=np.cross(b-a,c-a)
 for j in ids:normals[j]+=n
normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-10)
mesh={'version':1,'boneNames':BN,'anchors':[{'head':list(BONES[n][0]),'tail':list(BONES[n][1])} for n in BN],
'vertices':np.round(vertices,6).ravel().tolist(),'normals':np.round(normals,6).ravel().tolist(),'boneIndices':BI,
'submeshes':[{'triangles':triangles[np.array(MI)==m].ravel().tolist()} for m in range(5)]}
(OUT/'Model.bytes').write_bytes(gzip.compress(json.dumps(mesh,separators=(',',':')).encode(), mtime=0))
report={'vertices':len(V),'triangles':len(F),'materials':5,'bones':len(BN),'parts':len(PARTS),'bounds':[vertices.min(axis=0).tolist(),vertices.max(axis=0).tolist()]}
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2))
assert np.isfinite(vertices).all() and np.isfinite(normals).all()
assert triangles.min()>=0 and triangles.max()<len(V)
assert len(BI)==len(V) and max(BI)<len(BN)
assert len(V)<45000 and len(F)<50000
print(json.dumps(report),flush=True)

# Offline rasterizer: render exact exported geometry with interpolated normals.
def render(yaw,name):
 w,h=640,920;scale=420;angle=math.radians(yaw);tilt=math.radians(-5)
 ry=np.array([[math.cos(angle),0,math.sin(angle)],[0,1,0],[-math.sin(angle),0,math.cos(angle)]])
 rx=np.array([[1,0,0],[0,math.cos(tilt),-math.sin(tilt)],[0,math.sin(tilt),math.cos(tilt)]])
 rotation=rx@ry;p=(vertices-np.array([0,.91,0]))@rotation.T;n=normals@rotation.T
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
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'CHRONO TRAVELER / EXOSUIT',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-22,'front.png');render(155,'back.png')
