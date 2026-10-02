"""Original umbral-mage mesh; NumPy/Pillow only. +Y up, +Z forward."""
from pathlib import Path
import json, math, gzip
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/UmbralMage'
ART=ROOT/'ArtSource/Heroes/UmbralMage'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
BONES={
'Hips':((0,.981,0),(0,1.1227,0)),
'Spine':((0,1.1227,0),(0,1.308,0)),
'Chest':((0,1.308,0),(0,1.5478,0)),
'Head':((0,1.5478,0),(0,1.9075,0))}
for side,sign in [('L',-1),('R',1)]:
 BONES['UpperArm'+side]=((sign*.16465,1.54453,-.01564),(sign*.24831,1.2644,-.02116))
 BONES['Forearm'+side]=(BONES['UpperArm'+side][1],(sign*.33553,1.03332,.02576))
 BONES['Hand'+side]=(BONES['Forearm'+side][1],(sign*.35155,.9374,.04784))
 BONES['Thigh'+side]=((sign*.1248,.981,0),(sign*.1344,.5341,-.01))
 BONES['Shin'+side]=(BONES['Thigh'+side][1],(sign*.1728,.1308,-.02))
 BONES['Foot'+side]=(BONES['Shin'+side][1],(sign*.1728,.0654,.15))
 BONES['WeaponGrip'+side]=BONES['Hand'+side]
BN=list(BONES);V=[];F=[];MI=[];BI=[];PARTS=[];GI=[];GROUP=0
COLORS=np.array([[.010,.013,.019],[.032,.043,.064],[.30,.34,.42],[.33,.29,.64],[.30,.014,.027]])

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
 # Orient the beveled surface outward; negative depth faces the back.
 for f in faces:
  a,b,c=[np.array(verts[i]) for i in f]
  if np.cross(b-a,c-a)[2]*depth<0:f.reverse()
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

# Reference-led iron hierophant: broad silver shoulders, narrow core, black face and red pennants.
shell('sealed pelvis',[(.86,.110,.077,0),(.98,.140,.095,0),(1.10,.102,.071,0)],0,'Hips')
shell('narrow abdominal core',[(1.04,.10,.07,0),(1.22,.091,.077,0),(1.36,.118,.09,0)],0,'Spine')
shell('angular thorax',[(1.27,.11,.078,0),(1.40,.15,.10,0),(1.51,.157,.087,0),(1.57,.082,.058,0)],1,'Chest')
limb('armored neck',(0,1.48,0),(0,1.69,0),.052,0,'Head')
# Small recessed faceless helm, not the old broad fabric hood.
ell('helm dark rear',(0,1.77,-.012),(.085,.135,.081),1,'Head',24,16)
plate('recessed triangular face',[(-.045,1.832),(0,1.88),(.045,1.832),(.053,1.738),(0,1.699),(-.053,1.738)],.074,.012,0,'Head')
for side,s in [('L',-1),('R',1)]:
 plate('folded helmet edge',[(s*.007,1.938),(s*.041,1.908),(s*.075,1.818),(s*.080,1.70),(s*.049,1.661),(s*.033,1.702),(s*.054,1.778),(s*.025,1.844)],.068,.029,2,'Head')
 plate('helm cheek ridge',[(s*.057,1.79),(s*.099,1.827),(s*.089,1.713),(s*.045,1.665),(s*.066,1.744)],.075,.02,1,'Head')
 tube('helm steel engraving',[(s*.019,1.896,.091),(s*.049,1.815,.100),(s*.054,1.731,.099)],.0026,2,'Head',6)
 # Reference's straight ritual crossbar and two red pendant banners.
 tube('ritual head crossbar',[(0,1.908,-.030),(s*.36,1.908,-.030)],.006,2,'Head',8)
 fabric('crimson head pennant '+side,[(1.899,s*.277,s*.332,-.029),(1.846,s*.278,s*.333,-.025),(1.785,s*.28,s*.333,-.023)],4,'Head')
 plate('clavicle sculpted blade',[(s*.061,1.60),(s*.129,1.579),(s*.166,1.491),(s*.129,1.425),(s*.088,1.462),(s*.055,1.534)],.065,.038,2,'Chest')
 tube('red collar tendon',[(s*.052,1.498,.098),(s*.082,1.522,.092),(s*.111,1.486,.092)],.003,4,'Chest',6)
 for j in range(3):
  y=1.41-j*.080
  plate('rib blade',[(s*.099,y+.028),(s*.146,y+.067),(s*.143,y-.016),(s*.103,y-.062),(s*.081,y-.029)],.075,.032,2,'Chest' if j==0 else 'Spine')
  ring('recessed rib fastener',(s*.118,y+.012,.099),.014,.003,1,'Chest' if j==0 else 'Spine',segments=16)
 plate('pelvic crest',[(s*.015,1.17),(s*.074,1.213),(s*.127,1.148),(s*.095,1.044),(s*.040,.965),(s*.013,1.038)],.089,.03,2,'Hips')
 plate('side hip blade',[(s*.109,1.099),(s*.171,1.104),(s*.179,.991),(s*.129,.945),(s*.111,1.013)],.012,.03,1,'Hips')
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 limb('upper arm black sleeve',a,e,.059,0,upper)
 # Broad overlapping, sloping shells follow the shoulder joint.
 plate('massive sloping pauldron',[(s*.125,1.625),(s*.199,1.642),(s*.296,1.574),(s*.378,1.46),(s*.353,1.382),(s*.283,1.420),(s*.217,1.502),(s*.155,1.504)],.045,.078,2,upper)
 plate('pauldron lower dark lip',[(s*.250,1.502),(s*.345,1.472),(s*.365,1.396),(s*.309,1.351),(s*.276,1.419)],.089,.034,1,upper)
 plate('pauldron crown lamella',[(s*.145,1.623),(s*.19,1.650),(s*.252,1.608),(s*.227,1.558),(s*.176,1.573)],.096,.022,2,upper)
 for j in range(2):
  ring('shoulder rivet',(s*(.181+j*.050),1.602-j*.038,.12),.011,.003,1,upper,segments=16)
 p=(a+e)*.5
 tube('red upper arm conduit',[a+[s*.019,-.065,.064],p+[s*.017,0,.064],e+[s*.015,.025,.062]],.003,4,upper,6)
 ell('elbow mechanical seal',e,(.049,.050,.050),0,fore,16,10)
 limb('long black forearm',e,w,.050,0,fore)
 p=(e+w)*.5
 plate('swept forearm armor',[(p[0]-s*.038,p[1]+.114),(p[0]+s*.050,p[1]+.092),(p[0]+s*.070,p[1]-.021),(p[0]+s*.025,p[1]-.116),(p[0]-s*.031,p[1]-.073)],p[2]+.033,.030,1,fore)
 tube('forearm sharp silver edge',[p+[-s*.039,.100,.059],p+[-s*.035,.017,.065],p+[s*.020,-.098,.063]],.003,2,fore,6)
 shard('elbow trailing blade',e+[s*.012,-.075,-.007],e+[s*.152,-.255,-.025],.043,.022,1,fore)
 palm=w+[s*.01424,-.05341,.02576]
 ell('armored palm',palm,(.039,.050,.035),0,hand,18,12)
 for j in range(4):
  y=palm[1]+(j-1.5)*.019
  tube('closed armored grip finger',[(palm[0]-s*.024,y,palm[2]),(palm[0]-s*.034,y,palm[2]+.038),(palm[0]+s*.028,y,palm[2]+.042),(palm[0]+s*.030,y,palm[2]+.012)],.007,1,hand,8)
 if side=='L':
  for j in range(3):
   base=palm+[(j-1)*.022,-.008,.031]
   tube('left hand crystal talon',[base,base+[(j-1)*.014,.016,.067],base+[(j-1)*.021,.075,.096]],[.010,.014,.001],3,hand,8)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 limb('slender thigh underarmor',h,k,.063,0,thigh);limb('slender shin underarmor',k,f,.050,0,shin)
 for j in range(2):
  y=.883-j*.145;x=(h[0]+k[0])*.5
  plate('layered thigh blade',[(x-s*.061,y+.084),(x+s*.092,y+.139),(x+s*.087,y+.041),(x+s*.028,y-.069),(x-s*.04,y-.055)],.027,.039,1,thigh)
  tube('thigh steel etching',[(x-s*.041,y+.065,.048),(x+s*.059,y+.086,.067),(x+s*.032,y-.03,.055)],.0025,2,thigh,6)
 ell('recessed knee joint',k,(.046,.05,.041),0,shin,18,10)
 ring('knee hinge rim',k+[0,0,.047],.027,.005,2,shin,segments=20)
 for j in range(3):
  y=.481-j*.112;x=k[0]+(f[0]-k[0])*(j+.5)/3
  plate('jagged greave segment',[(x-s*.047,y+.06),(x+s*.086,y+.117),(x+s*.064,y+.012),(x+s*.030,y-.081),(x-s*.038,y-.050)],.038,.031,1,shin)
  shard('lateral shin barb',(x+s*.034,y-.018,.015),(x+s*.135,y+.055,-.025),.025,.022,1,shin)
 ell('boot ankle',f+[0,-.042,.009],(.049,.048,.064),0,foot,20,10)
 shard('pointed armored toe',f+[0,-.028,.021],f+[s*.023,-.085,.222],.055,.034,2,foot)
 # Split rigid ceremonial strips replace fabric and follow each leg independently.
 plate('long segmented front strip',[(s*.011,1.027),(s*.050,1.011),(s*.044,.61),(s*.014,.58)],.137,.012,1,thigh)
 plate('lower front strip',[(s*.014,.61),(s*.044,.61),(s*.050,.195),(s*.018,.17)],.132,.012,1,shin)
 tube('front strip silver edge',[(s*.048,.60,.145),(s*.053,.20,.145)],.0024,2,shin,6)
 # Back is plated rather than a hanging cloak.
 for j in range(4):
  y=1.29+j*.066
  plate('rear vertebral plate',[(s*.012,y+.02),(s*.118,y+.036),(s*.105,y-.022),(s*.015,y-.018)],-.10,-.019,1,'Chest')
  tube('rear red tendon',[(s*.063,y,-.121),(s*.092,y+.020,-.117)],.0023,4,'Chest',6)
plate('central silver pelvic point',[(-.028,1.17),(0,1.205),(.028,1.17),(.022,1.005),(0,.958),(-.022,1.005)],.12,.027,2,'Hips')
# Reference's slim staff and compact dark, bladed focus head.
GROUP=2;bone='WeaponGripR';p=np.array(BONES['HandR'][0])+[.01424,-.05341,.02576];x=p[0];z=p[2]+.031
shaftLow=.13;shaftHigh=1.78
# A thin segmented shaft retains the existing palm contact.
tube('staff black shaft',[(x,shaftLow,z),(x,shaftHigh,z)],.012,0,bone,12)
for y in [.18,.46,.79,1.16,1.45,1.73]:ring('staff narrow collar',(x,y,z),.014,.003,2,bone,(0,1,0),16)
for y in np.linspace(p[1]-.064,p[1]+.064,7):ring('staff grip winding',(x,y,z),.017,.003,1,bone,(0,1,0),16)
for s in [-1,1]:
 plate('dark staff crescent',[(x+s*.012,1.885),(x+s*.069,1.933),(x+s*.115,1.894),(x+s*.103,1.841),(x+s*.124,1.808),(x+s*.062,1.765),(x+s*.01,1.786)],z,.032,1,bone)
 tube('staff head inset edge',[(x+s*.026,1.867,z+.027),(x+s*.065,1.91,z+.027),(x+s*.093,1.881,z+.027),(x+s*.083,1.826,z+.027)],.0027,2,bone,6)
shard('small pale violet staff crystal',(x,1.793,z+.026),(x,1.93,z+.026),.024,.019,3,bone)
shard('staff pointed ferrule',(x,.22,z),(x,.065,z),.014,.012,2,bone)

vertices=np.array(V);triangles=np.array(F,dtype=np.int32);normals=np.zeros_like(vertices)
for i in range(len(triangles)):
 ids=triangles[i];a,b,c=vertices[ids];n=np.cross(b-a,c-a)
 for j in ids:normals[j]+=n
normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-10)
mesh={'version':1,'boneNames':BN,'anchors':[{'head':list(BONES[n][0]),'tail':list(BONES[n][1])} for n in BN],
'vertices':np.round(vertices,6).ravel().tolist(),'normals':np.round(normals,6).ravel().tolist(),'boneIndices':BI,'groups':GI,'markers':[{'name':'StaffGrip','bone':'WeaponGripR','position':[x,float(p[1]),z]},{'name':'StaffTop','bone':'WeaponGripR','position':[x,1.96,z]},{'name':'StaffFront','bone':'WeaponGripR','position':[x,1.96,z+.1]}],
'submeshes':[{'triangles':triangles[np.array(MI)==m].ravel().tolist()} for m in range(5)]}
(OUT/'Model.bytes').write_bytes(gzip.compress(json.dumps(mesh,separators=(',',':')).encode(), mtime=0))
report={'vertices':len(V),'triangles':len(F),'materials':5,'bones':len(BN),'parts':len(PARTS),'bounds':[vertices.min(axis=0).tolist(),vertices.max(axis=0).tolist()]}
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2))
assert np.isfinite(vertices).all() and np.isfinite(normals).all()
assert triangles.min()>=0 and triangles.max()<len(V)
assert len(BI)==len(V) and max(BI)<len(BN)
assert len(V)<65000 and len(F)<90000
assert len(GI)==len(V) and set(GI)=={0,2}
assert all(len(set(GI[i] for i in f))==1 for f in F)
print(json.dumps(report),flush=True)

# Offline rasterizer: render exact exported geometry with interpolated normals.
def render(yaw,name):
 w,h=700,920;scale=370;angle=math.radians(yaw);tilt=math.radians(-5)
 ry=np.array([[math.cos(angle),0,math.sin(angle)],[0,1,0],[-math.sin(angle),0,math.cos(angle)]])
 rx=np.array([[1,0,0],[0,math.cos(tilt),-math.sin(tilt)],[0,math.sin(tilt),math.cos(tilt)]])
 rotation=rx@ry;p=(vertices-np.array([0,1.015,0]))@rotation.T;n=normals@rotation.T
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
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'IONE VASKA / IRON HIEROPHANT',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-14,'front.png');render(158,'back.png')

