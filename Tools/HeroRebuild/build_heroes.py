"""DIMENSIONS FRONT authored hero pipeline. Run with Blender 4.5 LTS --background --python.
Anatomy: Blender Human Base Meshes 1.4.1 (CC0). All costumes/weapons/rigs below original.
"""
import bpy, math, os, sys, json, random
from pathlib import Path
from mathutils import Vector, Matrix
from math import sin, cos, pi
ROOT=str(next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir()))
SOURCE=os.environ.get('DF_HUMAN_BASE_MESH_SOURCE',os.path.join(ROOT,'Tools/HeroRebuild/source/base_meshes/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend'))
OUT=os.path.join(ROOT,'Assets/Alterverse/Resources/AuthoredHeroes'); os.makedirs(OUT,exist_ok=True)
PREVIEW=os.path.join(ROOT,'ArtSource/Heroes/Preview');os.makedirs(PREVIEW,exist_ok=True)
HEROES=[('Veyr',False,(.23,.065,.48),'ZHAEV NULL'),('Kaelon',False,(.035,.23,.30),'AUREN KEST'),('Nyra',True,(.025,.22,.30),'SIVRA QUELL'),('Elyra',True,(.24,.075,.42),'IONE VASKA'),('Mira',True,(.035,.30,.16),'TAHLA VENN'),('Rook',False,(.47,.095,.025),'BRAKK ORUN'),('Traveler',False,(.035,.19,.48),'THE TRAVELER')]
def mat(name,col,metal=0,rough=.4,emission=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*col,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 bs.inputs['Emission Color'].default_value=(*col,1);bs.inputs['Emission Strength'].default_value=emission
 return m
parts=[]
def mesh(name,verts,faces,material,bone='Chest',bevel=0):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);o.data.materials.append(material)
 for p in me.polygons:p.use_smooth=True
 if bevel:
  b=o.modifiers.new('Machined edges','BEVEL');b.width=bevel;b.segments=3
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=b.name)
  for p in o.data.polygons:p.use_smooth=True
 o['bone']=bone;parts.append(o);return o

def tube(name,points,r,material,bone='Chest',sides=10):
 vs=[];fs=[]
 for i,p in enumerate(points):
  p=Vector(p);t=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
  t.normalize();u=t.cross(Vector((0,1,0)))
  if u.length<.01:u=t.cross(Vector((1,0,0)))
  u.normalize();v=t.cross(u);radius=r[i] if isinstance(r,list) else r
  for j in range(sides):vs.append(p+radius*(cos(j*2*pi/sides)*u+sin(j*2*pi/sides)*v))
 for i in range(len(points)-1):
  for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;fs.append((a,b,b+sides,a+sides))
 fs.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))])
 return mesh(name,vs,fs,material,bone)

def ellipsoid(name,center,scale,material,bone='Chest',seg=24,rings=14):
 vs=[];fs=[]
 for i in range(rings+1):
  a=pi*i/rings
  for j in range(seg):b=2*pi*j/seg;vs.append(tuple(center[k]+scale[k]*[sin(a)*cos(b),sin(a)*sin(b),cos(a)][k] for k in range(3)))
 for i in range(rings):
  for j in range(seg):fs.append((i*seg+j,i*seg+(j+1)%seg,(i+1)*seg+(j+1)%seg,(i+1)*seg+j))
 return mesh(name,vs,fs,material,bone)

def plate(name,outline,y,depth,material,bone='Chest'):
 # Front and rear curved faces with a raised central ridge, not flat primitive boxes.
 n=len(outline);vs=[(x,y+.09*x*x,z) for x,z in outline]+[(x,y+depth+.09*x*x,z) for x,z in outline]
 cx=sum(x for x,z in outline)/n;cz=sum(z for x,z in outline)/n;vs.append((cx,y-.035,cz));fs=[]
 for i in range(n):j=(i+1)%n;fs.extend([(i,j,2*n),(i,n+i,n+j,j)])
 fs.append(tuple(n+i for i in reversed(range(n))))
 return mesh(name,vs,fs,material,bone,.009)

def cape(name,z_top,length,width,material,bone='Chest',side=0):
 vs=[];fs=[];rows=22;cols=20
 for i in range(rows):
  t=i/(rows-1)
  for j in range(cols):
   u=j/(cols-1)*2-1;x=side+u*width*(.50+.65*t);z=z_top-length*t+.065*cos(u*pi*2)*t*t;y=.11+.11*t+.035*cos(u*pi*7)*t
   vs.append((x,y,z))
 for i in range(rows-1):
  for j in range(cols-1):a=i*cols+j;fs.append((a,a+1,a+cols+1,a+cols))
 o=mesh(name,vs,fs,material,bone);s=o.modifiers.new('Fabric thickness','SOLIDIFY');s.thickness=.006;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=s.name);return o

def limb_bone(v):
 x,y,z=v
 if z>1.44:return 'Head'
 if abs(x)>.24 and z>.79:
  side='L' if x<0 else 'R'
  return ('Hand' if z<.99 else 'Forearm' if z<1.20 else 'UpperArm')+side
 if z<.93:
  side='L' if x<0 else 'R';return ('Foot' if z<.16 else 'Shin' if z<.49 else 'Thigh')+side
 return 'Chest' if z>1.12 else 'Spine' if z>1.0 else 'Hips'

def rig():
 data=bpy.data.armatures.new('DF_Skeleton');ob=bpy.data.objects.new('DF_Skeleton',data);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 bones={}
 def b(n,h,t,parent=None):
  x=data.edit_bones.new(n);x.head=h;x.tail=t
  if parent:x.parent=bones[parent]
  bones[n]=x
 b('Root',(0,0,0),(0,0,.15));b('Hips',(0,0,.90),(0,0,1.03),'Root');b('Spine',(0,0,1.03),(0,0,1.20),'Hips');b('Chest',(0,0,1.20),(0,0,1.42),'Spine');b('Head',(0,0,1.42),(0,0,1.75),'Chest')
 for s,k in [(-1,'L'),(1,'R')]:
  b('UpperArm'+k,(s*.18,0,1.39),(s*.31,0,1.16),'Chest');b('Forearm'+k,(s*.31,0,1.16),(s*.41,-.01,.95),'UpperArm'+k);b('Hand'+k,(s*.41,-.01,.95),(s*.43,-.03,.86),'Forearm'+k)
  b('Thigh'+k,(s*.13,0,.9),(s*.14,.01,.49),'Hips');b('Shin'+k,(s*.14,.01,.49),(s*.18,.02,.12),'Thigh'+k);b('Foot'+k,(s*.18,.02,.12),(s*.18,-.15,.06),'Shin'+k)
 bpy.ops.object.mode_set(mode='OBJECT');ob.select_set(False);return ob

def bind(o,arm,body=False):
 names=[b.name for b in arm.data.bones]
 for n in names:o.vertex_groups.new(name=n)
 if body:
  # Smooth proximity weights across neighboring bones; plate weights remain rigid.
  segments=[(b.name,b.head_local,b.tail_local) for b in arm.data.bones if b.name!='Root']
  for v in o.data.vertices:
   group=limb_bone(v.co); candidates=[(group,1.0)]
   # Feather at elbows/knees/waist to avoid a hard seam.
   x,y,z=v.co;s='L' if x<0 else 'R'
   if abs(x)<.20 and .88<z<1.12:candidates=[('Hips',max(0,(1.08-z)/.20)),('Spine',max(0,1-abs(z-1.08)/.20)),('Chest',max(0,(z-1.06)/.20))]
   elif z<.93 and .40<z<.58:t=(z-.4)/.18;candidates=[('Shin'+s,1-t),('Thigh'+s,t)]
   total=sum(w for n,w in candidates)
   for n,w in candidates:
    if w>0:o.vertex_groups[n].add([v.index],w/total,'REPLACE')
 else:o.vertex_groups[o.get('bone','Chest')].add(list(range(len(o.data.vertices))),1,'REPLACE')
 mod=o.modifiers.new('DF_Skin','ARMATURE');mod.object=arm;o.parent=arm

def animations(arm,hero):
 for name,length in [('Idle',60),('Run',24),('Attack',24),('Skill0',30),('Skill1',30),('Skill2',36),('Ultimate',48),('Hit',14),('Death',36)]:
  action=bpy.data.actions.new(name);arm.animation_data_create();arm.animation_data.action=action
  for f in list(range(1,length,3))+[length]:
   t=(f-1)/(length-1);wave=sin(t*2*pi);attack=sin(t*pi)
   for b in arm.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
   if name=='Idle':arm.pose.bones['Chest'].rotation_euler.x=.018*wave;arm.pose.bones['Head'].rotation_euler.z=.025*wave
   elif name=='Run':
    for side,sgn in [('L',1),('R',-1)]:
     arm.pose.bones['Thigh'+side].rotation_euler.x=.45*wave*sgn;arm.pose.bones['Shin'+side].rotation_euler.x=-.4*max(0,wave*sgn);arm.pose.bones['UpperArm'+side].rotation_euler.x=-.32*wave*sgn
    arm.pose.bones['Hips'].location.z=.025*abs(wave);arm.pose.bones['Chest'].rotation_euler.x=.10
   elif name=='Death':arm.pose.bones['Root'].rotation_euler.x=1.45*min(1,t*1.3);arm.pose.bones['Hips'].location.z=-.12*t
   elif name=='Hit':arm.pose.bones['Chest'].rotation_euler.x=-.18*attack
   else:
    arm.pose.bones['Chest'].rotation_euler.z=.32*attack;arm.pose.bones['UpperArmR'].rotation_euler.x=-1.15*attack;arm.pose.bones['ForearmR'].rotation_euler.x=-.45*attack
    if name=='Attack':
     if hero=='Nyra':arm.pose.bones['Chest'].rotation_euler.z=.025*attack;arm.pose.bones['UpperArmR'].rotation_euler.x=-.15*attack;arm.pose.bones['ForearmR'].rotation_euler.x=-.09*attack
     elif hero in ['Mira','Elyra','Traveler']:arm.pose.bones['UpperArmR'].rotation_euler.x=-.42*attack;arm.pose.bones['ForearmR'].rotation_euler.x=-.6*attack
     elif hero=='Kaelon':arm.pose.bones['UpperArmL'].rotation_euler.x=-.70*attack;arm.pose.bones['Chest'].rotation_euler.z=.12*attack
     elif hero=='Veyr':arm.pose.bones['UpperArmR'].rotation_euler.z=-.65*attack;arm.pose.bones['UpperArmL'].rotation_euler.z=.45*attack
     elif hero=='Rook':arm.pose.bones['UpperArmR'].rotation_euler.x=-1.3*attack;arm.pose.bones['ForearmR'].rotation_euler.x=.55*attack
    arm.pose.bones['UpperArmL'].rotation_euler.x=(-.8 if name in ['Ultimate','Skill1','Skill2'] else .2)*attack
    if name=='Ultimate':arm.pose.bones['UpperArmR'].rotation_euler.z=-.7*attack;arm.pose.bones['UpperArmL'].rotation_euler.z=.7*attack
   for b in arm.pose.bones:b.keyframe_insert('rotation_euler',frame=f,group=b.name);b.keyframe_insert('location',frame=f,group=b.name)
  track=arm.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,action);track.mute=True
 arm.animation_data.action=bpy.data.actions.get('Idle');bpy.context.scene.frame_set(1)

def build(hero,female,color,label):
 global parts;parts=[];random.seed(17)
 bpy.ops.wm.read_factory_settings(use_empty=True)
 source_name='GEO-body_'+('female' if female else 'male')+'_realistic'
 with bpy.data.libraries.load(SOURCE,link=False) as (src,dst):dst.objects=[source_name]
 body=dst.objects[0];bpy.context.collection.objects.link(body);body.location=(0,0,0);body.name='DF_Anatomy';body.data=body.data.copy()
 for modifier in list(body.modifiers):body.modifiers.remove(modifier)
 # Normalize all heroes to one canonical deform skeleton; shape variation lives in anatomy.
 zmax=max(v.co.z for v in body.data.vertices);factor=1.795/zmax
 for v in body.data.vertices:v.co*=factor
 body.data.materials.clear()
 skin=mat('Skin',(.52,.30,.20) if hero=='Rook' else (.68,.43,.31),rough=.46)
 cloth=mat('Obsidian fabric',(.018,.027,.048),rough=.75);accent=mat('Signature enamel',color,.45,.28);gold=mat('Champagne metal',(.64,.40,.16),.8,.25);steel=mat('Brushed silver',(.43,.53,.65),.82,.23);energy=mat('Energy',tuple(min(1,c*2+.15) for c in color),.15,.25,2);hair=mat('Hair',(.025,.018,.039) if hero!='Mira' else (.62,.49,.28),rough=.45);white=mat('Porcelain',(.69,.75,.69),.12,.38)
 for m in [skin,cloth]:body.data.materials.append(m)
 for p in body.data.polygons:
  z=sum(body.data.vertices[i].co.z for i in p.vertices)/len(p.vertices);x=sum(abs(body.data.vertices[i].co.x) for i in p.vertices)/len(p.vertices)
  p.material_index=0 if z>1.54 else 1;p.use_smooth=True
 
 import bmesh
 bm=bmesh.new();bm.from_mesh(body.data)
 bmesh.ops.delete(bm,geom=[f for f in bm.faces if all(v.co.z<.16 for v in f.verts)],context='FACES');bm.to_mesh(body.data);bm.free()
 parts.append(body)
 # Base-mesh topology contains detailed eyelids, mouth, nose and fingers.
 # Insert compact eyeballs in the sockets, with layered iris and pupils.
 for s in [-1,1]:
  ellipsoid('EyeWhite', (s*.035,-.130,1.677),(.012,.012,.010),white,'Head')
  ellipsoid('Iris',(s*.035,-.142,1.677),(.005,.002,.005),accent,'Head');ellipsoid('Pupil',(s*.035,-.144,1.677),(.002,.001,.004),cloth,'Head')
  tube('Eyebrow',[(s*.016,-.141,1.700),(s*.035,-.139,1.704),(s*.056,-.125,1.700)],.004,hair,'Head')
 # Sculpted swept hair locks, tapering individually; silhouettes vary per hero.
 for i in range(48):
  a=2*pi*i/48;x=.105*cos(a);y=.085*sin(a)
  length=.30 if female and hero!='Nyra' else .12
  pts=[(x*.2,y*.2,1.78),(x*.75,y*.65,1.795),(x,y,1.735),(x*1.02,y*.95+(length*.26),1.67-length*(.45+.4*sin(a)**2))]
  tube('HairLock',pts,[.014,.022,.018,.002],hair,'Head',7)
 if hero=='Nyra':cape('Scout cloak',1.36,.65,.30,accent)
 if hero in ['Veyr','Traveler']:cape('Split mantle',1.40,.90,.29,cloth)
 if hero in ['Elyra','Mira']:
  cape('Ceremonial robe',1.13,.88,.34,accent,'Hips');cape('Silk upper mantle',1.42,.4,.32,white if hero=='Mira' else cloth)
 # Tailored cuirass and layered panels, each with raised perimeter metalwork.
 plate('DF_Torso',[(-.18,1.37),(-.13,1.44),(.13,1.44),(.18,1.37),(.145,1.15),(0,1.10),(-.145,1.15)],-.187,.047,white if hero=='Mira' else accent)
 for s in [-1,1]:
  tube('Breastplate rim',[(s*.13,-.200,1.43),(s*.18,-.205,1.37),(s*.145,-.205,1.15),(0,-.205,1.10)],.007,gold)
  plate('Layered pauldron',[(s*.16,1.40),(s*.22,1.48),(s*.32,1.42),(s*.36,1.34),(s*.25,1.28)],-.03,.13,steel if hero in ['Kaelon','Rook'] else accent,'UpperArm'+('L' if s<0 else 'R'))
  for j in range(3):
   x=s*(.30+j*.009);z=1.19-j*.045;ellipsoid('Articulated bracer',(x,-.008,z),(.069,.075,.032),gold if j==0 else accent,'Forearm'+('L' if s<0 else 'R'))
  ellipsoid('Sculpted greave',(s*.155,.008,.32),(.084,.076,.19),accent,'Shin'+('L' if s<0 else 'R'))
  plate('Knee guard',[(s*.14-.06,.53),(s*.14,.56),(s*.14+.06,.53),(s*.14+.04,.45),(s*.14-.04,.45)],-.086,.022,steel,'Shin'+('L' if s<0 else 'R'))
  ellipsoid('Boot toe',(s*.18,-.075,.075),(.087,.16,.085),cloth,'Foot'+('L' if s<0 else 'R'))
  tube('Greave trim',[(s*.17,-.080,.15),(s*.155,-.080,.30),(s*.14,-.080,.44)],.008,gold,'Shin'+('L' if s<0 else 'R'))
 # Tailored collar, cross-body strap, fasteners and segmented abdomen.
 tube('Tailored collar',[(.095*cos(t*2*pi/40),.082*sin(t*2*pi/40),1.485) for t in range(41)],.036,cloth)
 for j in range(5):
  z=1.37-j*.036
  tube('Cuirass engraving',[(.02,-.230,z),(.06,-.222,z-.028),(.105,-.214,z-.029)],.0035,gold)
 for j in range(3):
  plate('Abdomen segment',[(-.12,1.19-j*.046),(.12,1.19-j*.046),(.105,1.15-j*.046),(0,1.13-j*.046),(-.105,1.15-j*.046)],-.172,.018,cloth)
 tube('Diagonal leather strap',[(-.15,-.22,1.41),(-.07,-.235,1.30),(.015,-.22,1.16),(.10,-.17,1.045)],.018,cloth)
 for side in [-1,1]:
  for j in range(3):ellipsoid('Armor rivet',(side*(.12-j*.005),-.219,1.38-j*.08),(.006,.004,.006),gold)
 # Hero-specific silhouettes and recognizable faces rather than palette swaps.
 if hero=='Veyr':
  plate('Assassin respirator',[(-.068,1.665),(.068,1.665),(.060,1.595),(0,1.570),(-.060,1.595)],-.164,.025,cloth,'Head')
  for j in range(3):tube('Mask filter',[(-.036,-.195,1.634-j*.013),(.036,-.195,1.634-j*.013)],.003,steel,'Head')
  cape('Asymmetric long coat',1.24,1.0,.33,cloth)
 elif hero=='Kaelon':
  # A crown and cheek guards leave the central face visible.
  for side in [-1,1]:
   plate('Helmet cheek',[(side*.072,1.75),(side*.112,1.73),(side*.11,1.60),(side*.074,1.57)],-.078,.11,steel,'Head')
   tube('Crown fin',[(side*.090,0,1.73),(side*.11,.01,1.86),(side*.15,.05,1.81)],[.020,.028,.002],gold,'Head')
  tube('Crown brow',[(-.086,-.12,1.72),(0,-.14,1.735),(.086,-.12,1.72)],.018,gold,'Head')
  for side in [-1,1]:ellipsoid('Heavy shoulder shell',(side*.25,.015,1.385),(.16,.15,.10),steel,'UpperArm'+('L' if side<0 else 'R'))
 elif hero=='Nyra':
  tube('Long ponytail',[(0,.085,1.74),(.02,.13,1.61),(.07,.15,1.46),(.11,.12,1.31)],[.05,.06,.035,.008],hair,'Head',14)
  plate('Scout monocle',[(-.065,1.706),(-.015,1.706),(-.015,1.657),(-.065,1.657)],-.151,.012,cloth,'Head')
  tube('Optic lens',[(-.04,-.166,1.682),(-.04,-.173,1.682)],.016,energy,'Head',16)
  for j in range(5):tube('Ammo cartridges',[(-.13+j*.055,-.19,1.05),(-.13+j*.055,-.19,1.11)],.009,gold,'Hips')
 elif hero=='Elyra':
  for side in [-1,1]:
   tube('Astral diadem',[(side*.045,-.12,1.735),(side*.087,-.07,1.82),(side*.115,-.025,1.85)],[.010,.013,.001],gold,'Head')
   for j in range(10):
    x=side*(.055+j*.008)
    tube('Flowing hair',[(x,.075,1.74),(x*1.35,.12,1.54),(x*1.5,.13,1.30),(x*1.3,.19,1.14)],[.019,.024,.020,.002],hair,'Head',7)
   # Front robe has a side slit; depth ripples make real fabric folds.
   o=cape('Front silk panel',1.07,.87,.18,accent,'Hips',side*.13)
   for v in o.data.vertices:v.co.y-=.29+.20*(1.07-v.co.z)/.87
   tube('Robe gold hem',[(side*.03,-.20,1.07),(side*.16,-.23,.65),(side*.30,-.26,.23)],.006,gold,'Hips')
 elif hero=='Mira':
  for side in [-1,1]:
   o=cape('White front vestment',1.10,.76,.16,white,'Hips',side*.13)
   for v in o.data.vertices:v.co.y-=.29+.20*(1.10-v.co.z)/.76
   tube('Healer leaf ornament',[(side*.08,-.20,1.34),(side*.10,-.20,1.42),(side*.04,-.20,1.37),(side*.08,-.20,1.34)],.006,gold)
   tube('Braided locks',[(side*.10,.04,1.74),(side*.11,-.02,1.57),(side*.13,-.025,1.42),(side*.13,-.04,1.30)],[.025,.026,.016,.004],hair,'Head',10)
  tube('Healer circlet',[(-.085,-.065,1.735),(0,-.125,1.745),(.085,-.065,1.735)],.010,gold,'Head')
 elif hero=='Rook':
  for v in body.data.vertices:
   if v.co.z<1.48 and v.co.z>.92:v.co.x*=1.18
  for side in [-1,1]:
   ellipsoid('Heavy exosuit shoulder',(side*.28,.02,1.39),(.17,.155,.12),accent,'UpperArm'+('L' if side<0 else 'R'))
   tube('Reactor exhaust',[(side*.15,.15,1.18),(side*.17,.16,1.46)],.040,steel)
   for j in range(6):tube('Beard strand',[(side*(.02+j*.008),-.14,1.607),(side*(.01+j*.006),-.15,1.57)],[.007,.001],hair,'Head',6)
  tube('Facial scar',[(.050,-.130,1.697),(.054,-.150,1.651)],.0025,gold,'Head',6)
 elif hero=='Traveler':
  plate('Chronomancer faceguard',[(-.085,1.71),(.085,1.71),(.075,1.60),(0,1.57),(-.075,1.60)],-.161,.030,cloth,'Head')
  tube('Chronomancer visor',[(-.068,-.200,1.678),(0,-.207,1.69),(.068,-.200,1.678)],.009,energy,'Head')
  for side in [-1,1]:tube('Raised cowl',[(side*.10,.035,1.47),(side*.14,.04,1.56),(side*.12,.09,1.72)],[.04,.045,.025],accent,'Head')
 # Waist belt and layered tassets. Metallic trims and seam detail remain modeled at game distance.
 ellipsoid('Waist sash',(0,0,1.025),(.16,.113,.04),gold,'Hips')
 for s in [-1,1]:plate('Tasset',[(s*.035,1.0),(s*.14,1.02),(s*.20,.84),(s*.12,.78),(s*.045,.84)],-.17,.035,accent,'Hips')
 ellipsoid('Chest relic',(0,-.235,1.30),(.028,.014,.044),energy)
 if hero=='Veyr':
  for s,k in [(-1,'L'),(1,'R')]:
   tube('DF_Weapon'+k,[(s*.39,-.08,1.03),(s*.39,-.08,.90)],.017,gold,'Hand'+k)
   plate('Rift blade',[(s*.39-.035,.91),(s*.39+.035,.91),(s*.39+.085,.36),(s*.39,.22),(s*.39-.03,.42)],-.08,.025,steel,'Hand'+k)
   tube('Rift blade inlay',[(s*.39,-.094,.89),(s*.41,-.094,.48),(s*.39,-.094,.27)],.006,energy,'Hand'+k)
 elif hero=='Kaelon':
  plate('DF_WeaponL',[(-.45,1.30),(-.66,1.25),(-.69,.86),(-.45,.63),(-.22,.86),(-.24,1.25)],-.18,.06,steel,'HandL')
  plate('Shield enamel',[(-.45,1.25),(-.61,1.20),(-.62,.89),(-.45,.71),(-.29,.89),(-.29,1.20)],-.225,.025,accent,'HandL')
  tube('Shield crest',[(-.45,-.27,.77),(-.45,-.27,1.21)],.014,energy,'HandL')
  tube('DF_WeaponR',[(.4,-.04,.70),(.4,-.04,1.08)],.026,gold,'HandR');ellipsoid('Hammer crown',(.4,-.04,1.16),(.13,.10,.14),steel,'HandR')
 elif hero=='Nyra':
  tube('DF_WeaponR',[(.40,-.05,1.07),(.40,-.70,1.07)],.043,steel,'HandR');tube('Rifle casing',[(.40,-.13,1.07),(.40,-.38,1.07)],.076,accent,'HandR')
  tube('Scope',[(.40,-.19,1.16),(.40,-.32,1.16)],.033,cloth,'HandR');ellipsoid('Scope lens',(.40,-.335,1.16),(.022,.006,.022),energy,'HandR')
 elif hero in ['Elyra','Mira']:
  tube('DF_WeaponR',[(.42,-.07,.11),(.42,-.07,1.61)],.017,gold,'HandR')
  for s in [-1,1]:tube('Relic prong',[(.42,-.07,1.37),(.42+s*.11,-.07,1.54),(.42+s*.07,-.07,1.72)],[.017,.020,.002],gold,'HandR')
  ellipsoid('Restoration crystal' if hero=='Mira' else 'Astral crystal',(.42,-.07,1.59),(.050,.040,.085),energy,'HandR',8,4)
  if hero=='Elyra':
   for s in [-1,1]:ellipsoid('Orbit relic',(s*.31,.12,1.49),(.039,.035,.070),energy)
 elif hero=='Rook':
  for s,k in [(-1,'L'),(1,'R')]:
   ellipsoid('DF_Gauntlet'+k,(s*.39,-.025,1.02),(.125,.135,.145),accent,'Hand'+k)
   for j in range(4):ellipsoid('Knuckle', (s*.39+(j-1.5)*.04,-.155,1.02),(.027,.033,.045),steel,'Hand'+k)
  tube('Spinal reactor',[(0,.135,1.14),(0,.135,1.40)],.05,energy)
 elif hero=='Traveler':
  cape('Traveler coat',1.22,.80,.28,accent);tube('DF_WeaponR',[(.41,-.06,.78),(.41,-.06,1.04)],.023,gold,'HandR')
  ellipsoid('Chronosphere',(.41,-.06,1.22),(.11,.08,.11),energy,'HandR')
  for j in range(3):
   a=j*pi/3;pts=[(.41+.145*cos(t*2*pi/48),-.06+.045*sin(t*2*pi/48+a),1.22+.145*sin(t*2*pi/48)) for t in range(49)];tube('Chronosphere orbit',pts,.007,gold,'HandR')
 arm=rig()
 for o in parts:
  if o.get('bone','').startswith('Hand'):
   for v in o.data.vertices:v.co.z-=.10
  bind(o,arm,o==body)
 animations(arm,hero)
 # Explicit named VFX socket travels with the weapon hand.
 socket=bpy.data.objects.new('DF_Muzzle',None);bpy.context.collection.objects.link(socket);socket.parent=arm;socket.parent_type='BONE';socket.parent_bone='HandR';bpy.context.view_layer.update()
 muzzle_point={'Nyra':(.40,-.71,.97),'Mira':(.42,-.07,1.49),'Elyra':(.42,-.07,1.49),'Traveler':(.41,-.06,1.12),'Rook':(.39,-.16,.92),'Kaelon':(.40,-.04,1.06),'Veyr':(.39,-.08,.70)}[hero]
 socket.matrix_world=Matrix.Translation(muzzle_point)
 # Merge costume sections into one weighted renderer with shared material slots.
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=body;bpy.ops.object.join();parts=[body]
 # FBX exports gameplay objects only; studio scene is saved independently for reproducible previews.
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts+[arm,socket]:o.select_set(True)
 bpy.context.view_layer.objects.active=arm
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,hero+'.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.5,path_mode='AUTO')
 # Studio camera and a restrained three-point rig.
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
 scene.render.resolution_x=640;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.025,.035,.06,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
 ground=mat('Studio floor',(.018,.025,.045),.25,.48);bpy.ops.mesh.primitive_plane_add(size=200);bpy.context.object.data.materials.append(ground)
 def aim(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
 for n,loc,power,size,col in [('Key',(-3,-4,5),650,4,(1,.85,.68)),('Fill',(3,-2,3),400,3,(.55,.72,1)),('Rim',(1,3,4),850,2,(.5,.65,1))]:
  d=bpy.data.lights.new(n,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=col;o=bpy.data.objects.new(n,d);bpy.context.collection.objects.link(o);o.location=loc;aim(o,(0,0,1))
 d=bpy.data.cameras.new('Portrait');camera=bpy.data.objects.new('Portrait',d);bpy.context.collection.objects.link(camera);camera.location=(2.7,-6,2.4);aim(camera,(0,-.03,.95));d.type='ORTHO';d.ortho_scale=2.30;scene.camera=camera
 scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(PREVIEW,hero+'.png')
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource/Heroes',hero+'.blend'),compress=True)
 bpy.ops.render.render(write_still=True)
 stats={'hero':hero,'label':label,'vertices':sum(len(o.data.vertices) for o in parts),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),'bones':len(arm.data.bones),'animations':9}
 print('HERO_COMPLETE',json.dumps(stats),flush=True)
 return stats
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
stats=[]
for row in HEROES:
 if not selected or row[0] in selected:stats.append(build(*row))
with open(os.path.join(ROOT,'manifest.json'),'w') as f:json.dump(stats,f,indent=2)
