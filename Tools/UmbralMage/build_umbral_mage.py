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
COLORS=np.array([[.018,.019,.026],[.13,.105,.22],[.72,.76,.80],[.57,.065,.95],[.015,.72,.78],[1.,1.,1.]])

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

# Pearl armored arcane mech, based on the new user silhouette.
shell('pelvis engine',[(.87,.12,.085,0),(1.04,.15,.10,0),(1.12,.095,.07,0)],0,'Hips')
shell('narrow violet waist',[(1.05,.080,.061,0),(1.23,.09,.070,0),(1.36,.125,.088,0)],1,'Spine')
shell('chest armored volume',[(1.27,.10,.085,0),(1.36,.17,.11,0),(1.49,.19,.10,0),(1.57,.11,.07,0)],0,'Chest')
for s in [-1,1]:
 plate('broad ivory chest facet',[(s*.008,1.55),(s*.15,1.565),(s*.208,1.46),(s*.14,1.34),(s*.016,1.375)],.088,.058,2,'Chest')
 plate('chest lower bevel',[(s*.015,1.39),(s*.142,1.35),(s*.13,1.30),(s*.02,1.335)],.093,.029,1,'Chest')
 for j in range(3):
  y=1.48-j*.019
  tube('chest intake',[(s*.085,y,.148),(s*.137,y-.012,.148)],.004,0,'Chest',6)
 tube('abdominal piston',[(s*.061,1.09,.065),(s*.068,1.32,.077)],.013,2,'Spine',10)
 plate('back reactor housing',[(s*.015,1.55),(s*.13,1.53),(s*.145,1.36),(s*.03,1.30)],-.09,-.042,1,'Chest')
 for j in range(3):
  tube('back cooling slit',[(s*.04,1.38+j*.045,-.135),(s*.11,1.38+j*.045,-.135)],.005,4,'Chest',6)
limb('neck actuator',(0,1.52,0),(0,1.69,0),.039,0,'Head')
ell('compact helmet core',(0,1.755,0),(.080,.100,.075),1,'Head',12,10)
plate('black sensor mask',[(-.048,1.82),(0,1.85),(.048,1.82),(.045,1.72),(0,1.68),(-.045,1.72)],.063,.015,0,'Head')
for s in [-1,1]:
 plate('white helmet brow',[(0,1.867),(s*.035,1.86),(s*.082,1.803),(s*.06,1.771),(s*.02,1.799)],.069,.025,2,'Head')
 plate('white cheek vane',[(s*.048,1.782),(s*.104,1.806),(s*.086,1.742),(s*.023,1.691),(s*.029,1.741)],.074,.018,2,'Head')
for dx,dy in [(0,0),(-.018,.02),(.018,.02),(0,-.022)]:
 ell('violet optical sensor',(dx,1.762+dy,.097),(.011,.012,.006),3,'Head',8,6)
# Replace the plain antenna with a more deliberate, split mech crest.
for s in [-1,1]:
 shard('split crown fin',(s*.018,1.838,.018),(s*.087,1.974,-.012),.026,.018,2,'Head')
 tube('violet crown channel',[(s*.028,1.855,.071),(s*.055,1.905,.052),(s*.084,1.960,.012)],[.006,.005,.001],3,'Head',8)
 shard('temple signal fin',(s*.073,1.792,.045),(s*.128,1.850,-.006),.018,.014,1,'Head')
ell('cyan forehead reactor',(0,1.852,.083),(.022,.027,.012),4,'Head',16,10)
ring('forehead reactor bezel',(0,1.852,.080),.030,.004,2,'Head',(0,0,1),24)
for s in [-1,1]:
 tube('visor brow light',[(s*.016,1.806,.088),(s*.050,1.817,.080),(s*.078,1.803,.061)],[.004,.005,.001],4,'Head',8)
for side,s in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 limb('upper arm mechanical sleeve',a,e,.052,1,upper)
 ell('shoulder rotary joint',a,(.072,.074,.075),0,upper,16,10)
 # Upper-arm plate between shoulder and elbow; no luminous shoulder cap.
 axis=unit(e-a);across=unit(np.array([-axis[1],axis[0],0]))
 top=a+(e-a)*.19;bottom=a+(e-a)*.83
 outline=[(top-across*.044)[:2],(top+across*.044)[:2],(bottom+across*.057)[:2],(bottom+axis*.017+across*.028)[:2],(bottom+axis*.017-across*.028)[:2],(bottom-across*.057)[:2]]
 faceZ=max(a[2],e[2])+.080
 plate('upper arm pearl armor plate',outline,faceZ,.030,2,upper)
 plate('upper arm rear armor plate',outline,-.057,-.023,1,upper)
 stripeStart=a+(e-a)*.28;stripeEnd=a+(e-a)*.74
 stripeStart[2]=faceZ+.030;stripeEnd[2]=faceZ+.030
 tube('upper arm recessed stripe channel',[stripeStart,stripeEnd],.010,0,upper,8)
 stripeStart[2]+=.008;stripeEnd[2]+=.008
 tube('upper arm fluorescent white stripe',[stripeStart,stripeEnd],.0055,5,upper,8)
 ell('elbow pivot',e,(.057,.055,.055),0,fore,12,8)
 ring('elbow cap',e+[0,0,.055],.034,.009,2,fore,segments=16)
 limb('forearm violet actuator',e,w,.048,1,fore)
 p=(e+w)*.5
 plate('white forearm shell',[(p[0]-s*.052,p[1]+.115),(p[0]+s*.052,p[1]+.091),(p[0]+s*.061,p[1]-.060),(p[0]+s*.021,p[1]-.118),(p[0]-s*.042,p[1]-.076)],.045,.050,2,fore)
 plate('forearm inset',[(p[0]-s*.021,p[1]+.061),(p[0]+s*.025,p[1]+.05),(p[0]+s*.029,p[1]-.018),(p[0]-s*.015,p[1]-.04)],.093,.006,0,fore)
 palm=w+[s*.01424,-.05341,.02576]
 plate('hand knuckle block',[(palm[0]-.035,palm[1]+.038),(palm[0]+.035,palm[1]+.038),(palm[0]+.032,palm[1]-.035),(palm[0]-.032,palm[1]-.035)],palm[2],.036,1,hand)
 for j in range(4):
  xx=palm[0]+(j-1.5)*.017
  tube('segmented finger',[(xx,palm[1]+.010,palm[2]+.038),(xx,palm[1]-.031,palm[2]+.045),(xx,palm[1]-.045,palm[2]+.012)],.007,0,hand,6)
 tube('opposed thumb',[palm+[s*.041,.025,0],palm+[s*.046,-.007,.025],palm+[s*.026,-.014,.036]],.011,1,hand,6)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 ell('hip gimbal',h+[0,.013,0],(.083,.083,.08),0,thigh,16,10)
 ring('violet hip reactor',h+[0,.021,.078],.059,.009,3,thigh,segments=24)
 ring('hip reactor bezel',h+[0,.021,.077],.073,.007,1,thigh,segments=24)
 limb('thigh structure',h,k,.068,1,thigh)
 x=(h[0]+k[0])*.5
 plate('large white thigh armor',[(x-s*.07,.885),(x+s*.08,.90),(x+s*.10,.72),(x+s*.05,.615),(x-s*.06,.65)],.048,.056,2,thigh)
 plate('thigh terminal seam',[(x-s*.05,.712),(x+s*.065,.725),(x+s*.068,.70),(x-s*.05,.689)],.101,.006,0,thigh)
 plate('rear thigh armor',[(x-.059,.86),(x+.059,.86),(x+.055,.67),(x-.055,.67)],-.041,-.032,1,thigh)
 ell('knee hinge',k,(.063,.061,.065),0,shin,12,8)
 plate('white angular kneecap',[(k[0]-.062,.585),(k[0]+.062,.585),(k[0]+.067,.54),(k[0]+.038,.481),(k[0]-.047,.49)],.054,.038,2,shin)
 limb('lower leg actuators',k,f,.046,0,shin)
 x=(k[0]+f[0])*.5
 plate('long violet greave',[(x-.045,.481),(x+.044,.482),(x+.053,.37),(x+.035,.178),(x-.034,.17),(x-.049,.32)],.030,.034,1,shin)
 tube('shin central rail',[(x,.43,.078),(x,.23,.066)],.008,2,shin,8)
 plate('outer white shin blade',[(x+s*.045,.49),(x+s*.102,.56),(x+s*.087,.29),(x+s*.040,.21)],.02,.031,2,shin)
 ell('ankle bearing',f,(.041,.044,.044),0,foot,12,8)
 shell('angular mech foot',[(.048,.057,.11,.061),(.086,.06,.108,.055),(.145,.037,.042,-.012)],1,foot,8)
 # shell helper is centered on x=0: move only this foot's vertices to its own ankle.
 count=PARTS[-1]['vertices']
 for v in V[-count:]:v[0]+=f[0]
 plate('toe cap',[(f[0]-.052,.087),(f[0]+.052,.087),(f[0]+.05,.047),(f[0]-.05,.047)],.158,.020,2,foot)
plate('white pelvic shield',[(-.052,1.11),(.052,1.11),(.065,1.013),(.031,.871),(0,.835),(-.041,.902)],.088,.040,2,'Hips')
for side,s in [('L',-1),('R',1)]:
 for bone,y0,y1,width in [('Forearm'+side,1.23,1.07,.043),('Shin'+side,.46,.21,.043)]:
  a=np.array(BONES[bone][0]);b=np.array(BONES[bone][1]);xx=(a[0]+b[0])*.5
  plate('rear articulated armor',[(xx-width,y0),(xx+width,y0),(xx+width*.8,y1),(xx-width*.8,y1)],-.046,-.027,1,bone)
  tube('rear actuator rail',[(xx,y0-.035,-.08),(xx,y1+.025,-.08)],.006,2,bone,6)
 plate('rear helmet casing',[(s*.006,1.84),(s*.055,1.823),(s*.071,1.752),(s*.035,1.69),(s*.008,1.706)],-.059,-.026,2,'Head')
# Hand-held deployable focus: compact reactor and four open armored petals; no staff.
GROUP=2;bone='WeaponGripR';p=np.array(BONES['HandR'][0])+[.01424,-.05341,.02576];x=p[0];z=p[2]+.031
limb('focus grip',(x,p[1]-.049,z),(x,p[1]+.065,z),.015,0,bone)
tube('reactor barrel',[(x,p[1]+.035,z),(x,p[1]+.25,z)],[.054,.068],1,bone,12)
ring('focus muzzle housing',(x,p[1]+.252,z),.056,.012,0,bone,(0,1,0),20)
ring('focus violet aperture',(x,p[1]+.259,z),.041,.007,3,bone,(0,1,0),20)
for j in range(4):
 angle=j*math.pi/2;d=np.array([math.cos(angle),0,math.sin(angle)])
 a=np.array([x,p[1]+.075,z])+d*.055;b=np.array([x,p[1]+.33,z])+d*.18
 tube('deployed petal mechanical spine',[a,b],[.033,.018],0,bone,6)
 u=np.array([-d[2],0,d[0]]);axis=unit(b-a);n=unit(np.cross(u,axis))
 verts=[]
 for c,width in [(a,.04),(a+(b-a)*.72,.059),(b,.028)]:
  for du,dn in [(-1,-1),(1,-1),(1,1),(-1,1)]:verts.append(c+u*width*du+n*.017*dn)
 faces=[]
 for row in range(2):
  for q in range(4):
   i=row*4+q;jj=row*4+(q+1)%4;faces.extend([[i,jj,jj+4],[i,jj+4,i+4]])
 faces.extend([[0,2,1],[0,3,2],[8,9,10],[8,10,11]])
 center=np.mean(verts,axis=0)
 for face in faces:
  aa,bb,cc=[verts[q] for q in face]
  if np.dot(np.cross(bb-aa,cc-aa),(aa+bb+cc)/3-center)<0:face.reverse()
 add('deployed broad violet focus petal',verts,faces,1,bone)
 tube('petal white edge',[a+u*.04,b+u*.028],.009,2,bone,6)
 for t in [.40,.55,.70]:
  c=a+(b-a)*t-d*.025
  tube('cyan petal capacitor',[c+[-.012,0,-.012],c+[.012,.006,.012]],.005,4,bone,6)

GROUP=0

# Scale Ione's entire hand-held focus by 30% around the handle center, preserving
# the palm contact while enlarging the head, petals and all mounted details.
WEAPON_SCALE=1.30
weapon_pivot=np.array([x,p[1],z])
for i,v in enumerate(V):
 if GI[i]==2:
  V[i]=(weapon_pivot+(np.array(v)-weapon_pivot)*WEAPON_SCALE).tolist()

# Heavy mech proportions: enlarge armor around each segment axis without lengthening limbs.
for i,v in enumerate(V):
 if GI[i]!=0:continue
 name=BN[BI[i]];a=np.array(BONES[name][0]);b=np.array(BONES[name][1]);point=np.array(v)
 if name in ['Hips','Spine','Chest']:
  sx={'Hips':1.15,'Spine':1.38,'Chest':1.20}[name]
  point[0]*=sx;point[2]*=1.22
 elif name.startswith(('UpperArm','Forearm','Thigh','Shin')):
  axis=unit(b-a);center=a+axis*np.dot(point-a,axis)
  factor=1.25 if name.startswith(('UpperArm','Forearm')) else 1.23
  point=center+(point-center)*factor
 elif name.startswith('Foot'):
  point[0]=a[0]+(point[0]-a[0])*1.20
 V[i]=point.tolist()

vertices=np.array(V);triangles=np.array(F,dtype=np.int32);normals=np.zeros_like(vertices)
for i in range(len(triangles)):
 ids=triangles[i];a,b,c=vertices[ids];n=np.cross(b-a,c-a)
 for j in ids:normals[j]+=n
normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-10)
focus_pivot=weapon_pivot.tolist()
staff_top=(weapon_pivot+(np.array([x,p[1]+.26,z])-weapon_pivot)*WEAPON_SCALE).tolist()
staff_front=(weapon_pivot+(np.array([x,p[1]+.26,z+.1])-weapon_pivot)*WEAPON_SCALE).tolist()
mesh={'version':1,'boneNames':BN,'anchors':[{'head':list(BONES[n][0]),'tail':list(BONES[n][1])} for n in BN],
'vertices':np.round(vertices,6).ravel().tolist(),'normals':np.round(normals,6).ravel().tolist(),'boneIndices':BI,'groups':GI,'markers':[{'name':'StaffGrip','bone':'WeaponGripR','position':focus_pivot},{'name':'StaffTop','bone':'WeaponGripR','position':staff_top},{'name':'StaffFront','bone':'WeaponGripR','position':staff_front}],
'submeshes':[{'triangles':triangles[np.array(MI)==m].ravel().tolist()} for m in range(len(COLORS))]}
(OUT/'Model.bytes').write_bytes(gzip.compress(json.dumps(mesh,separators=(',',':')).encode(), mtime=0))
report={'vertices':len(V),'triangles':len(F),'materials':len(COLORS),'bones':len(BN),'parts':len(PARTS),'bounds':[vertices.min(axis=0).tolist(),vertices.max(axis=0).tolist()]}
report['weaponScale']=WEAPON_SCALE
report['focusGripToAperture']=round(float(np.linalg.norm(np.array(staff_top)-np.array(focus_pivot))),6)
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2))
assert np.isfinite(vertices).all() and np.isfinite(normals).all()
assert triangles.min()>=0 and triangles.max()<len(V)
assert len(BI)==len(V) and max(BI)<len(BN)
assert abs(report['focusGripToAperture']-.338)<1e-6
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
 xy=np.column_stack([w/2-p[:,0]*scale,h/2-p[:,1]*scale]);z=p[:,2]
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
  if material in [3,4,5]:color+=COLORS[material]*.85
  patch=img[lo[1]:hi[1]+1,lo[0]:hi[0]+1];patch[mask]=color[mask];depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=iz[mask]
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),'IONE VASKA / PEARL MECH',fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-14,'front.png');render(158,'back.png')
