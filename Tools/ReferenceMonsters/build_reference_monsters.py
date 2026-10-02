"""Original reference-led biped monsters. python build_reference_monsters.py <golem|skeleton|alien|psr>"""
from pathlib import Path
import json,math,gzip,sys
import numpy as np
from PIL import Image,ImageDraw
KIND=sys.argv[1] if len(sys.argv)>1 else 'golem'
NAME={'golem':'GJMagentaGolem','skeleton':'MarsSkeleton','alien':'GreenStalker','psr':'PSRCrystalTitan'}[KIND]
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/Alterverse/Resources'/NAME;ART=ROOT/'ArtSource/HotZoneMonsters'/NAME
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
POS={'':(0,0,0),'Hips':(0,1.55,0),'Hips/Chest':(0,2.15,0),'Hips/Chest/Head':(0,2.80,.10)}
for s,side in [(-1,'L'),(1,'R')]:
 POS['Hips/Chest/Arm'+side]=(s*.95,2.45,0)
 POS['Hips/Chest/Arm'+side+'/Elbow'+side]=(s*1.16,1.60,.03)
 POS['Hips/Chest/Arm'+side+'/Elbow'+side+'/Hand'+side]=(s*1.20,.83,.11)
 POS['Hips/Thigh'+side]=(s*.42,1.50,0)
 POS['Hips/Thigh'+side+'/Shin'+side]=(s*.43,.78,.08)
 POS['Hips/Thigh'+side+'/Shin'+side+'/Foot'+side]=(s*.43,.16,.20)
if KIND=='alien':
 POS['Hips']=(0,1.24,-.18);POS['Hips/Chest']=(0,1.92,.20);POS['Hips/Chest/Head']=(0,2.42,.46)
 for s,side in [(-1,'L'),(1,'R')]:
  POS['Hips/Chest/Arm'+side]=(s*.58,2.04,.13);POS['Hips/Chest/Arm'+side+'/Elbow'+side]=(s*.85,1.43,.30);POS['Hips/Chest/Arm'+side+'/Elbow'+side+'/Hand'+side]=(s*.99,.65,.52)
  POS['Hips/Thigh'+side]=(s*.28,1.20,-.18);POS['Hips/Thigh'+side+'/Shin'+side]=(s*.39,.66,.04);POS['Hips/Thigh'+side+'/Shin'+side+'/Foot'+side]=(s*.40,.16,.22)
# Shorten both arm segments at their joint origins before generating geometry.
# Hands retain their size; shoulders, legs and the alien remain unchanged.
if KIND in ('golem','skeleton','psr'):
 for side in ('L','R'):
  arm='Hips/Chest/Arm'+side;elbow=arm+'/Elbow'+side;hand=elbow+'/Hand'+side
  shoulder=np.array(POS[arm],float)
  for joint in (elbow,hand):
   POS[joint]=tuple(shoulder+(np.array(POS[joint])-shoulder)*.70)
BN=list(POS);BONES={n:None for n in BN};V=[];F=[];MI=[];BI=[];PARTS=[];GI=[];GROUP=0
COLORS=np.array({'golem':[[.085,.015,.061],[.44,.045,.29],[.72,.13,.43],[.87,.28,.86],[.28,.045,.18]],'skeleton':[[.075,.065,.06],[.59,.54,.42],[.76,.70,.54],[.62,.18,.85],[.26,.22,.16]],'alien':[[.041,.063,.056],[.23,.32,.26],[.46,.51,.39],[.43,.62,.24],[.095,.13,.10]],'psr':[[.045,.085,.12],[.44,.59,.65],[.74,.84,.88],[.06,.77,1.0],[.23,.38,.46]]}[KIND])
P=lambda name:np.array(POS[name],float)
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


def rock(name,c,size,bone,mat=1,seed=0):
 # Independent chipped stone blocks with flat triangle normals.
 start=len(V);first=len(F);ell(name,c,size,mat,bone,9 if KIND=='psr' else 10,5)
 rng=np.random.default_rng(seed+len(PARTS)*31);c=np.array(c)
 for j in range(start,len(V)):V[j]=(c+(np.array(V[j])-c)*rng.uniform(.86,1.12)).tolist()
 # Split corners so lighting exposes the fracture planes rather than smoothing them.
 original=[f[:] for f in F[first:]];del F[first:];del MI[first:]
 for face in original:
  offset=len(V);V.extend([V[i][:] for i in face]);BI.extend([BN.index(bone)]*3);GI.extend([0]*3);F.append([offset,offset+1,offset+2]);MI.append(mat)

def boneRod(name,a,b,r,bone):
 limb(name,a,b,r,1,bone)
 for p in [a,b]:ell(name+' epiphysis',p,(r*1.3,r*1.15,r*1.3),2,bone,12,8)

chest='Hips/Chest';head=chest+'/Head'
if KIND in ['golem','psr']:
 rock('pelvis keystone',P('Hips')+[0,-.04,0],(.55,.30,.35),'Hips',0)
 rock('abdominal core',P('Hips')+[0,.34,0],(.47,.42,.33),'Hips',0)
 rock('chest core',P(chest)+[0,.18,0],(.90,.55,.43),chest,0)
 for s in [-1,1]:
  rock('chest fractured slab',P(chest)+[s*.39,.26,.28],(.45,.39,.25),chest,1)
  for j in range(3):rock('rib stone',P(chest)+[s*.50,-.17-j*.10,.13],(.21,.13,.27),chest,2 if j==1 else 1)
  rock('waist interlocked block',P('Hips')+[s*.23,.19,.29],(.22,.21,.13),'Hips',1)
  rock('pelvic hanging slab',P('Hips')+[s*.30,-.13,.24],(.22,.30,.18),'Hips',2)
  for j in range(3):rock('back shoulder slab',P(chest)+[s*(.23+j*.16),.39-j*.08,-.29],(.23,.20,.19),chest,1)
 rock('head monolith',P(head),(.36,.39,.32),head,1)
 rock('broad brow',P(head)+[0,.16,.25],(.38,.12,.13),head,2)
 rock('lower jaw',P(head)+[0,-.25,.20],(.29,.15,.19),head,1)
 rock('nose stone',P(head)+[0,-.02,.32],(.105,.18,.115),head,2)
 for s in [-1,1]:
  ell('eye shadow',P(head)+[s*.16,.017,.301],(.108,.076,.035),0,head,16,8)
  shard('luminous eye',P(head)+[s*.10,.03,.336],P(head)+[s*.23,.035,.33],.032,.02,3,head)
  for j in range(3):rock('stone tooth',P(head)+[s*(.043+j*.066),-.18,.367],(.027,.037,.023),head,2)
 if KIND=='psr':
  shard('crown crystal',P(head)+[0,.27,0],P(head)+[0,.94,.015],.20,.19,2,head)
  for s in [-1,1]:shard('side crown prism',P(head)+[s*.17,.20,0],P(head)+[s*.31,.59,.01],.095,.10,1,head)
  shard('chest crystal heart',P(chest)+[0,-.04,.40],P(chest)+[0,.46,.43],.11,.08,3,chest)
 else:
  for j in range(3):rock('broken crown',P(head)+[(j-1)*.21,.31,-.06],(.16,.14,.24),head,2 if j==1 else 1)
elif KIND=='skeleton':
 # Open rib cage and articulated long bones, not a closed armored torso.
 for j in range(7):
  y=1.50+j*.135;ell('spinal vertebra',(0,y,0),(.13,.086,.12),1,'Hips' if y<1.9 else chest,12,8)
 for s in [-1,1]:
  tube('pelvic ilium',[(s*.10,1.52,.04),(s*.34,1.69,0),(s*.54,1.53,0),(s*.43,1.33,.055),(s*.14,1.29,.06)],.10,1,'Hips',10)
  for j in range(6):
   y=2.52-j*.145;w=.55-j*.035
   tube('curved rib',[(s*.10,y,-.12),(s*w,y-.015,-.06),(s*(w+.05),y-.05,.16),(s*.31,y-.10,.33),(s*.11,y-.075,.35)],[.049,.052,.047,.041,.032],1,chest,10)
  boneRod('clavicle',(s*.10,2.55,.18),(s*.90,2.48,.0),.065,chest)
  rock('scapula',(s*.56,2.32,-.17),(.21,.30,.085),chest,1)
 boneRod('sternum',(0,1.83,.31),(0,2.53,.31),.065,chest)
 ell('cranium',P(head)+[0,.08,0],(.32,.37,.27),1,head,28,16)
 for s in [-1,1]:
  ell('deep eye socket',P(head)+[s*.145,.045,.24],(.110,.113,.050),0,head,20,12)
  ell('purple pin eye',P(head)+[s*.145,.045,.289],(.027,.034,.009),3,head,12,8)
  boneRod('cheekbone',P(head)+[s*.24,-.01,.21],P(head)+[s*.17,-.18,.25],.05,head)
 tube('mandible', [P(head)+[-.23,-.17,.10],P(head)+[-.19,-.30,.24],P(head)+[0,-.33,.29],P(head)+[.19,-.30,.24],P(head)+[.23,-.17,.10]],.055,1,head,10)
 shard('nasal opening',P(head)+[0,-.16,.278],P(head)+[0,-.015,.283],.036,.016,0,head)
 for j in range(8):
  x=(j-3.5)*.044
  boneRod('upper tooth',P(head)+[x,-.17,.283],P(head)+[x,-.23,.285],.018,head)
  boneRod('lower tooth',P(head)+[x,-.25,.287],P(head)+[x,-.29,.283],.017,head)
else:
 # Hunched sinewy stalker with an elongated smooth cranium and long talons.
 ell('narrow pelvis',P('Hips'),(.28,.25,.22),1,'Hips',24,14)
 limb('slender abdomen',P('Hips'),P(chest)+[0,-.08,0],.24,1,'Hips')
 ell('hunched ribcage',P(chest)+[0,.06,-.10],(.42,.48,.32),1,chest,28,16)
 shell('swept elongated cranium',[(2.11,.055,.07,.65),(2.23,.19,.16,.60),(2.43,.34,.25,.50),(2.66,.48,.36,.33),(2.88,.44,.42,.12),(3.02,.28,.29,.02),(3.08,.018,.020,-.02)],2,head,36)
 for s in [-1,1]:
  ell('sunken almond eye',P(head)+[s*.21,-.09,.329],(.103,.043,.018),0,head,24,12)
  tube('orbital brow', [P(head)+[s*.13,-.025,.36],P(head)+[s*.24,-.005,.335],P(head)+[s*.31,.033,.28]],.021,1,head,10)
  for j in range(5):tube('lateral rib sinew', [P(chest)+[s*.12,.25-j*.10,.20],P(chest)+[s*.30,.18-j*.10,.16],P(chest)+[s*.39,.12-j*.10,.03]],.026,4,chest,9)
 tube('thin closed mouth',[P(head)+[-.14,-.30,.40],P(head)+[0,-.325,.444],P(head)+[.14,-.30,.40]],.010,0,head,8)
 for j in range(5):shard('dorsal spine',P(chest)+[0,.38-j*.16,-.34],P(chest)+[0,.60-j*.15,-.52],.058,.07,1,chest)

for s,side in [(-1,'L'),(1,'R')]:
 arm=chest+'/Arm'+side;elbow=arm+'/Elbow'+side;hand=elbow+'/Hand'+side
 thigh='Hips/Thigh'+side;shin=thigh+'/Shin'+side;foot=shin+'/Foot'+side
 a,e,w=P(arm),P(elbow),P(hand);h,k,f=P(thigh),P(shin),P(foot)
 if KIND in ['golem','psr']:
  for name,bone,start,end,r in [('upper arm',arm,a,e,.28),('forearm',elbow,e,w,.28),('thigh',thigh,h,k,.29),('shin',shin,k,f,.28)]:
   limb(name+' fissure core',start,end,r*.77,0,bone)
   for j in range(3):
    c=start+(end-start)*(j+.45)/3
    rock(name+' fractured block',c,(r,.19,r*.9),bone,1 if j%2==0 else 2)
    for q in [-1,1]:rock(name+' edge chip',c+[q*r*.61,.035,.11],(.15,.13,.16),bone,1)
  rock('huge shoulder boulder',a+[s*.05,.055,0],(.47,.43,.43),arm,1)
  for j in range(3):rock('shoulder fractured overlay',a+[s*(j-1)*.19,.28,.20],(.19,.15,.21),arm,2 if j==1 else 1)
  rock('fist palm',w+[0,-.12,.04],(.34,.32,.28),hand,1)
  for j in range(4):
   x=w[0]+(j-1.5)*.145
   rock('knuckle stone',(x,w[1]-.19,w[2]+.27),(.075,.13,.12),hand,2)
   rock('curled stone finger',(x,w[1]-.32,w[2]+.12),(.070,.10,.13),hand,1)
  rock('opposing thumb',w+[-s*.29,-.08,.19],(.12,.20,.15),hand,1)
  rock('broad stone foot',f+[0,-.04,.10],(.35,.14,.41),foot,1)
  for j in range(3):rock('stone toe',f+[(j-1)*.20,-.06,.40],(.11,.10,.15),foot,2)
  if KIND=='golem':
   for j in range(3):
    tube('dark mineral root', [a+[s*(j-1)*.14,.02,-.28],e+[s*(j-1)*.12,.15,-.24],w+[s*(j-1)*.10,.06,-.20]],.019,4,elbow,8)
 elif KIND=='skeleton':
  boneRod('humerus',a,e,.086,arm)
  boneRod('radius',e+[s*.055,0,.035],w+[s*.06,0,.045],.049,elbow)
  boneRod('ulna',e+[-s*.055,0,-.018],w+[-s*.06,0,-.014],.054,elbow)
  boneRod('femur',h,k,.10,thigh)
  boneRod('tibia',k+[.04,0,.05],f+[.04,.02,.02],.066,shin)
  boneRod('fibula',k+[-.065,0,-.012],f+[-.060,.02,-.014],.039,shin)
  ell('patella',k+[0,.01,.11],(.10,.105,.045),2,shin,16,8)
  # Five metacarpals and three phalanges per finger, with a separate opposable thumb.
  for j in range(4):
   dx=(j-1.5)*.079;end=w+[dx,-.19,.04]
   boneRod('metacarpal',w+[dx*.40,0,0],end,.022,hand)
   points=[end,end+[dx*.15,-.13,.025],end+[dx*.22,-.23,.083],end+[dx*.18,-.29,.14]]
   for q in range(3):boneRod('finger phalanx',points[q],points[q+1],.020-q*.003,hand)
  points=[w+[-s*.11,-.06,.02],w+[-s*.23,-.16,.12],w+[-s*.25,-.26,.19]]
  for q in range(2):boneRod('thumb phalanx',points[q],points[q+1],.025-q*.004,hand)
  ell('calcaneus heel',f+[0,-.055,-.08],(.11,.075,.14),1,foot,16,8)
  for j in range(5):
   dx=(j-2)*.07;a0=f+[dx,-.065,.03];b0=f+[dx*1.08,-.075,.25];c0=f+[dx*1.11,-.077,.34-(j*.015)]
   boneRod('metatarsal',a0,b0,.021,foot);boneRod('toe phalanx',b0,c0,.019,foot)
 else:
  ell('shoulder tendon cap',a,(.12,.15,.115),1,arm,20,12)
  limb('sinewy upper arm',a,e,.079,1,arm);limb('long alien forearm',e,w,.068,1,elbow)
  ell('angular elbow',e,(.085,.09,.075),2,elbow,16,8)
  tube('forearm tendon',[e+[s*.026,0,.049],w+[s*.034,.03,.045]],.016,2,elbow,8)
  ell('clawed palm',w+[0,-.08,0],(.11,.16,.067),1,hand,20,12)
  for j in range(4):
   dx=(j-1.5)*.066;length=.39+(1-abs(j-1.5)/2)*.14
   tube('long separated talon',[w+[dx,-.12,.015],w+[dx*1.65,-.29,.085],w+[dx*1.9,-length,.14],w+[dx*1.55,-length-.10,.28]],[.031,.025,.017,.001],1,hand,10)
  tube('alien opposing thumb',[w+[-s*.086,-.03,.005],w+[-s*.19,-.16,.08],w+[-s*.21,-.36,.23]],[.038,.026,.001],1,hand,10)
  limb('digitigrade thigh',h,k,.12,1,thigh);limb('tapered shin',k,f,.072,1,shin)
  tube('rear leg tendon',[k+[0,0,-.069],f+[0,.03,-.04]],.020,2,shin,8)
  for j in range(3):tube('long toe claw',[f+[(j-1)*.07,-.02,.03],f+[(j-1)*.11,-.085,.24],f+[(j-1)*.14,-.13,.47-abs(j-1)*.07]],[.045,.029,.001],1,foot,10)

# Compact away construction vertices, retaining flat facets where authored.
used=sorted(set(i for face in F for i in face));remap={v:i for i,v in enumerate(used)}
V=[V[i] for i in used];BI=[BI[i] for i in used];F=[[remap[i] for i in face] for face in F]
vertices=np.array(V);triangles=np.array(F,dtype=np.int32);normals=np.zeros_like(vertices)
for ids in triangles:
 a,b,c=vertices[ids];n=np.cross(b-a,c-a)
 for j in ids:normals[j]+=n
normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-10)
local=vertices-np.array([POS[BN[b]] for b in BI])
joints=[]
for name in BN:
 if not name:continue
 parent=name.rsplit('/',1)[0] if '/' in name else ''
 joints.append({'path':name,'parent':parent,'position':(P(name)-P(parent)).tolist()})
mesh={'version':1,'bonePaths':BN,'joints':joints,'vertices':np.round(local,6).ravel().tolist(),'normals':np.round(normals,6).ravel().tolist(),'boneIndices':BI,'submeshes':[{'triangles':triangles[np.array(MI)==m].ravel().tolist()} for m in range(5)]}
(OUT/'Model.bytes').write_bytes(gzip.compress(json.dumps(mesh,separators=(',',':')).encode(),mtime=0))
report={'kind':KIND,'vertices':len(V),'triangles':len(F),'bones':len(BN),'parts':len(PARTS),'bounds':[vertices.min(0).tolist(),vertices.max(0).tolist()]}
assert np.isfinite(vertices).all() and np.isfinite(normals).all() and triangles.max()<len(V) and len(V)<65535
assert vertices[:,1].min()>-.12
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
# Offline rasterizer: render exact exported geometry with interpolated normals.
def render(yaw,name):
 w,h=800,960;scale=224;angle=math.radians(yaw);tilt=math.radians(-5)
 ry=np.array([[math.cos(angle),0,math.sin(angle)],[0,1,0],[-math.sin(angle),0,math.cos(angle)]])
 rx=np.array([[1,0,0],[0,math.cos(tilt),-math.sin(tilt)],[0,math.sin(tilt),math.cos(tilt)]])
 rotation=rx@ry;p=(vertices-np.array([0,1.80,0]))@rotation.T;n=normals@rotation.T
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
 image=Image.fromarray((np.clip(img,0,1)**(1/1.7)*255).astype(np.uint8));d=ImageDraw.Draw(image);d.text((20,20),NAME.upper(),fill=(195,224,214));d.text((20,h-28),'Geometry preview - not a Unity screenshot',fill=(130,155,157));image.save(ART/name)
render(-22,'front.png');render(155,'back.png')
