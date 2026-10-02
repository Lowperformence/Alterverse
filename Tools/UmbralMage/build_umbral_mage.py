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
COLORS=np.array([[.016,.020,.028],[.035,.048,.050],[.38,.42,.45],[.40,.12,.82],[.075,.057,.095]])

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

shell('under-robe hips',[(.90,.123,.083,0),(1.01,.150,.097,0),(1.12,.122,.083,0)],0,'Hips')
shell('corset dark base',[(1.075,.125,.083,0),(1.17,.107,.081,0),(1.28,.125,.089,0),(1.34,.151,.10,0)],0,'Spine')
shell('chest dark armor',[(1.28,.132,.09,0),(1.38,.166,.111,0),(1.46,.173,.101,0),(1.52,.12,.069,0),(1.56,.069,.055,0)],1,'Chest')
limb('hood neck',(0,1.51,0),(0,1.66,0),.051,0,'Head')
# Deep empty face surrounded by a peaked cloth hood, not a visible human face.
ell('hood inner shadow',(0,1.771,.007),(.102,.142,.095),0,'Head',24,14)
# Outer hood back and pointed crown. Opening created as a rim rather than overlaid face texture.
verts=[];faces=[];levels=[(1.61,.089,.070,-.032),(1.69,.136,.105,-.025),(1.82,.125,.107,-.024),(1.93,.072,.066,-.02),(2.005,.006,.008,-.006)]
for y,rx,rz,cz in levels:
 for j in range(25):
  a=.02+math.pi*j/24;verts.append([rx*math.cos(a),y,cz-rz*math.sin(a)])
for row in range(4):
 for j in range(24):
  a=row*25+j;faces.extend([[a,a+25,a+26],[a,a+26,a+1]])
# Surface points outward to the back.
add('peaked hood back',verts,faces,1,'Head')
for side,s in [('L',-1),('R',1)]:
 panel('hood sculpted edge',[(0,1.985),(s*.077,1.916),(s*.126,1.828),(s*.13,1.711),(s*.068,1.616),(s*.048,1.69),(s*.082,1.782),(s*.061,1.875)],.045,.019,'Head')
 tube('hood silver piping',[(0,1.979,.049),(s*.076,1.90,.069),(s*.107,1.80,.077),(s*.099,1.715,.069),(s*.063,1.651,.057)],.004,2,'Head')
 # Two dim, narrow eye slits under the shadowed brow.
 tube('violet hood eye',[(s*.021,1.79,.091),(s*.050,1.79,.084)],.0028,3,'Head')
 panel('gothic chest plate',[(s*.013,1.509),(s*.077,1.534),(s*.161,1.473),(s*.153,1.368),(s*.069,1.328),(s*.012,1.38)],.104,.024,'Chest')
 tube('chest engraved arc',[(s*.028,1.477,.133),(s*.089,1.496,.133),(s*.125,1.445,.14),(s*.063,1.365,.132)],.003,2,'Chest')
 panel('rib armor',[(s*.033,1.335),(s*.112,1.344),(s*.123,1.245),(s*.067,1.207),(s*.025,1.264)],.087,.022,'Spine')
 for j in range(3):tube('corset seam',[(s*.105,1.303-j*.034,.107),(s*.06,1.278-j*.034,.114)],.004,2,'Spine')
 shard('collar thorn',(s*.085,1.50,.019),(s*.174,1.675,-.033),.022,.014,2,'Chest')
 # Short shoulder mantle ends at the waist; legs remain fully articulated.
 fabric('short shoulder mantle '+side,[(1.50,s*.015,s*.14,-.09),(1.26,s*.021,s*.17,-.13),(1.12,s*.03,s*.16,-.139)],0,'Chest')
 tube('short mantle silver edge',[(s*.137,1.46,-.107),(s*.165,1.26,-.146),(s*.16,1.12,-.155)],.003,2,'Chest')
# Silver-violet amulet and a filigree belt clasp.
ring('chest amulet frame',(0,1.459,.14),.029,.005,2,'Chest')
shard('chest violet talisman',(0,1.426,.15),(0,1.50,.15),.017,.010,3,'Chest')
shell('waist armored belt',[(1.087,.133,.096,0),(1.11,.134,.097,0),(1.132,.129,.09,0)],1,'Hips',24)
for s in [-1,1]:
 tube('belt silver scroll',[(s*.015,1.12,.111),(s*.067,1.15,.11),(s*.115,1.12,.084),(s*.11,1.09,.09),(s*.061,1.10,.107),(s*.018,1.084,.11)],.005,2,'Hips')
shard('belt star upper',(0,1.105,.119),(0,1.168,.119),.019,.012,2,'Hips')
shard('belt star lower',(0,1.11,.119),(0,1.044,.119),.019,.012,2,'Hips')
ell('belt gemstone',(0,1.108,.132),(.013,.021,.01),3,'Hips',12,8)
for side,s in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 limb('dark armored sleeve',a,e,.065,0,upper)
 panel('pointed pauldron',[(s*.126,1.56),(s*.187,1.644),(s*.254,1.585),(s*.323,1.459),(s*.238,1.468),(s*.181,1.413)],.045,.040,upper)
 panel('pauldron inset',[(s*.175,1.57),(s*.209,1.599),(s*.262,1.54),(s*.249,1.482),(s*.202,1.483)],.078,.022,upper)
 shard('upper shoulder thorn',a+[s*.044,.04,-.02],a+[s*.082,.205,-.046],.026,.025,2,upper)
 shard('outer shoulder thorn',a+[s*.098,.0,-.025],a+[s*.175,.105,-.056],.021,.02,2,upper)
 shard('rear shoulder thorn',a+[s*.038,.037,-.055],a+[s*.08,.116,-.174],.018,.025,2,upper)
 for j in range(3):
  y=1.445-j*.045
  panel('upper sleeve lamella',[(s*.205,y+.017),(s*.282,y),(s*.299,y-.063),(s*.228,y-.033)],.046,.019,upper)
 ell('elbow joint',e,(.052,.049,.052),0,fore,16,8)
 limb('lower sleeve',e,w,.052,0,fore)
 p=(e+w)*.5
 panel('gothic vambrace',[(p[0]-.045,p[1]+.108),(p[0]+.045,p[1]+.089),(p[0]+.052,p[1]-.062),(p[0],p[1]-.117),(p[0]-.045,p[1]-.065)],p[2]+.038,.027,fore)
 shard('forearm blade',e+[s*.034,-.025,0],e+[s*.092,.102,-.018],.023,.018,2,fore)
 tube('vambrace silver ridge',[p+[0,.078,.066],p+[s*.019,0,.077],p+[0,-.080,.066]],.003,2,fore)
 palm=w+[s*.01424,-.05341,.02576]
 ell('dark glove',palm,(.037,.050,.033),0,hand,18,10)
 panel('glove back plate',[(palm[0]-.029,palm[1]+.025),(palm[0]+.029,palm[1]+.025),(palm[0]+.032,palm[1]-.024),(palm[0],palm[1]-.048),(palm[0]-.032,palm[1]-.024)],palm[2]-.026,.014,hand)
 for j in range(4):
  y=palm[1]+(j-1.5)*.017
  tube('segmented glove finger',[(palm[0]-s*.021,y,palm[2]),(palm[0]-s*.03,y,palm[2]+.035),(palm[0]+s*.026,y,palm[2]+.039),(palm[0]+s*.027,y,palm[2]+.013)],.0067,2,hand,8)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 limb('leggings',h,k,.069,0,thigh);limb('high armored boot',k,f,.057,1,shin)
 x=(h[0]+k[0])*.5
 panel('light thigh plate '+side,[(x-.054,h[1]-.08),(x+.054,h[1]-.08),(x+.048,k[1]+.14),(x,k[1]+.09),(x-.048,k[1]+.14)],.065,.015,thigh)
 x=k[0]
 panel('articulated knee guard '+side,[(x-.045,k[1]+.04),(x+.045,k[1]+.04),(x+.049,k[1]-.02),(x,k[1]-.067),(x-.049,k[1]-.02)],.058,.016,shin)
 x=(k[0]+f[0])*.5
 panel('light shin guard '+side,[(x-.043,k[1]-.10),(x+.043,k[1]-.10),(x+.035,f[1]+.07),(x,f[1]+.035),(x-.035,f[1]+.07)],.044,.014,shin)
 tube('violet shin inlay '+side,[(x,k[1]-.13,.069),(x,f[1]+.10,.069)],.0026,3,shin)
 ell('boot toe',f+[0,-.064,.071],(.061,.041,.111),1,foot,20,10)
 tube('boot silver welt',[f+[-.05,-.076,.116],f+[-.026,-.076,.173],f+[.03,-.076,.17],f+[.054,-.076,.11]],.004,2,foot)
# Original violet crystal staff, separated from anatomy for shop colorways.
GROUP=2;bone='WeaponGripR';p=np.array(BONES['HandR'][0])+[.01424,-.05341,.02576];x=p[0];z=p[2]+.031
shaftLow=.19;shaftHigh=1.85
tube('obsidian staff shaft',[(x,shaftLow,z),(x,shaftHigh,z)],.019,0,bone,12)
for y in np.linspace(.23,1.76,14):
 ring('staff etched silver collar',(x,y,z),.020,.003,2,bone,(0,1,0),16)
for y in np.linspace(p[1]-.07,p[1]+.07,8):ring('staff grip wrap',(x,y,z),.023,.004,4,bone,(0,1,0),16)
shard('staff lower violet shard',(x,.145,z),(x,.29,z),.028,.021,3,bone)
for s in [-1,1]:
 tube('twisted staff crown',[(x+s*.01,1.75,z),(x+s*.066,1.82,z+.013),(x+s*.081,1.98,z),(x+s*.045,2.083,z)], [.019,.027,.024,.005],2,bone,9)
 shard('crown outer thorn',(x+s*.053,1.839,z),(x+s*.145,1.939,z-.014),.028,.020,1,bone)
 shard('crown lower thorn',(x+s*.032,1.80,z),(x+s*.114,1.755,z),.019,.015,2,bone)
shard('large amethyst focus',(x,1.827,z),(x+.013,2.096,z),.050,.038,3,bone)
shard('violet focus highlight',(x-.018,1.861,z+.033),(x-.009,2.060,z+.017),.008,.005,2,bone)
ring('crystal socket',(x,1.83,z),.042,.008,1,bone,(0,1,0),20)
for s in [-1,1]:
 tube('staff silver spiral',[(x+s*.025,1.65,z),(x+s*.04,1.715,z+.015),(x+s*.026,1.76,z+.028)],.006,2,bone)

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
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'UMBRAL MAGE / IONE VASKA',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-14,'front.png');render(158,'back.png')
