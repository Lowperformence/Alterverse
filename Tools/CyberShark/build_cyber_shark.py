"""Build Nerok's original cyber-shark and anchor in Blender; export skeletal skin geometry.
Run: blender --background --python Tools/CyberShark/build_cyber_shark.py
Uses the established +Y-up hero bone contract. No external artwork is embedded.
"""
from pathlib import Path
import math, json, gzip
import numpy as np
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
# Reuse the project's tested geometry primitives, without executing its hero construction.
helper=ROOT/'Tools/UmbralMage/build_umbral_mage.py'
exec(compile(helper.read_text().split('# Pearl armored arcane mech')[0].split('# Refinement of the existing pearl-mech design')[0].replace('from PIL import Image, ImageDraw', ''),str(helper),'exec'))
OUT=ROOT/'Assets/Alterverse/Resources/CyberShark'
ART=ROOT/'ArtSource/Heroes/CyberShark'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
COLORS=[(.035,.022,.014),(.58,.61,.62),(.035,.065,.078),(.04,.78,1.0),(.72,.39,.105),(.93,.94,.91)]
shell('armored torso',[(1.02,.16,.11,0),(1.25,.24,.15,0),(1.46,.30,.19,0),(1.57,.18,.13,0)],1,'Chest',20)
shell('abdomen',[(.89,.18,.12,0),(1.04,.20,.13,0),(1.23,.16,.11,0)],1,'Spine',16)
for s in [-1,1]:
 plate('pirate side cuirass',[(s*.17,1.54),(s*.255,1.52),(s*.285,1.40),(s*.23,1.29),(s*.19,1.33)],.145,.055,2,'Chest')
 tube('chest cyan seam',[(s*.045,1.49,.202),(s*.11,1.41,.209),(s*.18,1.36,.19)],.005,3,'Chest',6)
 for j in range(3):plate('abdomen course',[(s*.018,1.24-j*.075),(s*.16,1.22-j*.075),(s*.15,1.17-j*.075),(s*.018,1.19-j*.075)],.11,.025,1,'Spine')
 # Swept back fins and mechanical gill vents.
 shard('dorsal back fin',(s*.07,1.39,-.15),(s*.10,1.80,-.35),.070,.022,2,'Chest')
 for j in range(4):tube('gill vent',[(s*.20,1.50-j*.035,-.08),(s*.24,1.48-j*.035,.015)],.009,0,'Chest',6)
# One connected shark head: skull, snout, jaw edge and mouth cavity share vertices.
def build_unified_shark_head():
 segments=64
 vertices=[];faces=[];materials=[]
 # Start at the mouth edge, roll into the jaw, then flow over the snout into the skull.
 rings=[(.145,.061,1.630,.332,.047),(.158,.072,1.640,.317,.070),
        (.171,.095,1.695,.218,.195),(.180,.180,1.735,.055,0),
        (.146,.149,1.735,-.057,0),(.082,.086,1.735,-.103,0)]
 for rx,ry,cy,z,nose in rings:
  for a in np.linspace(0,math.tau,segments,endpoint=False):
   vertices.append((rx*math.cos(a)*(1-.40*max(0,math.sin(a))**2 if nose>.10 else 1),cy+ry*math.sin(a),z+nose*max(0,math.sin(a))))
 for row in range(len(rings)-1):
  for j in range(segments):
   a=row*segments+j;b=row*segments+(j+1)%segments
   faces.extend([[a,b+segments,b],[a,a+segments,b+segments]]);materials.extend([1,1])
 back=len(vertices);vertices.append((0,1.735,-.120))
 for j in range(segments):
  faces.append([(len(rings)-1)*segments+j,back,(len(rings)-1)*segments+(j+1)%segments]);materials.append(1)
 # The inner wall shares the exact opening ring with the skin: no separate lip/cheek objects.
 last=0
 for rx,ry,cy,z in [(.125,.049,1.630,.230),(.059,.024,1.630,.188)]:
  row=len(vertices)
  for a in np.linspace(0,math.tau,segments,endpoint=False):
   vertices.append((rx*math.cos(a),cy+ry*math.sin(a),z))
  for j in range(segments):
   a=last+j;b=last+(j+1)%segments
   faces.extend([[a,b,row+(j+1)%segments],[a,row+(j+1)%segments,row+j]]);materials.extend([0,0])
  last=row
 inside=len(vertices);vertices.append((0,1.630,.178))
 for j in range(segments):
  faces.append([last+j,last+(j+1)%segments,inside]);materials.append(0)
 # Lift the mouth corners gently into a grin, fading smoothly into the cheek.
 def smile_point(point):
  p=np.array(point,dtype=float).copy()
  vertical=max(0,1-abs(p[1]-1.635)/.115)
  forward=float(np.clip((p[2]-.210)/.120,0,1))
  p[1]+=.018*(p[0]/.145)**2*vertical*forward
  return p.tolist()
 vertices=[smile_point(p) for p in vertices]
 material_start=len(MI)
 add('unified shark skull snout and jaw',vertices,faces,1,'Head')
 MI[material_start:]=materials
 # Locate eyes and nostrils on the actual skin, keeping them embedded in the snout.
 def skin_depth(x,y):
  depths=[]
  for face,material in zip(faces,materials):
   if material!=1:continue
   p,q,r=np.array([vertices[i] for i in face])
   den=(q[1]-r[1])*(p[0]-r[0])+(r[0]-q[0])*(p[1]-r[1])
   if abs(den)<1e-10:continue
   u=((q[1]-r[1])*(x-r[0])+(r[0]-q[0])*(y-r[1]))/den
   v=((r[1]-p[1])*(x-r[0])+(p[0]-r[0])*(y-r[1]))/den
   if u>=-1e-8 and v>=-1e-8 and u+v<=1+1e-8:depths.append(u*p[2]+v*q[2]+(1-u-v)*r[2])
  if not depths:raise ValueError('Facial feature lies outside shark head')
  return max(depths)
 for s in [-1,1]:
  x=s*.148;y=1.786
  ell('black shark eye',(x,y,skin_depth(x,y)),(.015,.011,.008),0,'Head',16,10)
  x=s*.058;y=1.757
  ell('shark nostril',(x,y,skin_depth(x,y)+.0005),(.007,.005,.0035),0,'Head',12,8)
 # Broad shark teeth sit inside the opening and follow the curved upper/lower jaw.
 for row,count in [('upper',11),('lower',9)]:
  for angle in np.linspace(math.radians(16),math.radians(164),count):
   a=angle if row=='upper' else -angle
   base=np.array((.137*math.cos(a),1.630+.055*math.sin(a),.338+.047*max(0,math.sin(a))))
   inward=unit(np.array((-math.cos(a),-math.sin(a),0)))
   tangent=np.array((-inward[1],inward[0],0))
   width=.010+.003*abs(math.sin(a));length=.018+.012*abs(math.sin(a))
   left=base-tangent*width;right=base+tangent*width;tip=base+inward*length+[0,0,-.004]
   points=[left,right,tip,left+[0,0,-.014],right+[0,0,-.014],tip+[0,0,-.014]]
   tooth_faces=[[0,1,2],[5,4,3],[0,3,4],[0,4,1],[1,4,5],[1,5,2],[2,5,3],[2,3,0]]
   points=[smile_point(p) for p in points]
   center=np.mean(points,axis=0)
   for face in tooth_faces:
    p,q,r=np.array([points[i] for i in face])
    if np.dot(np.cross(q-p,r-p),(p+q+r)/3-center)<0:face.reverse()
   add(row+' shark tooth',points,tooth_faces,5,'Head')
build_unified_shark_head()
# Halve the cranial fin width; retain its enlarged height and sweep.
cranial_fin_start=len(V)
shard('shark cranial fin',(0,1.85,-.08),(0,2.00,-.17),.065,.025,1,'Head')
cranial_fin_pivot=np.array((0,1.85,-.08))
for i in range(cranial_fin_start,len(V)):
 V[i]=(cranial_fin_pivot+(np.array(V[i])-cranial_fin_pivot)*np.array((.75,1.5,1.5))).tolist()
for s in [-1,1]:
 for j in range(3):tube('cranial vent',[(s*.127,1.79-j*.025,.09),(s*.147,1.78-j*.025,.12)],.004,0,'Head',6)
# Closed arm envelopes with shared elbow rings and two-bone skinning.
ARM_SKIN_WEIGHTS={}
def build_continuous_arm(side):
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 du=unit(e-a);df=unit(w-e)
 rings=[]
 for t,r in [(0,.109),(.15,.116),(.35,.114),(.55,.105),(.70,.097),(.85,.090),(1,.085)]:
  blend=max(0,(t-.65)/.35)*.5
  rings.append((a+(e-a)*t,r,upper,fore,blend,unit(du*(1-t)+df*t)))
 for t,r in [(.15,.088),(.30,.095),(.50,.100),(.70,.091),(.85,.079),(1,.060)]:
  if t<.30:primary,secondary,blend=upper,fore,.5+t/.30*.5
  else:primary,secondary,blend=fore,hand,max(0,(t-.75)/.25)*.55
  rings.append((e+(w-e)*t,r,primary,secondary,blend,df))
 rings.append((w+df*.018,.056,fore,hand,.80,df))
 vertices=[];faces=[];bindings=[];segments=20;last_u=None
 for center,r,primary,secondary,blend,tangent in rings:
  u=unit(np.cross(tangent,[0,0,1]))
  if last_u is not None and np.dot(u,last_u)<0:u=-u
  last_u=u;v=unit(np.cross(tangent,u))
  for angle in np.linspace(0,math.tau,segments,endpoint=False):
   vertices.append(center+r*(math.cos(angle)*u+math.sin(angle)*v*.94))
   bindings.append((BN.index(primary),BN.index(secondary),blend))
 for row in range(len(rings)-1):
  for j in range(segments):
   i=row*segments+j;k=row*segments+(j+1)%segments
   faces.extend([[i,k,k+segments],[i,k+segments,i+segments]])
 start=len(vertices);vertices.append(rings[0][0]);bindings.append(bindings[0])
 end=len(vertices);vertices.append(rings[-1][0]);bindings.append(bindings[(len(rings)-1)*segments])
 for j in range(segments):
  faces.append([start,(j+1)%segments,j])
  base=(len(rings)-1)*segments;faces.append([end,base+j,base+(j+1)%segments])
 # Tube basis may turn from left to right; orient every face away from its local centerline.
 for face in faces:
  points=np.array([vertices[i] for i in face]);center=points.mean(axis=0)
  line=min((c for c,*_ in rings),key=lambda c:np.linalg.norm(center-c))
  normal=np.cross(points[1]-points[0],points[2]-points[0])
  outward=center-line
  if start in face:outward=-du
  elif end in face:outward=df
  if np.dot(normal,outward)<0:face.reverse()
 offset=len(V)
 add('continuous shark arm '+side,vertices,faces,1,upper)
 for i,(primary,secondary,blend) in enumerate(bindings):
  BI[offset+i]=primary;ARM_SKIN_WEIGHTS[offset+i]=(secondary,blend)

for side,s in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0])
 build_continuous_arm(side)
 ell('shoulder armor',a+[s*.05,.005,-.025],(.142,.09,.13),2,upper,16,10)
 shard('shoulder fin',a+[s*.08,0,-.025],a+[s*.22,.10,-.07],.047,.025,2,upper)
 mid=(e+w)*.5
 plate('forearm plate',[(mid[0]-.07,mid[1]+.105),(mid[0]+.07,mid[1]+.105),(mid[0]+.06,mid[1]-.10),(mid[0]-.06,mid[1]-.10)],.05,.05,2,fore)
 tube('forearm energy',[(mid[0],mid[1]+.06,.10),(mid[0],mid[1]-.06,.10)],.005,3,fore,6)
 ell('palm',w+[s*.008,-.04,.02],(.043,.06,.041),1,hand,12,8)
 for j in range(4):
  x=w[0]+(j-1.5)*.021
  tube('finger',[(x,w[1]-.02,.052),(x,w[1]-.076,.055),(x,w[1]-.079,.018)],.010,2,hand,6)
 tube('thumb',[w+[s*.045,-.025,.01],w+[s*.054,-.05,.045],w+[s*.033,-.067,.05]],.012,2,hand,6)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0])
 limb('pirate trouser thigh',h,k,.105,0,thigh)
 x=(h[0]+k[0])*.5
 plate('thigh armor',[(x-.08,.91),(x+.08,.91),(x+.078,.67),(x-.073,.66)],.055,.05,2,thigh)
 ell('knee',k,(.083,.071,.076),2,shin,12,8)
 limb('shin',k,f,.076,1,shin)
 x=(k[0]+f[0])*.5
 plate('shin armor',[(x-.063,.47),(x+.063,.47),(x+.056,.19),(x-.053,.19)],.040,.045,2,shin)
 tube('shin light',[(x,.41,.090),(x,.25,.088)],.006,3,shin,6)
 ell('boot',f+[0,-.050,.060],(.087,.066,.145),0,foot,16,8)
 # Armored cable bundles on the outer thighs.
 tube('leg cable',[h+[s*.10,-.05,-.05],h+[s*.13,-.17,-.07],k+[s*.09,.10,-.04]],.008,0,thigh,8)
# Additional pressure hardware and layered slate armor, all rigidly bound to the rig.
def bolt(name,c,bone,r=.009):
 ell(name,c,(r,r,r*.45),5,bone,6,4)
for s in [-1,1]:
 # Separate cheek armor, reinforced gill grilles, and cranial panel seams.
 plate('cheek armor',[(s*.10,1.75),(s*.168,1.73),(s*.163,1.64),(s*.12,1.625)],.145,.030,2,'Head')
 for j in range(4):
  tube('cheek gill grille',[(s*(.119+j*.008),1.72-j*.007,.17),(s*(.128+j*.008),1.68-j*.007,.173)],.0045,0,'Head',6)
 bolt('temple fastener',(s*.153,1.742,.157),'Head')
 # Clear breastplate edging and fasteners, larger enough to read at gameplay scale.
 tube('cuirass brass rim',[(s*.18,1.53,.19),(s*.24,1.50,.192),(s*.266,1.41,.192)],.007,4,'Chest',6)
 for bx,by in [(.056,1.51),(.218,1.49),(.204,1.36)]:bolt('chest captive bolt',(s*bx,by,.202),'Chest')
 for j in range(3):
  tube('rib grille',[(s*.205,1.37-j*.034,.111),(s*.265,1.39-j*.034,.116)],.008,0,'Chest',6)
 tube('pressure hose',[(s*.24,1.48,-.10),(s*.30,1.43,-.11),(s*.29,1.29,-.055),(s*.19,1.25,-.025)],.013,1,'Chest',8)
 # Small back-mounted water jets frame the existing dorsal fins.
 tube('water jet pod',[(s*.17,1.28,-.175),(s*.17,1.45,-.21)],.040,2,'Chest',10)
 ring('jet exhaust',(s*.17,1.27,-.174),.026,.008,1,'Chest',normal=(0,1,0),segments=12)
 ring('jet luminous throat',(s*.17,1.266,-.174),.016,.004,3,'Chest',normal=(0,1,0),segments=12)
ring('chest pressure dial',(0,1.40,.206),.039,.009,1,'Chest',segments=16)
ell('pressure dial lens',(0,1.40,.208),(.031,.031,.008),3,'Chest',12,6)
tube('dial indicator',[(0,1.40,.221),(.016,1.421,.221)],.0035,5,'Chest',6)
for side,s in [('L',-1),('R',1)]:
 upper='UpperArm'+side;fore='Forearm'+side;hand='Hand'+side
 a=np.array(BONES[upper][0]);e=np.array(BONES[fore][0]);w=np.array(BONES[hand][0]);mid=(e+w)*.5
 for j in range(3):
  tube('shoulder segmented trim',[(a[0]+s*(.00+j*.035),a[1]+.025,.11),(a[0]+s*(.015+j*.035),a[1]-.065,.104)],.006,1,upper,6)
 for dy in [-.078,.078]:
  for dx in [-.05,.05]:bolt('forearm armor screw',(mid[0]+dx,mid[1]+dy,.105),fore,.007)
 plate('wrist control module',[(w[0]-.033,w[1]+.066),(w[0]+.033,w[1]+.066),(w[0]+.033,w[1]+.021),(w[0]-.033,w[1]+.021)],.098,.018,1,fore)
 for j in range(3):
  tube('wrist luminous readout',[(w[0]-.022,w[1]+.034+j*.010,.116),(w[0]+.01+j*.004,w[1]+.034+j*.010,.116)],.0028,3,fore,6)
 tube('forearm flex conduit',[e+[s*.08,-.015,-.018],mid+[s*.103,0,-.01],w+[s*.061,.035,.01]],.011,0,fore,8)
 for j in range(4):bolt('knuckle cap',(w[0]+(j-1.5)*.021,w[1]-.025,.065),hand,.008)
 thigh='Thigh'+side;shin='Shin'+side;foot='Foot'+side
 h=np.array(BONES[thigh][0]);k=np.array(BONES[shin][0]);f=np.array(BONES[foot][0]);tx=(h[0]+k[0])*.5;sx=(k[0]+f[0])*.5
 for yy in [.86,.70]:
  for xx in [-.052,.052]:bolt('thigh plate screw',(tx+xx,yy,.098),thigh,.008)
 for j in range(3):tube('thigh cooling slot',[(tx-.042,.83-j*.028,.100),(tx+.042,.83-j*.028,.100)],.005,0,thigh,6)
 tube('shin hydraulic sleeve',[k+[s*.08,-.065,-.005],f+[s*.073,.15,-.005]],.020,1,shin,8)
 tube('shin piston rod',[f+[s*.073,.15,-.005],f+[s*.073,.04,-.005]],.010,5,shin,8)
 for yy in [.43,.22]:
  for xx in [-.042,.042]:bolt('shin plate screw',(sx+xx,yy,.080),shin,.007)
 ring('knee lock',(k[0],k[1],k[2]+.075),.039,.006,1,shin,segments=12)
 for j in range(3):
  tube('boot armored toe ribs',[(f[0]-.055,f[1]-.018,.115+j*.028),(f[0]+.055,f[1]-.018,.115+j*.028)],.007,2,foot,6)

# The ventral pigment follows the EXACT torso cross-sections with a tiny surface offset.
# It reads as white skin, not a raised oval plate or a second body part.
torso_sections=[(1.02,.16,.11,0),(1.25,.24,.15,0),(1.46,.30,.19,0),(1.57,.18,.13,0)]
def ventral_section(y):
 for lo,hi in zip(torso_sections,torso_sections[1:]):
  if lo[0]<=y<=hi[0]:
   t=(y-lo[0])/(hi[0]-lo[0]);return tuple(lo[i]*(1-t)+hi[i]*t for i in range(1,4))
 raise ValueError(y)
white_rows=[(1.02,.15),(1.12,.70),(1.25,.83),(1.46,.72),(1.57,.10)]
white_vertices=[];white_faces=[];slices=13
for y,width in white_rows:
 rx,rz,cz=ventral_section(y)
 for j in range(slices):
  a=math.pi/2+width*(2*j/(slices-1)-1)
  white_vertices.append((rx*math.cos(a)*1.014,y,cz+rz*math.sin(a)+.0025))
for row in range(len(white_rows)-1):
 for j in range(slices-1):
  a=row*slices+j;b=a+1;c=a+slices;d=c+1
  white_faces.extend([(a,d,b),(a,c,d)])
add('white shark ventral pigment',white_vertices,white_faces,5,'Chest')
tube('leather cross-body baldric',[(-.14,1.54,.17),(-.04,1.44,.204),(.10,1.32,.177),(.20,1.24,.13)],.029,0,'Chest',8)
for j in range(5):bolt('baldric stud',(-.09+j*.052,1.50-j*.047,.226-j*.008),'Chest',.006)
plate('belt buckle',[(-.055,1.046),(.055,1.046),(.055,.978),(-.055,.978)],.15,.024,4,'Hips')
plate('belt buckle inset',[(-.033,1.03),(.033,1.03),(.033,.995),(-.033,.995)],.174,.005,0,'Hips')
tube('broad leather waist belt',[(-.18,1.015,.055),(-.13,1.015,.135),(0,1.015,.153),(.13,1.015,.135),(.18,1.015,.055)],.026,0,'Hips',8)
for side,sign in [('L',-1),('R',1)]:
 a=np.array(BONES['UpperArm'+side][0]);bone='UpperArm'+side
 tube('pauldron brass border',[a+[sign*.02,.07,.10],a+[sign*.13,.04,.137],a+[sign*.17,-.055,.09]],.009,4,bone,8)
 for j in range(3):
  shard('pauldron tooth',a+[sign*(.07+j*.045),.035-j*.025,-.015],a+[sign*(.13+j*.048),.125-j*.02,-.025],.025,.018,4,bone)
 # A visible leather belt and buckle on each upper arm.
 tube('arm leather strap',[a+[sign*.05,-.13,.06],a+[sign*.085,-.17,.07],a+[sign*.10,-.19,.03]],.013,0,bone,6)
for sign,side in [(-1,'L'),(1,'R')]:
 plate('open naval vest',[(sign*.135,1.55),(sign*.245,1.49),(sign*.218,1.24),(sign*.174,1.075),(sign*.085,1.095),(sign*.115,1.30)],.142,.035,2,'Chest')
 tube('vest brass lapel',[(sign*.135,1.55,.17),(sign*.115,1.30,.18),(sign*.085,1.095,.18)],.008,4,'Chest',6)
 for j in range(4):tube('vest gold braid',[(sign*.115,1.42-j*.072,.184),(sign*.196,1.415-j*.072,.176)],.0045,4,'Chest',6)
 plate('split coat tail',[(sign*.082,1.07),(sign*.22,1.07),(sign*.27,.73),(sign*.235,.56),(sign*.115,.65)],.077,.024,2,'Thigh'+side)
 tube('coat tail piping',[(sign*.085,1.05,.1),(sign*.117,.66,.095),(sign*.231,.57,.096)],.007,4,'Thigh'+side,6)
 # Leather boot panels and straps complement the exposed grey arms.
 for yy in [.24,.38]:
  tube('boot leather strap',[(sign*.11,yy,.073),(sign*.16,yy,.095),(sign*.22,yy,.06)],.013,0,'Shin'+side,6)

# Neutral tail is centred behind the pelvis; runtime adds a slow +/-45 degree sweep.
tail_start=len(V)
tail=np.array([(0,.97,-.14),(0,.79,-.30),(0,.49,-.48),(0,.28,-.69),(0,.36,-.88),(0,.63,-1.02)])
tr=[.105,.095,.074,.055,.038,.022];tp=[];radii=[]
for i in range(len(tail)-1):
 p0=tail[max(0,i-1)];p1=tail[i];p2=tail[i+1];p3=tail[min(len(tail)-1,i+2)]
 for t in np.linspace(0,1,5,endpoint=False):
  tp.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t));radii.append(tr[i]*(1-t)+tr[i+1]*t)
tp.append(tail[-1]);radii.append(tr[-1]);tube('shark tail',tp,radii,1,'Hips',14)
fluke_start=len(V)
shard('tail upper lobe',(0,.61,-1.02),(0,.91,-1.16),.067,.023,1,'Hips')
shard('tail lower lobe',(0,.61,-1.02),(0,.37,-1.17),.044,.018,1,'Hips')
# Double only the terminal fin about its attachment, keeping the tail stalk intact.
fluke_pivot=np.array((0,.61,-1.02))
for i in range(fluke_start,len(V)):
 V[i]=(fluke_pivot+(np.array(V[i])-fluke_pivot)*2.0).tolist()
tail_end=len(V)
# Small healed scars sit proud of exposed skin and follow its bone, avoiding decal flicker.
tube('snout old scar',[(-.072,1.803,.351),(-.042,1.812,.351),(-.016,1.815,.347)],.003,5,'Head',6)
tube('chest healed scar',[(-.055,1.48,.191),(-.035,1.455,.198),(-.008,1.428,.20)],.0035,5,'Chest',6)
for side,sign in [('L',-1),('R',1)]:
 bone='UpperArm'+side;a=np.array(BONES[bone][0]);e=np.array(BONES['Forearm'+side][0]);c=a+(e-a)*.55
 tube('upper arm claw scar',[c+[-.038,.028,.107],c+[-.012,.008,.116],c+[.022,-.017,.11]],.003,5,bone,6)
 for j in range(3):
  cx=c[0]-.026+j*.018;cy=c[1]+.02-j*.013
  tube('small scar cross mark',[(cx-.004,cy-.008,c[2]+.115),(cx+.004,cy+.008,c[2]+.115)],.002,5,bone,5)
 w=np.array(BONES['Hand'+side][0]);tube('hand scar',[w+[-.015,-.030,.066],w+[.013,-.048,.065]],.0025,5,'Hand'+side,6)

# Free-hand chain hook: independent group, so anchor scaling/skins/projectiles cannot affect it.
GROUP=1
hook_start=len(V)
hook_grip=np.array(BONES['WeaponGripL'][0])+[-.012,-.052,.070]
def hook_link(center,normal):
 n=unit(normal);u=unit(np.cross(n,[0,1,0]));v=unit(np.cross(n,u))
 vertices=[];faces=[];around=20;sides=6
 for j in range(around):
  angle=j*math.tau/around
  radial=unit(math.cos(angle)*u+math.sin(angle)*v)
  for k in range(sides):
   cross=k*math.tau/sides
   vertices.append(center+.018*math.cos(angle)*u+.034*math.sin(angle)*v+
                   .007*(math.cos(cross)*radial+math.sin(cross)*n))
 for j in range(around):
  for k in range(sides):
   a=j*sides+k;b=((j+1)%around)*sides+k
   c=((j+1)%around)*sides+(k+1)%sides;d=j*sides+(k+1)%sides
   faces.extend([(a,b,c),(a,c,d)])
 add('free hand interlocked hook chain',vertices,faces,2,'WeaponGripL')
for j in range(7):
 hook_link(hook_grip+[0,-j*.049,0],(0,0,1) if j%2==0 else (1,0,0))
hook_eye=hook_grip+[0,-6*.049-.056,0]
ring('forged hook eye',hook_eye,.030,.010,2,'WeaponGripL',segments=20)
tube('long forged hook shank',[hook_eye+[0,-.028,0],hook_eye+[0,-.325,0],
      hook_eye+[-.065,-.365,0],hook_eye+[-.132,-.315,0],hook_eye+[-.156,-.247,0]],
     [.019,.023,.022,.015,.001],4,'WeaponGripL',12)
hook_end=len(V)

# Epic skins recolor the original right-hand anchor mesh independently.
GROUP=2
g=np.array(BONES['WeaponGripR'][0]);x=g[0]+.022;z=g[2]+.055
limb('anchor shank',(x,g[1]+.16,z),(x,g[1]-.46,z),.034,2,'WeaponGripR')
ring('anchor handle',(x,g[1]+.21,z),.069,.014,2,'WeaponGripR',segments=20)
tube('anchor crossbar',[(x-.17,g[1]-.12,z),(x+.17,g[1]-.12,z)],.029,2,'WeaponGripR',10)
tube('anchor curved arms',[(x-.24,g[1]-.30,z),(x-.18,g[1]-.44,z),(x,g[1]-.49,z),(x+.18,g[1]-.44,z),(x+.24,g[1]-.30,z)],.034,2,'WeaponGripR',12)
for s in [-1,1]:
 shard('anchor fluke',(x+s*.19,g[1]-.43,z),(x+s*.25,g[1]-.25,z),.066,.035,2,'WeaponGripR')
 tube('anchor cyan channel',[(x+s*.11,g[1]-.445,z+.032),(x+s*.20,g[1]-.34,z+.032)],.006,3,'WeaponGripR',6)
for j in range(6):ring('short grip chain',(x+.10,g[1]+.12-j*.048,z-.02),.029,.006,1,'WeaponGripR',normal=(0,0,1) if j%2 else (1,0,0),segments=12)
# Bolted anchor housing, pressure core and engraved grip bands.
ring('anchor pressure core',(x,g[1]-.12,z+.036),.053,.009,2,'WeaponGripR',segments=16)
ell('anchor core lens',(x,g[1]-.12,z+.044),(.040,.040,.010),3,'WeaponGripR',12,6)
for s in [-1,1]:
 for j in range(3):bolt('anchor crossbar bolt',(x+s*(.065+j*.039),g[1]-.12,z+.031),'WeaponGripR',.008)
for j in range(5):ring('anchor grip band',(x,g[1]+.09-j*.040,z),.034,.0045,5,'WeaponGripR',normal=(0,1,0),segments=12)
tube('anchor power lead',[(x-.035,g[1]+.13,z),(x-.06,g[1]-.01,z+.02),(x-.05,g[1]-.10,z+.03)],.007,3,'WeaponGripR',6)
# Double the standard anchor about its grip, preserving hand contact and bone binding.
anchor_pivot=np.array((x,g[1],z))
for i,group in enumerate(GI):
 if group==2:V[i]=(anchor_pivot+(np.array(V[i])-anchor_pivot)*2.0).tolist()
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mesh=bpy.data.meshes.new('NerokCyberShark');mesh.from_pydata(V,[],F);mesh.update()
obj=bpy.data.objects.new('Nerok Abyss',mesh);bpy.context.collection.objects.link(obj)
for idx,color in enumerate(COLORS):
 mat=bpy.data.materials.new('Nerok slot '+str(idx));mat.diffuse_color=(*color,1);mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*color,1);bsdf.inputs['Metallic'].default_value=(.55 if idx==4 else .15 if idx==2 else 0);bsdf.inputs['Roughness'].default_value=.58;obj.data.materials.append(mat)
for face,m in zip(mesh.polygons,MI):face.material_index=m;face.use_smooth=True
for idx,name in enumerate(BN):
 group=obj.vertex_groups.new(name=name);indices=[i for i,b in enumerate(BI) if b==idx]
 if indices:group.add(indices,1,'REPLACE')
for vi,(secondary,blend) in ARM_SKIN_WEIGHTS.items():
 if blend>0:
  obj.vertex_groups[BN[BI[vi]]].add([vi],1-blend,'REPLACE')
  obj.vertex_groups[BN[secondary]].add([vi],blend,'REPLACE')
tail_group=obj.vertex_groups.new(name='Tail')
tail_group.add(list(range(tail_start,tail_end)),1,'REPLACE')
obj.vertex_groups['Hips'].remove(list(range(tail_start,tail_end)))
mesh.calc_loop_triangles()
vertices=[];normals=[];bones=[];bones2=[];weights2=[];groups=[];tail_vertices=[];subs=[{'triangles':[]} for _ in COLORS]
for tri in mesh.loop_triangles:
 for loop in tri.loops:
  vi=mesh.loops[loop].vertex_index;idx=len(bones)
  vertices.extend(mesh.vertices[vi].co);normals.extend(mesh.corner_normals[loop].vector)
  bones.append(BI[vi])
  secondary,blend=ARM_SKIN_WEIGHTS.get(vi,(BI[vi],0.0))
  bones2.append(secondary);weights2.append(blend)
  groups.append(GI[vi]);subs[tri.material_index]['triangles'].append(idx)
  if tail_start<=vi<tail_end:tail_vertices.append(idx)
data={'version':1,'boneNames':BN,'anchors':[{'head':BONES[b][0],'tail':BONES[b][1]} for b in BN],
 'vertices':vertices,'normals':normals,'boneIndices':bones,'boneIndices2':bones2,'boneWeights2':weights2,'groups':groups,'submeshes':subs,'markers':[],
 'tailVertices':tail_vertices,'tailPivot':[0,.97,-.14],
 'hookGrip':hook_grip.tolist()}
with gzip.GzipFile(filename=str(OUT/'Model.bytes'),mode='wb',mtime=0) as f:f.write(json.dumps(data,separators=(',',':')).encode())
# Blender-authored effect meshes are exported once and reused by the runtime pool.
def export_effect(name,verts,faces,alpha):
 m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update();m.calc_loop_triangles()
 ob=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(ob);ob.hide_render=True
 color=m.color_attributes.new(name='EffectAlpha',type='FLOAT_COLOR',domain='POINT')
 for i,a in enumerate(alpha):color.data[i].color=(1,1,1,a)
 indices=[v for t in m.loop_triangles for v in t.vertices]
 payload={'vertices':[float(c) for v in m.vertices for c in v.co], 'triangles':indices,'alpha':alpha}
 (OUT/(name+'.bytes')).write_text(json.dumps(payload,separators=(',',':')))
 ob.rotation_euler.x=math.pi/2
# One pooled chain mesh: interlocked oval links along local +Z, stretched to the live endpoints.
cv=[];cf=[];links=24;around=12;sides=6
for link in range(links):
 base=len(cv);center=(link+.5)/links
 for j in range(around):
  angle=j*math.tau/around
  for k in range(sides):
   cross=k*math.tau/sides
   xx=(.028+.006*math.cos(cross))*math.cos(angle);yy=.006*math.sin(cross)
   zz=center+(.031+.006*math.cos(cross))*math.sin(angle)
   cv.append((xx,yy,zz) if link%2==0 else (-yy,xx,zz))
 for j in range(around):
  for k in range(sides):
   a=base+j*sides+k;b=base+((j+1)%around)*sides+k
   c=base+((j+1)%around)*sides+(k+1)%sides;d=base+j*sides+(k+1)%sides
   cf.extend([(a,b,c),(a,c,d)])
export_effect('AnchorChain',cv,cf,[1]*len(cv))

# Three feathered spiral ribbons, visibly descending into the vortex center.
v=[];f=[];a=[]
for arm in range(3):
 base=len(v)
 for i in range(65):
  t=i/64;angle=arm*2*math.pi/3+t*math.pi*2.3;r=.12+.82*t
  for edge in [-1,0,1]:
   rr=r+edge*.045*(.35+t);v.append((rr*math.cos(angle),.035+.19*(1-t),rr*math.sin(angle)))
   a.append(0 if edge else .80*math.sin(math.pi*t)**.35)
 for i in range(64):
  for edge in range(2):
   j=base+i*3+edge;f.extend([(j,j+1,j+4),(j,j+4,j+3)])
export_effect('WaterSpiral',v,f,a)
# A low curved wave crest, with a transparent upper rim and translucent lower margin.
v=[];f=[];a=[]
for i in range(49):
 angle=-1.2+2.4*i/48
 for j in range(4):
  height=[0,.08,.18,.25][j];r=[.82,.94,1,.99][j]
  v.append((r*math.sin(angle),height,r*math.cos(angle)));a.append([0,.65,.8,0][j]*math.sin(math.pi*i/48)**.4)
for i in range(48):
 for j in range(3):
  k=i*4+j;f.extend([(k,k+1,k+5),(k,k+5,k+4)])
export_effect('WaveCrest',v,f,a)
# The flying anchor uses the actual standard anchor geometry, normalized about its grip.
indices=[i for i,g in enumerate(GI) if g==2];remap={old:new for new,old in enumerate(indices)}
v=[((V[i][0]-x)*1.4,(V[i][1]-g[1])*1.4,(V[i][2]-z)*1.4) for i in indices]
f=[tuple(remap[i] for i in face) for face in F if all(i in remap for i in face)]
export_effect('AnchorProjectile',v,f,[.9]*len(v))
# Rust-brown preview material is assigned after export; runtime retains six material slots.
hook_mat=bpy.data.materials.new('Rust brown chain hook');hook_mat.diffuse_color=(.35,.14,.045,1);hook_mat.use_nodes=True
hook_bsdf=hook_mat.node_tree.nodes.get('Principled BSDF')
hook_bsdf.inputs['Metallic'].default_value=.30;hook_bsdf.inputs['Roughness'].default_value=.74
noise=hook_mat.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=38
ramp=hook_mat.node_tree.nodes.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].color=(.12,.037,.009,1);ramp.color_ramp.elements[1].color=(.42,.17,.050,1)
hook_mat.node_tree.links.new(noise.outputs['Fac'],ramp.inputs['Fac'])
hook_mat.node_tree.links.new(ramp.outputs['Color'],hook_bsdf.inputs['Base Color'])
obj.data.materials.append(hook_mat)
dark_hook_mat=hook_mat.copy();dark_hook_mat.name='Darker rust brown hook';dark_hook_mat.diffuse_color=(.24,.085,.025,1)
dark_ramp=next(n for n in dark_hook_mat.node_tree.nodes if n.type=='VALTORGB')
dark_ramp.color_ramp.elements[0].color=(.08,.020,.006,1);dark_ramp.color_ramp.elements[1].color=(.28,.095,.026,1)
obj.data.materials.append(dark_hook_mat)
for face in obj.data.polygons:
 if GI[face.vertices[0]]==1:face.material_index=len(obj.data.materials)-(1 if face.material_index==4 else 2)
# Keep Blender's preview upright while exported gameplay geometry remains +Y up.
obj.rotation_euler.x=math.pi/2
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'NerokCyberShark.blend'),compress=True)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=700;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.color=(.025,.025,.025);scene.view_settings.view_transform='AgX';scene.view_settings.exposure=-.25
def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.4,-4.0,1.8));cam=bpy.context.object;aim(cam,(0,0,1.02));scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.4
for pos,power,size in [((2,-4,3),230,3),((-3,-1,2),280,3),((0,3,2),220,2)]:
 bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;aim(light,(0,0,1))
scene.render.filepath=str(ART/'preview.png');bpy.ops.render.render(write_still=True)
print('NEROK_EXPORT',len(bones),'vertices',len(bones)//3,'triangles',len(PARTS),'parts')
# Keep an editable armature in the source .blend, using the same bones as the game exporter.
armdata=bpy.data.armatures.new('NerokArmature');arm=bpy.data.objects.new('NerokArmature',armdata);bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name in BN:
 bone=armdata.edit_bones.new(name);bone.head=BONES[name][0];bone.tail=BONES[name][1]
tail_bone=armdata.edit_bones.new('Tail');tail_bone.head=(0,.97,-.14);tail_bone.tail=(0,.79,-.30);tail_bone.parent=armdata.edit_bones['Hips']
for name in BN:
 parent='Hips' if name=='Spine' else 'Spine' if name=='Chest' else 'Chest' if name=='Head' or name.startswith('UpperArm') else 'Hips' if name.startswith('Thigh') else 'UpperArm'+name[-1] if name.startswith('Forearm') else 'Forearm'+name[-1] if name.startswith('Hand') else 'Thigh'+name[-1] if name.startswith('Shin') else 'Shin'+name[-1] if name.startswith('Foot') else 'Hand'+name[-1] if name.startswith('WeaponGrip') else None
 if parent:armdata.edit_bones[name].parent=armdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');arm.rotation_euler.x=math.pi/2
modifier=obj.modifiers.new('Rigid hero skin','ARMATURE');modifier.object=arm
for ob in bpy.data.objects:
 if ob.type=='MESH':ob.hide_render=ob!=obj
cam.location=(2.4,-4,1.8);aim(cam,(0,0,1.02));cam.data.ortho_scale=2.4
scene.render.resolution_x=700;scene.render.resolution_y=900
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'NerokCyberShark.blend'),compress=True)

# Render the five independent pictorial attacks after saving the model scene.
import runpy,sys
if '--skip-icons' not in sys.argv:
 runpy.run_path(str(ROOT/"Tools/CyberShark/build_attack_icons.py"),run_name="__main__")

