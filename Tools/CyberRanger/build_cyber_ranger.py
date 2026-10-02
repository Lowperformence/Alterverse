"""Original cyber-sniper geometry, referenced to Nyra's existing rest skeleton.
No Blender dependency. Exports indexed meshes used by CyberRangerAppearance and
renders the same triangles for geometry review (not a Unity/game screenshot).
"""
from pathlib import Path
import json, math, gzip
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/CyberRanger'
ART=ROOT/'ArtSource/Heroes/CyberRanger'
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
COLORS=np.array([[.028,.041,.044],[.080,.115,.120],[.46,.57,.57],[.025,.65,.36],[.007,.018,.016]])
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

# Tailored female combat silhouette, all surfaces closed or overlapping at joints.
shell('pelvic armored suit',[(.86,.116,.086,0),(.92,.149,.100,0),(1.005,.131,.094,0),(1.065,.110,.079,0)],0,'Hips')
shell('flexible waist',[(1.00,.123,.087,0),(1.08,.104,.079,0),(1.15,.112,.083,0),(1.225,.141,.092,0)],0,'Spine')
shell('upper suit',[(1.17,.124,.086,0),(1.25,.157,.102,0),(1.34,.175,.113,-.002),(1.41,.146,.085,0),(1.448,.087,.065,0)],0,'Chest')
limb('neck actuator',(0,1.42,0),(0,1.565,0),.052,4,'Head')
for y in [1.457,1.477,1.497,1.517]:ring('neck titanium vertebra',(0,y,0),.048,.004,2,'Head',(0,1,0),24)
# Smooth protective skull; silver orbital cage and angular ventilated respirator.
ell('graphite skull',(0,1.661,-.002),(.107,.144,.100),1,'Head',32,18)
ell('temple casing L',(-.102,1.663,-.003),(.022,.056,.046),2,'Head')
ell('temple casing R',(.102,1.663,-.003),(.022,.056,.046),2,'Head')
for side in [-1,1]:
 ring('temple recess',(side*.118,1.664,.006),.023,.005,4,'Head',(side,0,0),24)
 ell('green temple insert',(side*.120,1.664,.006),(.006,.017,.017),3,'Head',16,8)
 plate('cheekbone mask',[(side*.018,1.65),(side*.066,1.676),(side*.094,1.653),(side*.076,1.602),(side*.037,1.583)],.082,.024,2,'Head')
 tube('jaw seam',[(side*.080,1.619,.075),(side*.066,1.581,.074),(side*.025,1.551,.060)],.006,2,'Head')
 tube('brow ridge',[(side*.010,1.713,.088),(side*.041,1.724,.089),(side*.078,1.710,.076)],.007,2,'Head')
# Asymmetric targeting monocle: three machined rings, black recess, emerald lens.
ell('left optical well',(-.045,1.68,.09),(.046,.046,.022),4,'Head')
ring('monocle silver rim',(-.045,1.68,.118),.036,.007,2,'Head')
ring('monocle dark bevel',(-.045,1.68,.124),.026,.006,1,'Head')
ring('monocle emerald reticle',(-.045,1.68,.133),.020,.0035,3,'Head')
ell('monocle lens',(-.045,1.68,.129),(.015,.015,.006),3,'Head',20,10)
ell('monocle pupil',(-.045,1.68,.136),(.009,.009,.003),4,'Head',16,8)
ell('right optical recess',(.041,1.681,.091),(.027,.018,.012),4,'Head')
tube('right narrow green eye',[(.022,1.681,.107),(.040,1.683,.110),(.060,1.681,.103)],.003,3,'Head')
plate('nose bridge',[(-.011,1.675),(.011,1.675),(.020,1.634),(0,1.622),(-.020,1.634)],.112,.018,2,'Head')
ell('respirator black backing',(0,1.593,.094),(.042,.036,.025),4,'Head')
for j in range(-2,3):
 x=j*.012;tube('vertical respirator blade',[(x*.7,1.564,.103),(x,1.593,.126),(x*.8,1.619,.115)],.0035,2,'Head')
for x in [-.013,.013]:tube('respirator energy slit',[(x,1.574,.120),(x,1.605,.129)],.0025,3,'Head')
for x,y in [(-.035,1.747),(0,1.757),(.035,1.747)]:
 ring('forehead sensor bezel',(x,y,.078),.012,.003,2,'Head',segments=20)
 ell('forehead sensor',(x,y,.079),(.008,.008,.004),3,'Head',12,8)
# Curved breastplate with framing similar to the reference, not enlarged anatomy.
for side in [-1,1]:
 ell('silver pectoral frame',(side*.078,1.318,.098),(.087,.090,.050),2,'Chest',28,14)
 ell('black curved pectoral',(side*.078,1.322,.108),(.077,.079,.049),1,'Chest',28,14)
 tube('chest lower silver sweep',[(side*.156,1.282,.100),(side*.116,1.245,.112),(side*.060,1.232,.118),(side*.020,1.259,.110)],.006,2,'Chest')
 tube('clavicle silver edge',[(0,1.412,.063),(side*.070,1.423,.067),(side*.148,1.408,.059)],.008,2,'Chest')
 tube('clavicle green inset',[(side*.028,1.401,.080),(side*.087,1.410,.078),(side*.132,1.399,.074)],.003,3,'Chest')
ell('sternum silver mount',(0,1.236,.103),(.025,.034,.016),2,'Chest')
ell('sternum emerald',(0,1.237,.119),(.017,.025,.009),3,'Chest')
plate('abdominal contoured plate',[(-.084,1.195),(0,1.213),(.084,1.195),(.077,1.085),(0,1.054),(-.077,1.085)],.084,.018,1,'Spine')
for side in [-1,1]:
 tube('abdominal silver sweep',[(side*.072,1.184,.103),(side*.045,1.158,.112),(side*.038,1.107,.113),(0,1.07,.108)],.008,2,'Spine')
 tube('side armor silver sweep',[(side*.091,1.184,.075),(side*.108,1.124,.073),(side*.075,1.067,.088)],.005,2,'Spine')
 for j in range(3):tube('rib articulation',[(side*.118,1.208-j*.025,.077),(side*.090,1.196-j*.025,.101)],.0045,2,'Spine')
ell('abdominal emerald cell',(0,1.10,.11),(.014,.024,.009),3,'Spine')
# Hip plates, functional belt and power cell instead of bulky skirt/cape.
shell('titanium waist belt',[(.994,.139,.100,0),(1.013,.137,.100,0),(1.032,.128,.095,0)],1,'Hips')
ring('belt reactor housing',(0,1.013,.107),.028,.006,2,'Hips')
ell('belt dark core',(0,1.013,.115),(.022,.022,.010),4,'Hips')
ring('belt reactor status',(0,1.013,.127),.014,.0025,3,'Hips',segments=24)
for side in [-1,1]:
 plate('segmented hip plate',[(side*.07,1.00),(side*.144,.975),(side*.151,.884),(side*.090,.873)],.093,.018,2,'Hips')
 plate('hip graphite inset',[(side*.090,.980),(side*.132,.966),(side*.137,.902),(side*.103,.895)],.108,.014,1,'Hips')
# Back detailing makes the design work in third-person play as well as the shop.
ell('back armored spine',(0,1.300,-.099),(.064,.119,.028),1,'Chest')
for j in range(5):
 y=1.218+j*.033
 plate('back vertebral frame',[(-.03,y-.008),(.03,y-.008),(.037,y+.01),(-.037,y+.01)],-.126,.010,2,'Chest')
 ell('rear green spine cell',(0,y,-.137),(.008,.006,.004),3,'Chest',12,6)
for side in [-1,1]:
 tube('back shoulder conduit',[(side*.155,1.395,-.055),(side*.110,1.363,-.105),(side*.066,1.267,-.116)],.008,2,'Chest')
 tube('back emerald tracer',[(side*.13,1.382,-.073),(side*.090,1.337,-.119),(side*.063,1.265,-.130)],.0027,3,'Chest')
# Articulated limbs with joints, low-profile guards and fine energy traces.
for side,sign in [('L',-1),('R',1)]:
 for name,radius in [('UpperArm',.045),('Forearm',.040),('Thigh',.076),('Shin',.053)]:
  bone=name+side;a=np.array(BONES[bone][0]);b=np.array(BONES[bone][1]);limb(name+' black suit '+side,a,b,radius,0,bone)
  center=a*.57+b*.43
  if name=='UpperArm':
   ell('shoulder underjoint '+side,a,(.047,.045,.046),4,bone)
   ell('silver shoulder plate '+side,a+np.array([sign*.010,-.012,0]),(.067,.043,.061),2,bone)
   ell('shoulder graphite inset '+side,a+np.array([sign*.013,-.010,.009]),(.055,.037,.058),1,bone)
  if name=='Forearm':
   ell('elbow titanium socket '+side,a,(.041,.037,.041),2,bone)
   ell('elbow emerald joint '+side,a+np.array([0,0,.032]),(.019,.022,.010),3,bone)
  # Circuit traces follow the limb surface, not floating full-bright sleeves.
  for line in range(2):
   pts=[]
   for t in np.linspace(.18,.83,8):
    q=a+(b-a)*t;q=q+np.array([math.sin(t*8+line*2)*radius*.44,0,radius*.83]);pts.append(q)
   tube('emerald etched circuit '+bone,pts,.0018 if name in ['UpperArm','Forearm'] else .0022,3,bone,6)
  if name in ['Forearm','Shin']:
   delta=b-a
   tube('bracer silver rail '+bone,[a+delta*t+np.array([sign*radius*.52,0,radius*.65]) for t in [.2,.38,.62,.84]],.004,2,bone)
   for t in [.30,.46,.62,.78]:
    q=a+delta*t+np.array([0,0,radius*.93]);tube('segmented bracer vent '+bone,[q+[-radius*.24,0,0],q+[radius*.24,0,0]],.003,2,bone)
  if name=='Thigh':
   q=a+(b-a)*.27
   ell('thigh satin inset '+side,q+np.array([sign*.020,0,.035]),(.052,.093,.043),1,bone)
  if name=='Shin':
   ell('knee silver bezel '+side,a+np.array([0,0,.035]),(.052,.047,.029),2,bone)
   ell('knee emerald enamel '+side,a+np.array([0,0,.048]),(.041,.035,.024),1,bone)
   tube('knee status trace '+side,[a+[-.019,.01,.073],a+[0,.02,.077],a+[.019,.01,.073]],.0023,3,bone)
 bone='Hand'+side;a=np.array(BONES[bone][0]);palm=a+np.array([sign*.012,-.040,.02])
 ell('tactical glove '+side,palm,(.035,.045,.029),0,bone)
 ell('silver glove back '+side,palm+np.array([0,.007,-.023]),(.029,.028,.008),2,bone)
 for finger in range(4):
  x=palm[0]+(finger-1.5)*.013
  pts=[(x,palm[1]-.011,palm[2]+.017),(x,palm[1]-.031,palm[2]+.035),(x,palm[1]-.043,palm[2]+.020),(x,palm[1]-.035,palm[2]+.004)]
  tube('articulated finger '+side,pts,[.006,.006,.0055,.004],1,bone,8)
  ell('finger knuckle '+side,pts[1],(.0065,.007,.0065),2,bone,12,6)
 tube('opposing glove thumb '+side,[palm+[sign*.028,0,.015],palm+[sign*.041,-.014,.038],palm+[sign*.015,-.023,.040]],[.008,.007,.005],1,bone)
 foot='Foot'+side;q=np.array(BONES[foot][0]);
 ell('armored ankle boot '+side,q+[0,-.014,.028],(.054,.081,.078),0,foot)
 ell('rounded combat toe '+side,q+[0,-.064,.087],(.062,.036,.098),1,foot)
 tube('boot sole titanium '+side,[q+[-.052,-.083,.12],q+[-.042,-.083,.168],q+[.042,-.083,.168],q+[.052,-.083,.12]],.0055,2,foot)
 tube('boot emerald seam '+side,[q+[-.027,-.038,.141],q+[.027,-.038,.141]],.0025,3,foot)

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
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'CYBER RANGER / NYRA',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-22,'front.png');render(155,'back.png')
