"""Original siege-tank mesh; NumPy/Pillow only. +Y up, +Z forward."""
from pathlib import Path
import json, math, gzip
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/SiegeTank'
ART=ROOT/'ArtSource/Heroes/SiegeTank'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
BONES={
'Hips':((0,.972,0),(0,1.1124,0)),
'Spine':((0,1.1124,0),(0,1.296,0)),
'Chest':((0,1.296,0),(0,1.5336,0)),
'Head':((0,1.5336,0),(0,1.89,0))}
for side,sign in [('L',-1),('R',1)]:
 BONES['UpperArm'+side]=((sign*.29315,1.5498,-.0078),(sign*.44044,1.269,.0026))
 BONES['Forearm'+side]=(BONES['UpperArm'+side][1],(sign*.57057,1.02708,.0793))
 BONES['Hand'+side]=(BONES['Forearm'+side][1],(sign*.59631,.93204,.1105))
 BONES['Thigh'+side]=((sign*.156,.972,0),(sign*.168,.5292,-.01))
 BONES['Shin'+side]=(BONES['Thigh'+side][1],(sign*.216,.1296,-.02))
 BONES['Foot'+side]=(BONES['Shin'+side][1],(sign*.216,.0648,.15))
 BONES['WeaponGrip'+side]=BONES['Hand'+side]
BN=list(BONES);V=[];F=[];MI=[];BI=[];PARTS=[];GI=[];GROUP=0
COLORS=np.array([[.028,.038,.031],[.18,.235,.135],[.38,.32,.19],[1.0,.32,.035],[.30,.36,.34],[.65,.08,1.0]])
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
 plate(name+' bronze rim',outline,z,depth,2,bone)
 xy=np.array(outline);c=xy.mean(axis=0)
 plate(name+' olive enamel',c+(xy-c)*.88,z+depth*.35,depth*.6,1,bone)
def scar(name,a,b,bone):tube(name,[a,b],.0022,4,bone,5)

shell('pelvis machinery',[(.86,.15,.105,0),(.96,.205,.135,0),(1.065,.20,.13,0),(1.13,.155,.115,0)],0,'Hips')
shell('segmented waist seal',[(1.045,.15,.12,0),(1.12,.165,.12,0),(1.24,.20,.14,0),(1.30,.22,.145,0)],0,'Spine')
shell('chest armored volume',[(1.23,.19,.145,0),(1.32,.245,.175,0),(1.43,.29,.178,0),(1.51,.25,.13,0),(1.57,.145,.105,0)],1,'Chest',24)
# Heavy collar frames a compact helmet, leaving no exposed neck.
ring('collar bronze seal',(0,1.55,0),.13,.021,2,'Chest',(0,1,0),32)
for side,s in [('L',-1),('R',1)]:
 panel('pectoral '+side,[(s*.018,1.51),(s*.17,1.555),(s*.277,1.486),(s*.267,1.343),(s*.105,1.32),(s*.018,1.37)],.159,.047,'Chest')
 bolts('chest rivet',[(s*.05,1.485,.204),(s*.218,1.468,.199),(s*.224,1.375,.201)],'Chest')
 for j in range(4):
  x=s*(.13+j*.025)
  tube('under-chest radiator',[(x,1.305,.156),(x,1.272,.159),(x+s*.012,1.248,.143)],.007,2,'Chest')
 for j in range(3):
  y=1.115+j*.042
  panel('waist overlapping plate',[(s*.025,y+.025),(s*.132,y+.039),(s*.146,y),(s*.06,y-.016)],.135,.025,'Spine')
 tube('abdominal hydraulic hose',[(s*.18,1.30,.13),(s*.188,1.20,.115),(s*.145,1.12,.105)],.023,0,'Spine',12)
 for j in range(5):ring('hose reinforcement',(s*(.18-j*.004),1.27-j*.026,.137),.020,.004,4,'Spine',(0,1,0),16)
 panel('hip hanging plate',[(s*.06,1.048),(s*.203,1.035),(s*.22,.904),(s*.154,.866),(s*.07,.904)],.137,.035,'Hips')
 panel('collar guard',[(s*.09,1.57),(s*.15,1.64),(s*.223,1.566),(s*.178,1.50)],.075,.045,'Chest')
ring('reactor bronze mount',(0,1.268,.186),.041,.011,2,'Spine')
ell('reactor recess',(0,1.268,.19),(.035,.044,.017),0,'Spine',24,12)
ring('amber reactor ring',(0,1.268,.213),.022,.005,3,'Spine',segments=24)
ell('reactor core',(0,1.268,.217),(.008,.014,.006),3,'Spine',12,8)
panel('central belt shield',[(-.055,1.10),(.055,1.10),(.064,.985),(0,.905),(-.064,.985)],.152,.035,'Hips')
# Helmet: compressed mechanical brow, narrow optics and layered respirator.
ell('helmet shell',(0,1.687,-.013),(.126,.156,.123),1,'Head',24,14)
plate('face dark well',[(-.096,1.751),(.096,1.751),(.10,1.64),(.045,1.588),(-.045,1.588),(-.10,1.64)],.105,.024,0,'Head')
for s in [-1,1]:
 panel('helmet cheek',[(s*.025,1.681),(s*.09,1.722),(s*.112,1.661),(s*.065,1.591),(s*.018,1.614)],.112,.033,'Head')
 tube('armored brow',[(s*.012,1.751,.115),(s*.053,1.77,.118),(s*.10,1.733,.089)],.017,2,'Head')
 tube('recessed amber optic',[(s*.031,1.721,.138),(s*.054,1.729,.132)],.005,3,'Head')
 ell('temple actuator',(s*.122,1.699,0),(.024,.039,.040),4,'Head',16,8)
 ring('temple bronze seal',(s*.139,1.699,0),.024,.006,2,'Head',(s,0,0),20)
 tube('helmet crown reinforcement',[(s*.069,1.757,.068),(s*.06,1.818,.017),(s*.033,1.834,-.06)],.011,2,'Head')
 for j in range(3):tube('respirator slat',[(s*.018,1.634+j*.017,.15),(s*.038,1.640+j*.017,.146)],.004,4,'Head')
panel('forehead key',[(-.025,1.804),(.025,1.804),(.037,1.758),(0,1.727),(-.037,1.758)],.100,.022,'Head')
# Readable back: a reactor casing, exhaust packs, bolts and power hoses.
ell('spine reactor casing',(0,1.385,-.17),(.105,.14,.055),0,'Chest',20,12)
for y in np.linspace(1.30,1.48,6):
 tube('rear rib', [(-.075,y,-.211),(0,y+.007,-.225),(.075,y,-.211)],.009,4,'Chest')
for s in [-1,1]:
 limb('back exhaust canister',(s*.177,1.29,-.151),(s*.177,1.51,-.16),.055,1,'Chest')
 for y in [1.31,1.37,1.43,1.49]:ring('exhaust band',(s*.177,y,-.16),.052,.006,2,'Chest',(0,1,0),20)
 tube('rear power feed',[(s*.12,1.32,-.192),(s*.15,1.21,-.16),(s*.12,1.13,-.133)],.016,0,'Spine')
# Armor sits around the actual source-rig joints; guard pieces follow individual bones.
for side,s in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 limb('upper arm piston housing',a,e,.097,0,upper)
 ell('massive shoulder shell',a+[s*.017,-.009,0],(.179,.149,.153),2,upper,20,10)
 ell('olive shoulder cap',a+[s*.025,.006,.011],(.166,.132,.148),1,upper,20,10)
 panel('shoulder front armor',[(s*.225,1.624),(s*.366,1.632),(s*.459,1.536),(s*.422,1.448),(s*.275,1.452)],.117,.038,upper)
 bolts('shoulder bolts',[(s*.282,1.594,.155),(s*.39,1.544,.155),(s*.317,1.477,.159)],upper)
 for j in range(3):
  y=1.45-j*.033
  tube('shoulder lower lamella',[(s*.31,y,.114),(s*.385,y-.007,.132),(s*.448,y-.035,.083)],.014,4,upper)
 ell('elbow joint',e,(.09,.087,.095),0,fore,20,10)
 ell('elbow front cap',e+[0,0,.080],(.072,.07,.024),2,fore,20,10)
 ring('elbow ring',e+[0,0,.100],.048,.008,4,fore,segments=24)
 limb('forearm dark core',e,w,.087,0,fore)
 p=e+(w-e)*.55
 ell('forearm olive bracer',p+[0,0,.026],(.123,.14,.112),1,fore,20,12)
 panel('forearm raised guard',[(p[0]-.086,p[1]+.085),(p[0]+.08,p[1]+.10),(p[0]+.083,p[1]-.076),(p[0]-.067,p[1]-.092)],p[2]+.104,.031,fore)
 for j in range(3):tube('forearm vent',[(p[0]-.051,p[1]+j*.027-.03,p[2]+.136),(p[0]+.046,p[1]+j*.027-.03,p[2]+.136)],.005,0,fore)
 tube('arm hydraulic ram',[e+[s*.073,-.03,-.028],w+[s*.068,.06,-.027]],.018,4,fore,12)
 tube('arm piston sleeve',[e+[s*.073,-.025,-.028],e+(w-e)*.45+[s*.073,0,-.028]],.026,2,fore,12)
 palm=w+[s*.02288,-.05292,.0364]
 ell('glove machinery',palm,(.061,.068,.051),0,hand,20,10)
 ell('armored glove back',palm+[0,.004,-.028],(.062,.054,.035),1,hand,20,10)
 for j in range(4):
  y=palm[1]+(j-1.5)*.022
  tube('closed armored finger',[(palm[0]-s*.04,y,palm[2]),(palm[0]-s*.041,y,palm[2]+.055),(palm[0]+s*.028,y,palm[2]+.056),(palm[0]+s*.037,y,palm[2]+.020)],.011,4,hand,8)
  ell('knuckle armor',(palm[0]-s*.036,y,palm[2]+.052),(.015,.012,.016),1,hand,10,6)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 limb('thigh undersuit',h,k,.102,0,thigh)
 p=(h+k)*.5+[s*.026,0,.012]
 ell('thigh plate body',p,(.133,.208,.12),1,thigh,20,12)
 panel('thigh armor edge',[(p[0]-.096,p[1]+.16),(p[0]+.10,p[1]+.16),(p[0]+.082,p[1]-.138),(p[0],p[1]-.183),(p[0]-.081,p[1]-.13)],.12,.031,thigh)
 ring('hip swivel',h+[s*.037,-.006,.127],.067,.013,2,thigh,segments=24)
 ell('hip actuator',h+[s*.037,-.006,.139],(.05,.05,.017),1,thigh,16,8)
 ell('knee black joint',k,(.10,.08,.095),0,shin,20,10)
 panel('knee shield',[(k[0]-.082,k[1]+.065),(k[0]+.082,k[1]+.065),(k[0]+.067,k[1]-.062),(k[0],k[1]-.09),(k[0]-.067,k[1]-.062)],.094,.038,shin)
 limb('shin mechanical core',k,f,.093,0,shin)
 p=(k+f)*.5+[s*.02,0,0]
 ell('shin olive plate',p,(.137,.20,.127),1,shin,20,12)
 panel('shin raised armor',[(p[0]-.094,p[1]+.16),(p[0]+.094,p[1]+.16),(p[0]+.083,p[1]-.148),(p[0]-.083,p[1]-.148)],.109,.035,shin)
 for j in range(3):tube('shin heat vent',[(p[0]-.055,p[1]+.1-j*.025,.143),(p[0]+.055,p[1]+.1-j*.025,.143)],.0045,0,shin)
 tube('leg piston',[k+[s*.101,-.04,-.036],f+[s*.109,.07,-.036]],.020,4,shin,12)
 tube('leg piston sleeve',[k+[s*.101,-.04,-.036],p+[s*.094,.025,-.036]],.027,2,shin,12)
 ell('heavy foot',f+[0,-.058,.087],(.148,.077,.191),0,foot,20,10)
 ell('boot armored toe',f+[0,-.042,.121],(.139,.074,.16),1,foot,20,10)
 for j in [-1,0,1]:
  x=f[0]+j*.087
  panel('toe steel tooth',[(x-.032,.098),(x+.032,.098),(x+.031,.028),(x-.03,.028)],.266,.025,foot)
 tube('ankle guard', [f+[-.11,.038,.072],f+[0,.065,.101],f+[.11,.038,.072]],.017,2,foot)
 bolts('leg rivets',[(p[0]-.07,p[1]+.12,.144),(p[0]+.07,p[1]+.12,.144)],shin)
 # Sparse bright chips on high-wear armor edges, modeled into the surface.
 for j in range(4):
  x=p[0]-.062+j*.031;y=p[1]-.05+(j%2)*.018
  scar('shin paint chip',(x,y,.147),(x+.014,y+.018,.147),shin)
 scar('shoulder paint chip',(s*.34,1.59,.16),(s*.37,1.577,.16),upper)

# Weapons remain separate skinned renderers so shop palettes and visibility still work.
GROUP=1;bone='WeaponGripL';w=np.array(BONES['HandL'][0]);p=w+[-.02288,-.05292,.0364]
c=p+[0,.055,.142]
ell('shield structural backing',c,(.296,.435,.078),0,bone,32,16)
ell('shield bronze rim',c+[0,0,.037],(.283,.419,.078),2,bone,32,16)
ell('shield olive face',c+[0,0,.067],(.262,.395,.073),1,bone,32,16)
oval('shield silver perimeter',c+[0,0,.085],.27,.400,.012,4,bone)
ell('shield center dome',c+[0,.027,.116],(.214,.312,.073),1,bone,32,16)
# Raised original DR lettering, rigidly skinned to the shield grip.
# A distinct emissive material preserves its purple identity across shop variants.
for label,points in [('D',[(-.15,-.095),(-.15,.13),(-.072,.13),(-.035,.09),(-.035,-.05),(-.072,-.095),(-.15,-.095)]),('R',[(.015,-.095),(.015,.13),(.085,.13),(.125,.095),(.125,.044),(.085,.015),(.015,.015)]),('R leg',[(.075,.015),(.14,-.095)])]:
 tube('shield DR '+label,[c+[x,y+.025,.203] for x,y in points],.013,5,bone,8)
for a in np.linspace(0,2*math.pi,12,endpoint=False):
 q=c+[.271*math.cos(a),.400*math.sin(a),.094]
 ell('shield perimeter fastener',q,(.013,.013,.012),4,bone,12,6)
for s in [-1,1]:
 x=c[0]+s*.247
 for j in range(3):
  y=c[1]-.12+j*.105
  panel('shield locking clasp',[(x-.036,y-.022),(x+.036,y-.022),(x+.033,y+.022),(x-.031,y+.029)],c[2]+.085,.025,bone)
 panel('shield lower talon',[(c[0]+s*.06,c[1]-.31),(c[0]+s*.23,c[1]-.30),(c[0]+s*.19,c[1]-.49),(c[0]+s*.095,c[1]-.42)],c[2]+.042,.035,bone)
 scar('shield edge wear',(c[0]+s*.20,c[1]+.27,c[2]+.10),(c[0]+s*.23,c[1]+.22,c[2]+.10),bone)
tube('shield hand grip',[p+[0,-.067,.033],p+[0,.067,.033]],.031,0,bone,12)

GROUP=2;bone='WeaponGripR';w=np.array(BONES['HandR'][0]);p=w+[.02288,-.05292,.0364];x=p[0];z=p[2]+.040
# The long handle passes through the curled fingers, with a weighted lower pommel.
tube('hammer steel shaft',[(x,.19,z),(x,1.81,z)],.028,4,bone,16)
for y in np.linspace(.23,.46,8):ring('hammer lower grip',(x,y,z),.034,.007,0,bone,(0,1,0),20)
for y in np.linspace(p[1]-.08,p[1]+.09,7):ring('hammer hand wrap',(x,y,z),.035,.006,0,bone,(0,1,0),20)
ell('hammer lower pommel',(x,.185,z),(.050,.047,.05),2,bone,16,8)
limb('hammer upper neck',(x,1.46,z),(x,1.79,z),.063,1,bone)
for y in [1.47,1.54,1.72]:ring('hammer neck band',(x,y,z),.064,.008,2,bone,(0,1,0),24)
cy=1.86
tube('hammer core crossbar',[(x-.255,cy,z),(x+.255,cy,z)],.116,0,bone,24)
# Separate wide striking faces and a vented reactor chamber.
for s in [-1,1]:
 tube('hammer bronze end rim',[(x+s*.17,cy,z),(x+s*.32,cy,z)],[.16,.174],2,bone,24)
 tube('hammer olive end case',[(x+s*.18,cy,z),(x+s*.31,cy,z)],[.15,.162],1,bone,24)
 ring('hammer striking face rim',(x+s*.327,cy,z),.146,.019,4,bone,(s,0,0),32)
 tube('hammer striking face',[(x+s*.316,cy,z),(x+s*.343,cy,z)],.140,1,bone,24)
 for a in np.linspace(0,2*math.pi,8,endpoint=False):
  q=np.array([x+s*.35,cy+.104*math.cos(a),z+.104*math.sin(a)])
  ell('hammer face stud',q,(.012,.019,.019),4,bone,12,6)
 for j in range(4):
  px=x+s*(.05+j*.029)
  ring('hammer reactor fin',(px,cy,z),.122,.008,2,bone,(1,0,0),24)
 for j in [-1,0,1]:
  tube('hammer amber vent',[(x+s*.062,cy+j*.033,z+.12),(x+s*.135,cy+j*.033,z+.12)],.005,3,bone)
# Looping heavy cables, no flame baked into the weapon.
tube('hammer power cable',[(x+.13,cy-.10,z+.073),(x+.19,1.63,z+.08),(x+.12,1.54,z+.09),(x+.055,1.58,z+.084)],.020,0,bone,12)
tube('hammer secondary cable',[(x-.12,cy-.11,z+.071),(x-.19,1.64,z+.083),(x-.12,1.58,z+.09),(x-.058,1.62,z+.082)],.012,4,bone,10)

shieldCenter=c.copy()
vertices=np.array(V);triangles=np.array(F,dtype=np.int32);normals=np.zeros_like(vertices)
for i in range(len(triangles)):
 ids=triangles[i];a,b,c=vertices[ids];n=np.cross(b-a,c-a)
 for j in ids:normals[j]+=n
normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-10)
mesh={'version':1,'boneNames':BN,'anchors':[{'head':list(BONES[n][0]),'tail':list(BONES[n][1])} for n in BN],
'vertices':np.round(vertices,6).ravel().tolist(),'normals':np.round(normals,6).ravel().tolist(),'boneIndices':BI,'groups':GI,'markers':[
{'name':'HammerGrip','bone':'WeaponGripR','position':[x,float(p[1]),z]},
{'name':'HammerTop','bone':'WeaponGripR','position':[x,cy,z]},
{'name':'HammerFront','bone':'WeaponGripR','position':[x,cy,z+.15]},
{'name':'ShieldCenter','bone':'WeaponGripL','position':shieldCenter.tolist()},
{'name':'ShieldFront','bone':'WeaponGripL','position':(shieldCenter+[0,0,.17]).tolist()},
{'name':'ShieldTop','bone':'WeaponGripL','position':(shieldCenter+[0,.40,0]).tolist()}],
'submeshes':[{'triangles':triangles[np.array(MI)==m].ravel().tolist()} for m in range(6)]}
(OUT/'Model.bytes').write_bytes(gzip.compress(json.dumps(mesh,separators=(',',':')).encode(), mtime=0))
report={'vertices':len(V),'triangles':len(F),'materials':6,'bones':len(BN),'parts':len(PARTS),'bounds':[vertices.min(axis=0).tolist(),vertices.max(axis=0).tolist()]}
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2))
assert np.isfinite(vertices).all() and np.isfinite(normals).all()
assert triangles.min()>=0 and triangles.max()<len(V)
assert len(BI)==len(V) and max(BI)<len(BN)
assert len(V)<65000 and len(F)<90000
assert len(GI)==len(V) and set(GI)=={0,1,2}
assert all(len(set(GI[i] for i in f))==1 for f in F)
print(json.dumps(report),flush=True)

# Offline rasterizer: render exact exported geometry with interpolated normals.
def render(yaw,name):
 w,h=800,920;scale=360;angle=math.radians(yaw);tilt=math.radians(-5)
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
  if material in [3,5]:color+=COLORS[material]*.85
  patch=img[lo[1]:hi[1]+1,lo[0]:hi[0]+1];patch[mask]=color[mask];depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=iz[mask]
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'SIEGE TANK / KAELON',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-14,'front.png');render(158,'back.png')

