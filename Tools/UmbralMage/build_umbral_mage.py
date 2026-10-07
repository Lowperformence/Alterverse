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
COLORS=np.array([[.018,.019,.026],[.055,.050,.075],[.84,.86,.89],[.57,.065,.95],[.015,.72,.78],[1.,1.,1.]])

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

# Refinement of the existing pearl-mech design; components follow the original rig.
# Repeated longitudinal rings create curved armor rather than flat extruded boxes.
def armor_wrap(name,a,b,width,depth,mat,bone):
 a=np.array(a,float);b=np.array(b,float);axis=unit(b-a)
 across=unit(np.cross(axis,[0,0,1]));out=unit(np.cross(across,axis))
 verts=[];faces=[];segments=20
 profiles=[(0,.70),(.06,.86),(.18,1.0),(.45,1.03),(.73,.96),(.90,.83),(1,.66)]
 for t,factor in profiles:
  center=a+(b-a)*t
  for j in range(segments):
   ang=2*math.pi*j/segments
   # Flatten the inner shell slightly; let the front contour remain rounded.
   z=math.sin(ang);z=z if z>0 else z*.78
   verts.append(center+across*math.cos(ang)*width*factor+out*z*depth*factor)
 for row in range(len(profiles)-1):
  for j in range(segments):
   i=row*segments+j;k=row*segments+(j+1)%segments
   faces.extend([[i,k,k+segments],[i,k+segments,i+segments]])
 for row,reverse in [(0,True),(len(profiles)-1,False)]:
  center=len(verts);verts.append(a+(b-a)*profiles[row][0])
  for j in range(segments):
   f=[center,row*segments+j,row*segments+(j+1)%segments]
   if reverse:f.reverse()
   faces.append(f)
 # End caps need axial normals; radial tests on coplanar caps are numerically unstable.
 cap_start=len(profiles)*segments
 for f in faces:
  p,q,r=[verts[i] for i in f];c=(p+q+r)/3
  outward=(-axis if cap_start in f else axis) if max(f)>=cap_start else c-(a+axis*np.dot(c-a,axis))
  if np.dot(np.cross(q-p,r-p),outward)<0:f.reverse()
 add(name,verts,faces,mat,bone)

shell('sealed pelvis flexible shell',[(.875,.095,.075,0),(.935,.140,.105,0),(1.025,.147,.103,0),(1.10,.105,.080,0)],0,'Hips',32)
shell('fitted ribbed abdomen',[(1.04,.082,.067,0),(1.12,.086,.073,0),(1.20,.095,.080,0),(1.29,.116,.087,0),(1.36,.130,.09,0)],0,'Spine',32)
shell('anatomical upper thorax',[(1.27,.116,.080,0),(1.34,.158,.107,0),(1.44,.190,.120,0),(1.52,.190,.105,0),(1.575,.115,.080,0)],0,'Chest',32)
# Separate left/right sculpted breastplate with deliberate underlapping side panels.
for sign in [-1,1]:
 plate('sculpted ceramic breastplate',[(sign*.012,1.570),(sign*.135,1.563),(sign*.203,1.508),(sign*.194,1.420),(sign*.140,1.345),(sign*.056,1.357),(sign*.014,1.412)],.099,.069,2,'Chest')
 plate('rib overlapping ceramic',[(sign*.142,1.437),(sign*.192,1.45),(sign*.162,1.345),(sign*.116,1.308),(sign*.081,1.335)],.070,.042,2,'Chest')
 plate('clavicle collar',[(sign*.014,1.601),(sign*.115,1.60),(sign*.170,1.559),(sign*.091,1.546),(sign*.027,1.568)],.055,.030,1,'Chest')
 for j in range(3):
  y=1.526-j*.016
  tube('recessed chest intake',[(sign*.108,y,.164),(sign*.148,y-.006,.158)],.003,0,'Chest',6)
 for y in [1.544,1.407]:ell('flush chest socket',(sign*.067,y,.173),(.004,.004,.002),1,'Chest',8,5)
 plate('rear sculpted shoulder blade',[(sign*.012,1.565),(sign*.142,1.551),(sign*.166,1.446),(sign*.096,1.346),(sign*.015,1.364)],-.090,-.052,2,'Chest')
 for j in range(3):
  tube('rear radiator vent',[(sign*.046,1.457+j*.022,-.145),(sign*.114,1.452+j*.022,-.145)],.0035,0,'Chest',6)
# Articulated abdominal lamellae conceal the empty stick-like midsection.
for j in range(4):
 y=1.28-j*.044
 plate('abdominal lamella',[(-.058,y+.019),(.058,y+.019),(.066,y-.013),(.042,y-.026),(-.042,y-.026),(-.066,y-.013)],.079,.020,1,'Spine')
 for sign in [-1,1]:
  tube('abdominal oblique rib',[(sign*.066,y+.016,.078),(sign*.093,y+.004,.044)],.007,0,'Spine',8)
plate('central pelvis ceramic',[(-.062,1.105),(.062,1.105),(.074,1.025),(.035,.900),(0,.870),(-.035,.900),(-.074,1.025)],.093,.040,2,'Hips')
# Raised neck seals and collar close the artificial long neck gap.
limb('neck flexible seal',(0,1.535,0),(0,1.677,0),.047,0,'Head')
for y in [1.583,1.598,1.613,1.628]:ring('neck seal rib',(0,y,0),.048,.0035,1,'Head',(0,1,0),20)
ell('enclosed helmet shell',(0,1.752,-.007),(.096,.113,.086),1,'Head',32,18)
plate('recessed black mask',[(-.071,1.811),(0,1.854),(.071,1.811),(.061,1.730),(0,1.657),(-.061,1.730)],.072,.026,0,'Head')
for sign in [-1,1]:
 plate('swept white brow',[(sign*.004,1.866),(sign*.047,1.858),(sign*.093,1.812),(sign*.075,1.778),(sign*.026,1.810)],.077,.034,2,'Head')
 plate('sculpted cheek jaw shell',[(sign*.069,1.792),(sign*.109,1.808),(sign*.102,1.739),(sign*.065,1.695),(sign*.012,1.655),(sign*.023,1.725)],.076,.029,2,'Head')
 plate('helmet lateral cheek vent',[(sign*.083,1.779),(sign*.106,1.785),(sign*.093,1.749),(sign*.076,1.74)],.099,.007,0,'Head')
 plate('helmet rear quarter',[(sign*.005,1.853),(sign*.068,1.839),(sign*.091,1.77),(sign*.06,1.696),(sign*.014,1.674)],-.057,-.033,2,'Head')
 tube('slanted violet optical lens',[(sign*.019,1.780,.117),(sign*.038,1.791,.115),(sign*.058,1.803,.108)],[.004,.006,.0028],3,'Head',10)
 shard('swept split crown',(sign*.020,1.833,-.004),(sign*.094,1.976,-.028),.024,.018,2,'Head')
 tube('crown inset violet emitter',[(sign*.029,1.863,.060),(sign*.062,1.914,.036),(sign*.088,1.962,-.005)],[.0038,.003,.001],3,'Head',8)
 shard('temple antenna',(sign*.085,1.800,.025),(sign*.125,1.839,-.018),.013,.010,1,'Head')
shard('cyan forehead diamond',(0,1.801,.116),(0,1.850,.102),.009,.006,4,'Head')
tube('forehead axial inlay',[(0,1.855,.106),(0,1.901,.039)],[.0038,.002],4,'Head',8)
plate('white central chin',[(-.022,1.703),(0,1.722),(.022,1.703),(.018,1.660),(0,1.644),(-.018,1.660)],.090,.018,2,'Head')

for side,sign in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0]);axis=unit(e-a)
 # Glossy graphite shoulder shell: a complete sculpted cap, not a loose sphere.
 ell('graphite shoulder rotary core',a+[sign*.028,0,0],(.104,.088,.096),0,upper,28,16)
 ell('graphite shoulder crown',a+[sign*.034,.028,-.012],(.106,.077,.093),1,upper,28,14)
 ring('shoulder recessed bearing',a+[sign*.030,0,.074],.056,.007,1,upper,(0,0,1),24)
 armor_wrap('continuous ceramic upper arm',a+(e-a)*.23,e-axis*.042,.060,.067,2,upper)
 tube('white upper arm light strip',[a+(e-a)*.40+[sign*.012,0,.070],a+(e-a)*.62+[sign*.012,0,.070]],.0025,5,upper,8)
 # Keep the dark cuff independent of the joint motion.
 armor_wrap('upper arm terminal cuff',e-axis*.053,e-axis*.032,.058,.059,1,upper)
 ell('elbow recessed pivot',e,(.060,.059,.064),0,fore,24,14)
 ring('silver elbow retaining bezel',e+[0,0,.060],.035,.006,1,fore,segments=24)
 armor_wrap('sculpted wraparound forearm',e+(w-e)*.17,w+(e-w)*.10,.064,.066,2,fore)
 p=e+(w-e)*.52
 plate('recessed forearm service panel',[(p[0]-.022,p[1]+.055),(p[0]+.022,p[1]+.055),(p[0]+.021,p[1]-.045),(p[0]-.021,p[1]-.048)],p[2]+.081,.005,0,fore)
 tube('forearm restrained violet conduit',[(p[0],p[1]+.037,p[2]+.088),(p[0],p[1]-.023,p[2]+.088)],.0028,3,fore,6)
 palm=w+[sign*.01424,-.05341,.02576]
 ell('flexible palm',palm,(.037,.044,.025),0,hand,20,12)
 plate('hand protective metacarpal',[(palm[0]-.035,palm[1]+.033),(palm[0]+.035,palm[1]+.033),(palm[0]+.030,palm[1]-.009),(palm[0]-.030,palm[1]-.009)],palm[2]+.032,.012,1,hand)
 for j in range(4):
  xx=palm[0]+(j-1.5)*.017
  points=[(xx,palm[1]+.003,palm[2]+.015),(xx,palm[1]-.025,palm[2]+.038),(xx,palm[1]-.045,palm[2]+.013)]
  tube('articulated closed finger',points,[.0085,.008,.006],0,hand,8)
  ell('finger hinge',points[1],(.0088,.0088,.0088),1,hand,10,6)
 tube('opposing segmented thumb',[palm+[sign*.037,.018,0],palm+[sign*.045,-.008,.027],palm+[sign*.023,-.017,.034]],[.012,.011,.008],0,hand,8)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 ell('hip sealed articulation',h+[0,.015,0],(.086,.087,.083),0,thigh,24,14)
 ring('violet hip annular reactor',h+[0,.020,.089],.062,.009,3,thigh,segments=40)
 ring('hip machined outer bezel',h+[0,.020,.085],.078,.007,1,thigh,segments=32)
 armor_wrap('rounded anatomical thigh shell',h+(k-h)*.21,k+(h-k)*.17,.088,.082,2,thigh)
 p=h+(k-h)*.69
 plate('thigh angled inset',[(p[0]-.055,p[1]+.008),(p[0]+.055,p[1]+.022),(p[0]+.053,p[1]+.008),(p[0]-.052,p[1]-.005)],p[2]+.091,.005,0,thigh)
 ell('knee flexible hinge',k,(.066,.065,.068),0,shin,24,14)
 plate('floating shaped knee cap',[(k[0]-.054,k[1]+.047),(k[0]+.054,k[1]+.047),(k[0]+.061,k[1]+.011),(k[0]+.036,k[1]-.043),(k[0]-.036,k[1]-.043),(k[0]-.061,k[1]+.011)],.073,.040,2,shin)
 armor_wrap('calf undershell',k+(f-k)*.16,f+(k-f)*.13,.048,.052,0,shin)
 armor_wrap('continuous ceramic greave',k+(f-k)*.18,f+(k-f)*.19,.063,.064,2,shin)
 p=k+(f-k)*.55
 plate('long greave technical recess',[(p[0]-.019,p[1]+.084),(p[0]+.019,p[1]+.082),(p[0]+.017,p[1]-.077),(p[0]-.017,p[1]-.077)],p[2]+.072,.005,0,shin)
 tube('violet greave inlay',[(p[0],p[1]+.053,p[2]+.080),(p[0],p[1]-.039,p[2]+.080)],.003,3,shin,8)
 shard('outer swept greave',p+[sign*.046,-.071,0],k+[sign*.083,.024,.012],.026,.021,2,shin)
 ell('ankle bearing',f,(.042,.044,.047),0,foot,20,12)
 # Three dimensional feet now have a rounded vamp, toe box and independent sole.
 ell('sculpted armored foot',f+[0,-.062,.060],(.062,.052,.120),1,foot,24,14)
 ell('ceramic upper vamp',f+[0,-.031,.043],(.054,.046,.076),2,foot,24,14)
 plate('angular toe armor',[(f[0]-.048,.080),(f[0]+.048,.080),(f[0]+.051,.050),(f[0]+.032,.036),(f[0]-.032,.036),(f[0]-.051,.050)],.169,.023,2,foot)
 ell('rubber foot sole',f+[0,-.089,.064],(.060,.016,.118),0,foot,20,8)

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
report['qualityPass']='Rounded pearl-mech shells, recessed joints and fitted helmet details; original rig and focus retained'
report['weaponScale']=WEAPON_SCALE
report['focusGripToAperture']=round(float(np.linalg.norm(np.array(staff_top)-np.array(focus_pivot))),6)
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2))
assert np.isfinite(vertices).all() and np.isfinite(normals).all()
assert np.allclose(np.linalg.norm(normals,axis=1),1,atol=.001)
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

