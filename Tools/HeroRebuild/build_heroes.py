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
HEROES=[('Veyr',False,(.23,.065,.48),'ZHAEV NULL'),('Kaelon',False,(.035,.23,.30),'AUREN KEST'),('Nyra',True,(.025,.22,.30),'SIVRA QUELL'),('Elyra',True,(.24,.075,.42),'IONE VASKA'),('Mira',True,(.035,.30,.16),'TAHLA VENN'),('Rook',False,(.47,.095,.025),'BRAKK ORUN'),('Traveler',False,(.035,.043,.055),'THE TRAVELER')]
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

def box(name,center,size,material,bone='Chest',edge=.01):
 vs=[(center[0]+x*size[0]/2,center[1]+y*size[1]/2,center[2]+z*size[2]/2) for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 return mesh(name,vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],material,bone,edge)

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
  b('WeaponGrip'+k,(s*.41,-.01,.95),(s*.43,-.03,.86),'Hand'+k)
  b('Thigh'+k,(s*.13,0,.9),(s*.14,.01,.49),'Hips');b('Shin'+k,(s*.14,.01,.49),(s*.18,.02,.12),'Thigh'+k);b('Foot'+k,(s*.18,.02,.12),(s*.18,-.15,.06),'Shin'+k)
 bpy.ops.object.mode_set(mode='OBJECT');ob.select_set(False);return ob

def bind(o,arm,body=False):
 names=[b.name for b in arm.data.bones]
 for n in names:o.vertex_groups.new(name=n)
 if body:
  # Smooth proximity weights across neighboring bones; plate weights remain rigid.
  segments=[(b.name,b.head_local,b.tail_local) for b in arm.data.bones if b.name!='Root' and not b.name.startswith('WeaponGrip')]
  for v in o.data.vertices:
   distances=[]
   for name,head,tail in segments:
    direction=tail-head;t=max(0,min(1,(v.co-head).dot(direction)/direction.length_squared))
    distance=(v.co-(head+direction*t)).length
    distances.append((distance,name))
   distances.sort();closest=distances[:3]
   weights=[1/(distance+.018)**5 for distance,name in closest];total=sum(weights)
   for (distance,name),weight in zip(closest,weights):o.vertex_groups[name].add([v.index],weight/total,'REPLACE')
 else:
  bone=o.get('bone','Chest')
  # Weapons have independent grip bones. Armor gauntlets still follow the anatomical hand.
  if bone.startswith('Hand') and arm.get('hero')!='Rook':bone='WeaponGrip'+bone[-1]
  o.vertex_groups[bone].add(list(range(len(o.data.vertices))),1,'REPLACE')
 mod=o.modifiers.new('DF_Skin','ARMATURE');mod.object=arm;o.parent=arm

PROFILE={'Veyr':(.94,.94,1.02,.94),'Kaelon':(1.43,1.20,1.08,1.30),'Nyra':(.88,.94,1.02,.93),'Elyra':(.89,.96,1.09,.92),'Mira':(.89,1.09,1.02,.96),'Rook':(1.60,1.32,1.04,1.40),'Traveler':(1.03,.96,1.07,.97)}
def proportion(hero,point):
 x,y,z=point;chest,hip,height,depth=PROFILE[hero]
 if z>1.52:width=1.12 if hero=='Rook' else 1.0;d=1.07 if hero=='Rook' else 1.0
 elif z>1.10:width=chest;d=depth
 else:width=hip+(chest-hip)*max(0,min(1,(z-.92)/.18));d=1+(depth-1)*max(0,min(1,z/.9))
 if hero=='Mira':
  # A tailored waist transitions smoothly into fuller hips; the skeleton shares the deformation.
  width*=1-.16*math.exp(-((z-1.09)/.085)**2)
  if z>1.55:width*=.96;d*=.97
 return Vector((x*width,y*d,z*height))

def animations(arm,hero):
 # Analytic two-bone posing is baked into ordinary FBX clips. No runtime procedural pose owner.
 guard={
 'Veyr':{'L':(-.10,-.27,1.20),'R':(.14,-.27,1.30)},
 'Kaelon':{'L':(-.24,-.24,1.20),'R':(.31,-.16,1.20)},
 'Nyra':{'L':(.08,-.38,1.29),'R':(.19,-.18,1.34)},
 'Elyra':{'L':(-.25,-.19,1.22),'R':(.33,-.12,1.17)},
 'Mira':{'L':(-.22,-.17,1.17),'R':(.32,-.13,1.16)},
 'Rook':{'L':(-.20,-.25,1.32),'R':(.23,-.23,1.31)},
 'Traveler':{'L':(-.20,-.22,1.22),'R':(.27,-.20,1.22)}}[hero]
 def chain(upper,lower,end,target,pole,hand_rotation=None):
  bpy.context.view_layer.update();a=arm.pose.bones[upper];b=arm.pose.bones[lower];last=arm.pose.bones[end]
  origin=a.head.copy();delta=Vector(target)-origin;distance=delta.length
  if distance<.001:return
  direction=delta.normalized();l1=a.bone.length;l2=b.bone.length;distance=min(distance,l1+l2-.001)
  side=Vector(pole)-direction*Vector(pole).dot(direction)
  if side.length<.001:side=direction.cross(Vector((1,0,0)))
  side.normalize();along=(l1*l1-l2*l2+distance*distance)/(2*distance)
  elbow=origin+direction*along+side*math.sqrt(max(.00001,l1*l1-along*along));tip=origin+direction*distance
  for bone,start,finish in [(a,origin,elbow),(b,elbow,tip)]:
   rotation=bone.bone.matrix_local.to_quaternion();axis=rotation@Vector((0,1,0));rotation=axis.rotation_difference((finish-start).normalized())@rotation
   bone.matrix=Matrix.LocRotScale(start,rotation,Vector((1,1,1)));bpy.context.view_layer.update()
  rotation=last.bone.matrix_local.to_quaternion()
  if end.startswith('Hand'):
   # Keep the hand aligned with the forearm; weapon orientation belongs to its child grip.
   rotation=b.matrix.to_quaternion()@b.bone.matrix_local.to_quaternion().inverted()@rotation
  elif hand_rotation is not None:rotation=hand_rotation@rotation
  last.matrix=Matrix.LocRotScale(tip,rotation,Vector((1,1,1)));bpy.context.view_layer.update()
  if end.startswith('Hand'):
   grip=arm.pose.bones['WeaponGrip'+end[-1]]
   weapon_rotation=grip.bone.matrix_local.to_quaternion()
   if hand_rotation is not None:weapon_rotation=hand_rotation@weapon_rotation
   grip.matrix=Matrix.LocRotScale(tip,weapon_rotation,Vector((1,1,1)));bpy.context.view_layer.update()
 from mathutils import Quaternion
 frames=[('Idle',61),('Run',25),('Attack',25),('Skill0',31),('Skill1',34),('Skill2',37),('Ultimate',49),('Hit',16),('Death',40)]
 for name,length in frames:
  action=bpy.data.actions.new(name);arm.animation_data_create();arm.animation_data.action=action
  for f in range(1,length+1):
   t=(f-1)/(length-1);wave=sin(t*2*pi);pulse=sin(pi*t)**2
   # Anticipation -> fast contact -> eased recovery; each class uses its own hand path.
   impact=max(0,1-abs(t-.42)/.24);windup=max(0,1-abs(t-.16)/.16)
   for bone in arm.pose.bones:bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
   hips=arm.pose.bones['Hips'];chest=arm.pose.bones['Chest'];spine=arm.pose.bones['Spine'];head=arm.pose.bones['Head']
   hips.location.z=-.028;chest.rotation_euler.x=.012*wave;head.rotation_euler.z=.010*wave
   if name=='Run':
    hips.location.z=-.10+.014*cos(t*4*pi);hips.rotation_euler.z=.035*wave;chest.rotation_euler.z=-.028*wave;chest.rotation_euler.x=.075
   elif name=='Hit':chest.rotation_euler.x=-.22*pulse;spine.rotation_euler.z=.12*pulse
   elif name=='Death':
    arm.pose.bones['Root'].rotation_euler.x=1.45*min(1,t*1.25);hips.location.z=-.18*t
   elif name not in ['Idle','Run']:
    strength=1.25 if name=='Ultimate' else 1.0
    hips.rotation_euler.z=(-.09*windup+.13*impact)*strength
    chest.rotation_euler.z=(.18*windup-.33*impact)*strength
    spine.rotation_euler.x=.08*impact;head.rotation_euler.z=-chest.rotation_euler.z*.35
   bpy.context.view_layer.update()
   if name!='Death':
    for side,offset,sign in [('L',0,-1),('R',.5,1)]:
     phase=(t+offset)%1;lift=0;forward=-.025;roll=0
     if name=='Run':
      if phase<.56:forward=-.36+.72*(phase/.56);roll=.10*max(0,(phase-.42)/.14)
      else:
       swing=(phase-.56)/.44;ease=swing*swing*(3-2*swing);forward=.36-.72*ease;lift=.16*sin(pi*swing);roll=-.20*sin(pi*swing)
     foot=proportion(hero,(sign*.18,forward,.12+lift))
     chain('Thigh'+side,'Shin'+side,'Foot'+side,foot,(0,-1,0),Quaternion((1,0,0),roll))
    # Guard targets follow the torso; only a small vertical run bounce is permitted.
    torso_matrix=chest.matrix@chest.bone.matrix_local.inverted()
    for side,sign in [('L',-1),('R',1)]:
     target=Vector(guard[side]);twist=0
     if name=='Run':target.z+=.006*cos(t*4*pi)
     elif name not in ['Idle','Hit']:
      if hero=='Veyr':target.x+=sign*(.14*windup-.18*impact);target.y-=.16*impact;target.z+=(.12 if side=='R' else -.07)*impact;twist=sign*.75*impact
      elif hero=='Rook':
       if side=='R':target.y-=.23*impact;target.x-=.06*impact;target.z-=.05*impact
       else:target.y-=.04*impact
      elif hero=='Kaelon':target.y-=.13*impact;target.z+=(.24*windup-.14*impact) if side=='R' else .03*impact
      elif hero=='Nyra':target.y+=.025*impact;chest.rotation_euler.z=.018*impact
      else:
       target.y-=.13*pulse;target.z+=(.27 if name=='Ultimate' else .13)*pulse;target.x+=sign*.055*pulse
     if hero=='Veyr':twist+=sign*.52
     handrot=Quaternion((0,1,0),twist)
     point=torso_matrix@proportion(hero,target)
     chain('UpperArm'+side,'Forearm'+side,'Hand'+side,point,(sign,-.10,-.40),handrot)
   for bone in arm.pose.bones:bone.keyframe_insert('rotation_euler',frame=f,group=bone.name);bone.keyframe_insert('location',frame=f,group=bone.name)
  track=arm.animation_data.nla_tracks.new();track.name=name;track.strips.new(name,1,action);track.mute=True
 arm.animation_data.action=bpy.data.actions.get('Idle');bpy.context.scene.frame_set(1);bpy.context.view_layer.update()

def build(hero,female,color,label):
 global parts;parts=[];random.seed(17)
 bpy.ops.wm.read_factory_settings(use_empty=True)
 source_name='GEO-body_'+('female' if female else 'male')+'_realistic'
 with bpy.data.libraries.load(SOURCE,link=False) as (src,dst):dst.objects=[source_name]
 body=dst.objects[0];bpy.context.collection.objects.link(body);body.location=(0,0,0);body.name='DF_Anatomy';body.data=body.data.copy()
 for modifier in list(body.modifiers):body.modifiers.remove(modifier)
 # Normalize all heroes to one canonical deform skeleton; shape variation lives in anatomy.
 zmax=max(v.co.z for v in body.data.vertices);factor=1.795/zmax
 for v in body.data.vertices:
  v.co*=factor
  if abs(v.co.x)>.34 and v.co.z<.89 and v.co.z>.64:
   t=max(0,min(1,(.89-v.co.z)/.18))
   v.co.z=.89-.045*sin(pi*t)
   v.co.y-=.035*sin(pi*t)-.035*(1-cos(pi*t))
 body.data.materials.clear()
 skin=mat('Skin',(.52,.30,.20) if hero=='Rook' else (.68,.43,.31),rough=.46)
 cloth=mat('Obsidian fabric',(.018,.027,.048),rough=.75);accent=mat('Signature enamel',color,.45,.28);gold=mat('Champagne metal',(.64,.40,.16),.8,.25);steel=mat('Brushed silver',(.43,.53,.65),.82,.23);energy=mat('Energy',tuple(min(1,c*2+.15) for c in color),.15,.25,2);hair=mat('Hair',(.025,.018,.039) if hero!='Mira' else (.62,.49,.28),rough=.45);white=mat('Porcelain',(.69,.75,.69),.12,.38)
 if hero=='Traveler':
  gold=mat('Traveler engraved silver',(.38,.42,.46),.82,.34)
  energy=mat('Traveler violet chronolight',(.30,.09,.48),.15,.30,2)
  skin=mat('Traveler shadowed skin',(.28,.22,.19),rough=.62)
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
  box('Boot sole',(s*.18,-.065,.035),(.16,.29,.034),cloth,'Foot'+('L' if s<0 else 'R'),.009)
  box('Armored toe cap',(s*.18,-.145,.076),(.16,.13,.075),steel if hero in ['Kaelon','Rook'] else accent,'Foot'+('L' if s<0 else 'R'),.017)
  box('Boot heel',(s*.18,.041,.060),(.14,.084,.075),cloth,'Foot'+('L' if s<0 else 'R'),.008)
  box('Boot upper',(s*.18,-.025,.124),(.14,.17,.16),cloth,'Foot'+('L' if s<0 else 'R'),.017)
  box('Boot buckle',(s*.18,-.115,.14),(.092,.019,.025),gold,'Foot'+('L' if s<0 else 'R'),.004)
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
  for side in [-1,1]:tube('Raised cowl',[(side*.10,.035,1.47),(side*.14,.04,1.56),(side*.12,.09,1.72)],[.04,.045,.025],accent,'Head')
 # Waist belt and layered tassets. Metallic trims and seam detail remain modeled at game distance.
 ellipsoid('Waist sash',(0,0,1.025),(.16,.113,.04),gold,'Hips')
 for s in [-1,1]:plate('Tasset',[(s*.035,1.0),(s*.14,1.02),(s*.20,.84),(s*.12,.78),(s*.045,.84)],-.17,.035,accent,'Hips')
 ellipsoid('Chest relic',(0,-.235,1.30),(.028,.014,.044),energy)
 if hero=='Veyr':
  for side,k in [(-1,'L'),(1,'R')]:
   tube('DF_Weapon'+k,[(side*.42,-.03,1.005),(side*.42,-.03,.865)],.015,cloth,'Hand'+k)
   for j in range(6):tube('Grip wrap',[(side*.42-.016,-.045,.88+j*.020),(side*.42+.016,-.045,.887+j*.020)],.003,gold,'Hand'+k)
   tube('Crescent crossguard',[(side*.34,-.03,.998),(side*.42,-.03,1.02),(side*.50,-.03,.998)],.012,gold,'Hand'+k)
   # Two swept crescent blades, with a narrow inner cutting edge and pointed tips.
   verts=[];faces=[]
   for j in range(33):
    a=-1.42+j*2.9/32;t=j/32;width=.075*sin(pi*t)
    for y in [-.045,-.018]:
     for inner in [False,True]:
      verts.append((side*(.42+(.25-width if inner else .25)*sin(a)),y,.59+(.38-width if inner else .38)*cos(a)))
   for j in range(32):
    n=j*4;faces.extend([(n,n+4,n+5,n+1),(n+2,n+3,n+7,n+6),(n,n+2,n+6,n+4),(n+1,n+5,n+7,n+3)])
   faces.extend([(0,1,3,2),(128,130,131,129)])
   mesh('Moon sickle blade',verts,faces,steel,'Hand'+k)
   tube('Violet crescent cutting edge',[(side*(.42+.25*sin(-1.42+j*2.9/40)),-.047,.59+.38*cos(-1.42+j*2.9/40)) for j in range(41)],.005,energy,'Hand'+k)
 elif hero=='Kaelon':
  plate('DF_WeaponL',[(-.45,1.30),(-.66,1.25),(-.69,.86),(-.45,.63),(-.22,.86),(-.24,1.25)],-.18,.06,steel,'HandL')
  plate('Shield enamel',[(-.45,1.25),(-.61,1.20),(-.62,.89),(-.45,.71),(-.29,.89),(-.29,1.20)],-.225,.025,accent,'HandL')
  tube('Shield crest',[(-.45,-.27,.77),(-.45,-.27,1.21)],.014,energy,'HandL')
  tube('DF_WeaponR',[(.4,-.04,.70),(.4,-.04,1.08)],.026,gold,'HandR');ellipsoid('Hammer crown',(.4,-.04,1.16),(.13,.10,.14),steel,'HandR')
 elif hero=='Nyra':
  tube('DF_WeaponR',[(.40,-.12,1.07),(.40,-.92,1.07)],.020,steel,'HandR');tube('Sniper receiver',[(.40,-.10,1.07),(.40,-.34,1.07)],.050,accent,'HandR')
  box('Sniper shoulder stock',(.40,.10,1.06),(.085,.27,.10),cloth,'HandR',.012)
  box('Sniper butt pad',(.40,.245,1.06),(.10,.035,.13),steel,'HandR',.008)
  tube('Sniper muzzle brake',[(.40,-.88,1.07),(.40,-.98,1.07)],.033,cloth,'HandR')
  tube('Sniper trigger grip',[(.40,-.16,1.04),(.40,-.12,.955)],.026,cloth,'HandR')
  for side in [-1,1]:tube('Folded precision bipod',[(.40+side*.035,-.59,1.06),(.40+side*.047,-.73,1.025)],.008,steel,'HandR')
  tube('Scope',[(.40,-.14,1.17),(.40,-.40,1.17)],.042,cloth,'HandR');ellipsoid('Scope lens',(.40,-.412,1.17),(.033,.006,.033),energy,'HandR')
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
 # Second art pass: deliberate head silhouettes, distinct armor construction and weapon detail.
 if hero=='Veyr':
  plate('Null full-face mask',[(-.090,1.733),(-.035,1.752),(.075,1.730),(.103,1.66),(.058,1.585),(0,1.566),(-.075,1.598),(-.103,1.667)],-.211,.035,cloth,'Head')
  for side in [-1,1]:
   tube('Mask violet eye',[(side*.018,-.255,1.696),(side*.068,-.248,1.709)],.006,energy,'Head')
   tube('Mask cheek seam',[(side*.080,-.244,1.670),(side*.034,-.253,1.600)],.0035,steel,'Head')
   plate('Assassin shoulder blade',[(side*.21,1.43),(side*.37,1.48),(side*.35,1.33),(side*.24,1.28)],-.06,.07,cloth,'UpperArm'+('L' if side<0 else 'R'))
   box('Belt pouch',(side*.18,-.08,1.00),(.085,.12,.14),cloth,'Hips',.013)
  for o in parts:
   if o.name.startswith('DF_Torso'):o.data.materials[0]=cloth
 elif hero=='Kaelon':
  ellipsoid('Closed guardian helmet',(0,.005,1.695),(.135,.149,.173),steel,'Head',32,20)
  plate('Guardian armored faceplate',[(-.112,1.75),(.112,1.75),(.12,1.65),(.065,1.566),(0,1.545),(-.065,1.566),(-.12,1.65)],-.189,.036,accent,'Head')
  tube('Guardian visor',[(-.090,-.231,1.698),(0,-.244,1.690),(.090,-.231,1.698)],.008,energy,'Head')
  tube('Helmet ridge',[(0,-.229,1.758),(0,-.12,1.87),(0,.08,1.875)],[.022,.029,.012],gold,'Head')
  for side in [-1,1]:
   for j in range(3):plate('Guardian shoulder lamella',[(side*.20,1.42-j*.036),(side*.38,1.40-j*.036),(side*.36,1.34-j*.036),(side*.22,1.35-j*.036)],-.11,.045,steel,'UpperArm'+('L' if side<0 else 'R'))
   tube('Shield border',[(side*.19-.45,-.265,1.20),(side*.19-.45,-.265,.89),(-.45,-.265,.70)],.013,gold,'HandL')
  box('Hammer striking block',(.4,-.04,1.16),(.28,.20,.17),steel,'HandR',.022)
  box('Hammer energy inset',(.4,-.148,1.16),(.19,.013,.065),energy,'HandR',.009)
 elif hero=='Nyra':
  for side in [-1,1]:
   plate('Recon goggle frame',[(side*.008,1.710),(side*.083,1.710),(side*.080,1.655),(side*.009,1.655)],-.166,.021,cloth,'Head')
   plate('Recon lens',[(side*.015,1.698),(side*.074,1.700),(side*.070,1.669),(side*.016,1.669)],-.204,.005,accent,'Head')
  tube('Recon antenna',[(.12,.02,1.68),(.14,.03,1.88)],.005,steel,'Head')
  box('Rifle receiver',(.40,-.29,1.07),(.105,.27,.11),cloth,'HandR',.014)
  box('Rifle magazine',(.40,-.27,.965),(.06,.075,.095),accent,'HandR',.008)
  for j in range(5):box('Rifle top rail',(.40,-.18-j*.039,1.16),(.075,.018,.026),steel,'HandR',.004)
  box('Rifle side emission',(.47,-.30,1.09),(.008,.19,.018),energy,'HandR',.003)
 elif hero=='Elyra':
  for side in [-1,1]:
   tube('Archmage crown spire',[(side*.065,-.10,1.735),(side*.12,-.03,1.90),(side*.17,.01,1.96)],[.014,.018,.001],gold,'Head')
   tube('Floating lunar shoulder',[(side*.19,-.04,1.40),(side*.34,.03,1.58),(side*.40,.08,1.48)],[.025,.019,.002],gold)
   for j in range(4):ellipsoid('Suspended astral jewels',(side*(.25+j*.04),.07,1.43-j*.065),(.013,.014,.022),energy)
  for o in parts:
   if o.name.startswith(('HairLock','Flowing hair')):o.data.materials[0]=white
  for j in range(7):
   a=j*2*pi/7;tube('Staff orbit prong',[(.42+.09*cos(a),-.07,1.59+.09*sin(a)),(.42+.13*cos(a),-.07,1.59+.13*sin(a))],[.009,.002],gold,'HandR')
 elif hero=='Mira':
  for side in [-1,1]:
   tube('Laurel leaf crown',[(side*.022,-.13,1.742),(side*.10,-.08,1.770),(side*.12,.015,1.738)],.009,gold,'Head')
   for j in range(4):
    x=side*(.035+j*.020);z=1.745+j*.003
    plate('Laurel crown leaf',[(x,z),(x+side*.02,z+.035),(x+side*.03,z+.028),(x+side*.02,z-.004)],-.13+j*.017,.008,accent,'Head')
   tube('White healer shoulder shawl',[(side*.105,.06,1.43),(side*.24,.01,1.41),(side*.30,-.045,1.30)],[.06,.055,.035],white,'UpperArm'+('L' if side<0 else 'R'))
   for j in range(5):ellipsoid('Shawl green embroidery',(side*(.15+j*.027),-.067,1.435-j*.022),(.006,.005,.012),accent)
  # Rounded face accents and a refined lip line over the detailed female base mesh.
  lip=mat('Healer rose lips',(.48,.16,.15),rough=.52)
  tube('Soft upper lip',[(-.023,-.145,1.625),(-.007,-.151,1.628),(0,-.152,1.625),(.007,-.151,1.628),(.023,-.145,1.625)],.003,lip,'Head')
  tube('Soft lower lip',[(-.020,-.146,1.621),(0,-.151,1.619),(.020,-.146,1.621)],.0035,lip,'Head')
  # Priestess reference: open blonde hair, white-gold armor and layered flowing vestments.
  for side in [-1,1]:
   for j in range(8):
    x=side*(.075+j*.010)
    tube('Priestess flowing blonde lock',[(x,.070,1.74),(x*1.24,.115,1.62),(x*1.45,.08,1.46),(x*1.20,.13,1.28),(x*1.48,.14,1.22)],[.016,.023,.021,.014,.002],hair,'Head',8)
   plate('Priestess gold shoulder',[ (side*.13,1.425),(side*.23,1.49),(side*.34,1.48),(side*.33,1.36),(side*.20,1.32)],-.085,.042,gold,'UpperArm'+('L' if side<0 else 'R'))
   plate('Priestess shoulder ivory inlay',[(side*.19,1.43),(side*.28,1.455),(side*.30,1.38),(side*.22,1.36)],-.134,.009,white,'UpperArm'+('L' if side<0 else 'R'))
   tube('Priestess pointed shoulder crest',[(side*.19,-.08,1.43),(side*.28,-.07,1.55),(side*.35,-.045,1.51)],[.012,.010,.001],gold,'UpperArm'+('L' if side<0 else 'R'))
   tube('Priestess waist gold scroll',[(side*.02,-.221,1.20),(side*.10,-.225,1.16),(side*.135,-.215,1.085),(side*.085,-.205,1.025),(side*.02,-.213,1.08)],.006,gold,'Hips')
   plate('Priestess long ivory tabard',[(side*.015,1.01),(side*.13,1.02),(side*.21,.58),(side*.15,.23),(side*.025,.34)],-.18,.025,white,'Hips')
   tube('Priestess tabard gold hem',[(side*.03,-.211,.95),(side*.12,-.214,.73),(side*.17,-.214,.48),(side*.13,-.214,.28),(side*.03,-.214,.37)],.005,gold,'Hips')
   for j in range(5):
    z=.41+j*.08
    tube('Priestess embroidered scroll',[(side*.06,-.220,z),(side*.11,-.220,z+.025),(side*.085,-.220,z+.050)],.003,gold,'Hips')
   tube('Priestess ornate staff crest',[(.42+side*.025,-.07,1.40),(.42+side*.12,-.07,1.51),(.42+side*.12,-.07,1.70),(.42+side*.045,-.07,1.82)],[.012,.020,.015,.001],gold,'HandR')
  plate('Priestess gold waist crest',[(-.08,1.10),(0,1.16),(.08,1.10),(.055,1.02),(0,.97),(-.055,1.02)],-.245,.016,gold,'Hips')
  ellipsoid('Priestess emerald brooch',(0,-.270,1.06),(.023,.010,.032),energy,'Hips')
  for j in range(4):tube('Staff vine',[(.42+.03*cos(t*pi/8+j),-.07+.03*sin(t*pi/8+j),.25+t*.065) for t in range(18)],.004,accent,'HandR')
 elif hero=='Rook':
  for side in [-1,1]:
   box('Industrial shoulder cage',(side*.29,.03,1.40),(.30,.31,.18),cloth,'UpperArm'+('L' if side<0 else 'R'),.035)
   for j in range(4):box('Shoulder heat fin',(side*.29,-.135,1.34+j*.027),(.19,.018,.012),steel,'UpperArm'+('L' if side<0 else 'R'),.003)
   box('Gauntlet armor chassis',(side*.39,-.022,1.02),(.27,.30,.24),cloth,'Hand'+('L' if side<0 else 'R'),.035)
   for j in range(4):box('Segmented knuckle',(side*.39+(j-1.5)*.055,-.19,1.04),(.048,.055,.10),accent,'Hand'+('L' if side<0 else 'R'),.012)
  plate('Steel jaw respirator',[(-.07,1.637),(.07,1.637),(.063,1.573),(-.063,1.573)],-.205,.03,steel,'Head')
  box('Cybernetic eye',(.048,-.155,1.678),(.045,.04,.025),energy,'Head',.006)
 elif hero=='Traveler':
  hood=ellipsoid('Chronomancer open hood',(0,.04,1.69),(.16,.17,.19),cloth,'Head',32,18)
  # An open fabric shell leaves the real face visible; no porcelain mask or eye slit.
  bm=bmesh.new();bm.from_mesh(hood.data)
  opening=[f for f in bm.faces if f.calc_center_median().y<-.035 and f.calc_center_median().z<1.805 and abs(f.calc_center_median().x)<.13]
  bmesh.ops.delete(bm,geom=opening,context='FACES');bm.to_mesh(hood.data);bm.free()
  for side in [-1,1]:
   tube('Open hood stitched rim',[(side*.12,-.075,1.56),(side*.135,-.09,1.69),(side*.095,-.095,1.79),(0,-.09,1.82)],.010,gold,'Head')
   tube('Hood fabric fold',[(side*.14,.02,1.59),(side*.155,.025,1.71),(side*.105,.045,1.82)],.014,accent,'Head')
 if hero=='Traveler':
  # Dark armored hooded traveler: chainmail, layered coat and restrained silver engraving.
  removed=[o for o in parts if o.name.startswith(('DF_Torso','Breastplate rim','Cuirass engraving','Chest relic','Tasset','Armor rivet'))]
  for o in removed:parts.remove(o);bpy.data.objects.remove(o,do_unlink=True)
  plate('Traveler chainmail cuirass',[(-.18,1.39),(-.12,1.44),(.12,1.44),(.18,1.39),(.14,1.16),(0,1.11),(-.14,1.16)],-.174,.035,cloth)
  mail=mat('Dark chainmail links',(.13,.15,.17),.72,.58)
  for row in range(10):
   z=1.18+row*.023
   for column in range(13):
    x=(column-6)*.022+(.011 if row%2 else 0)
    if abs(x)<.148:
     tube('Chainmail link',[(x-.007,-.211,z),(x,-.214,z+.008),(x+.007,-.211,z),(x,-.212,z-.008),(x-.007,-.211,z)],.0018,mail,'Chest',5)
  for side in [-1,1]:
   plate('Traveler shoulder plate',[(side*.16,1.41),(side*.25,1.445),(side*.32,1.39),(side*.28,1.29),(side*.19,1.31)],-.08,.055,cloth,'UpperArm'+('L' if side<0 else 'R'))
   tube('Traveler shoulder silver border',[(side*.16,-.11,1.41),(side*.25,-.11,1.445),(side*.32,-.11,1.39),(side*.28,-.11,1.29)],.005,gold,'UpperArm'+('L' if side<0 else 'R'))
   plate('Traveler long coat panel',[(side*.02,1.035),(side*.17,1.05),(side*.24,.64),(side*.19,.49),(side*.025,.57)],-.158,.026,cloth,'Hips')
   tube('Traveler coat embroidery',[(side*.045,-.191,.97),(side*.13,-.191,.86),(side*.085,-.191,.74),(side*.16,-.191,.63),(side*.08,-.191,.59)],.004,gold,'Hips')
   for j in range(4):
    z=1.01+j*.034;x=side*(.34-j*.014)
    tube('Traveler silver bracer etching',[(x-.027,-.083,z),(x,-.088,z+.014),(x+.027,-.083,z)],.004,gold,'Forearm'+('L' if side<0 else 'R'))
   for j in range(4):
    z=.20+j*.057
    tube('Traveler greave engraving',[(side*.15-.032,-.083,z),(side*.15,-.093,z+.024),(side*.15+.032,-.083,z)],.004,gold,'Shin'+('L' if side<0 else 'R'))
   ellipsoid('Traveler luminous eye',(side*.035,-.148,1.677),(.006,.004,.004),energy,'Head')
  plate('Traveler waist armor',[(-.145,1.16),(-.12,1.06),(0,1.025),(.12,1.06),(.145,1.16),(0,1.20)],-.196,.026,cloth,'Hips')
  for j in range(3):
   z=1.08+j*.033;tube('Traveler waist filigree',[(-.11,-.232,z),(0,-.240,z+.029),(.11,-.232,z)],.005,gold,'Hips')
 if hero=='Elyra':
  # Reference direction: a sealed demonic helm, swept luminous horns and dark plate armor.
  darkmetal=mat('Demon midnight armor',(.024,.035,.065),.82,.36)
  edge=mat('Demon etched steel',(.10,.20,.27),.88,.29)
  blue=mat('Demon horn chronoglow',(.04,.38,.72),.25,.30,1.25)
  removed=[o for o in parts if o.name.startswith(('Demon','HairLock','Flowing hair','Astral diadem','Crown spire','Archmage crown spire','Sorceress silver','Chest relic'))]
  for o in removed:parts.remove(o);bpy.data.objects.remove(o,do_unlink=True)
  ellipsoid('Demon sealed helm',(0,.015,1.686),(.133,.128,.177),darkmetal,'Head',32,18)
  plate('Demon long faceplate',[(-.075,1.80),(0,1.846),(.075,1.80),(.108,1.718),(.062,1.58),(0,1.545),(-.062,1.58),(-.108,1.718)],-.185,.042,darkmetal,'Head')
  for side in [-1,1]:
   tube('Demon faceplate silver ridge',[(side*.035,-.231,1.794),(side*.061,-.237,1.711),(side*.026,-.231,1.588)],.006,edge,'Head')
   tube('Demon glowing eye slit',[(side*.013,-.235,1.723),(side*.060,-.230,1.739)],.005,blue,'Head')
   tube('Demon great curved horn',[(side*.075,.005,1.75),(side*.16,.005,1.85),(side*.208,.005,2.03),(side*.175,.005,2.18),(side*.135,-.005,2.205),(side*.112,-.008,2.145)],[.036,.047,.041,.025,.014,.001],darkmetal,'Head',16)
   tube('Demon horn luminous outer edge',[(side*.11,-.037,1.78),(side*.20,-.036,1.89),(side*.239,-.026,2.05),(side*.190,-.018,2.19),(side*.144,-.012,2.21)],[.009,.014,.014,.010,.001],blue,'Head',10)
   for j in range(3):
    x=side*(.23+j*.042);z=1.42-j*.027
    plate('Demon layered shoulder plate',[(x-side*.065,z+.04),(x+side*.05,z+.055),(x+side*.09,z-.014),(x+side*.05,z-.067),(x-side*.055,z-.045)],-.072-j*.020,.050,darkmetal,'UpperArm'+('L' if side<0 else 'R'))
    tube('Demon shoulder blade spike',[(x,-.032,z),(x+side*.09,.01,z+.04),(x+side*.13,.055,z+.155)],[.025,.023,.001],edge,'UpperArm'+('L' if side<0 else 'R'))
   plate('Demon armored forearm',[(side*.29,1.18),(side*.39,1.16),(side*.43,.99),(side*.36,.97)],-.091,.028,darkmetal,'Forearm'+('L' if side<0 else 'R'))
   tube('Demon forearm glowing seam',[(side*.32,-.123,1.15),(side*.385,-.125,1.01)],.004,blue,'Forearm'+('L' if side<0 else 'R'))
   tube('Demon chest armor rib',[(side*.14,-.241,1.36),(side*.10,-.248,1.24),(side*.07,-.234,1.13)],.014,edge)
  plate('Demon pointed chest emblem',[(-.055,1.42),(.055,1.42),(.067,1.32),(0,1.235),(-.067,1.32)],-.244,.022,darkmetal)
  for j in [-1,0,1]:tube('Demon chest energy slit',[(j*.022,-.274,1.395),(j*.018,-.274,1.32)],[.005,.001],blue)
  for o in parts:
   if o.name.startswith(('Ceremonial robe','Silk upper mantle','Tasset','Front robe','Front silk panel')):
    o.data.materials.clear();o.data.materials.append(darkmetal if 'Tasset' in o.name else cloth)
   elif o.name.startswith('DF_Torso'):
    o.data.materials.clear();o.data.materials.append(darkmetal)
 if hero=='Nyra':
  # Asymmetric cyborg face: retain the biological half rather than adding another mask.
  plate('Cyborg facial armor',[(-.005,1.755),(.075,1.75),(.110,1.692),(.084,1.615),(.012,1.594),(-.006,1.660)],-.155,.025,steel,'Head')
  ellipsoid('Cyborg sensor housing',(.045,-.203,1.692),(.035,.015,.029),cloth,'Head',18,10)
  ellipsoid('Cyborg targeting lens',(.045,-.222,1.692),(.019,.008,.017),energy,'Head',18,10)
  tube('Cyborg cheek circuitry',[(.055,-.206,1.662),(.082,-.193,1.64),(.063,-.187,1.615)],.004,energy,'Head')
  box('Cyborg temple module',(.117,-.015,1.701),(.028,.07,.08),steel,'Head',.006)
  tube('Cyborg data cable',[(.12,.006,1.73),(.13,.05,1.64),(.105,.068,1.57)],.008,cloth,'Head')
  # Remove goggles that would cover the new sensor face.
  removed=[o for o in parts if 'goggle' in o.name.lower()]
  for o in removed:parts.remove(o);bpy.data.objects.remove(o,do_unlink=True)
 if hero in ['Veyr','Nyra']:
  # Slim boots without moving the ankle or reducing leg length.
  for o in parts:
   if o.get('bone','').startswith('Foot'):
    side=-1 if o['bone'].endswith('L') else 1
    pivot=Vector((side*.18,-.015,.08))
    for v in o.data.vertices:
     delta=v.co-pivot;v.co=pivot+Vector((delta.x*.80,delta.y*.80,delta.z*.90))
 # Remove the shared shoulder silhouette from robe and recon classes.
 if hero in ['Nyra','Elyra','Mira','Traveler']:
  removed=[o for o in parts if o.name.startswith('Layered pauldron')]
  for o in removed:parts.remove(o);bpy.data.objects.remove(o,do_unlink=True)
 arm=rig();arm['hero']=hero
 for o in parts:
  if o.get('bone','').startswith('Hand'):
   side=-1 if o['bone'].endswith('L') else 1
   shift=Vector((side*.005,0,-.035)) if hero=='Veyr' else Vector((side*.02,.035,-.10))
   for v in o.data.vertices:v.co+=shift
  bind(o,arm,o==body)
 # Proportions alter anatomy, costume, weapon positions and bind skeleton together.
 for o in parts:
  for v in o.data.vertices:v.co=proportion(hero,v.co)
 bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
 for bone in arm.data.edit_bones:
  bone.head=proportion(hero,bone.head);bone.tail=proportion(hero,bone.tail)
 bpy.ops.object.mode_set(mode='OBJECT')
 animations(arm,hero)
 # Explicit named VFX socket travels with the weapon hand.
 socket=bpy.data.objects.new('DF_Muzzle',None);bpy.context.collection.objects.link(socket);socket.parent=arm;socket.parent_type='BONE';socket.parent_bone='HandR' if hero=='Rook' else 'WeaponGripR';bpy.context.view_layer.update()
 muzzle_point={'Nyra':(.40,-.99,.97),'Mira':(.42,-.07,1.49),'Elyra':(.42,-.07,1.49),'Traveler':(.41,-.06,1.12),'Rook':(.39,-.16,.92),'Kaelon':(.40,-.04,1.06),'Veyr':(.39,-.08,.70)}[hero]
 socket.matrix_world=Matrix.Translation(proportion(hero,muzzle_point))
 # Body and shop-addressable weapon renderers share one rig, but never one renderer.
 grouped={'DF_Anatomy':[]}
 for o in parts:
  bone=o.get('bone','')
  key=('DF_Gauntlet' if hero=='Rook' else 'DF_Weapon')+bone[-1] if bone in ['HandL','HandR'] else 'DF_Anatomy'
  grouped.setdefault(key,[]).append(o)
 merged=[]
 for name,objects in grouped.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  active=body if name=='DF_Anatomy' else objects[0]
  bpy.context.view_layer.objects.active=active
  if len(objects)>1:bpy.ops.object.join()
  active.name=name;active.data.name=name;merged.append(active)
 parts=merged
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
 d=bpy.data.cameras.new('Portrait');camera=bpy.data.objects.new('Portrait',d);bpy.context.collection.objects.link(camera);camera.location=(2.7,-6,2.4);aim(camera,(0,-.03,.95));d.type='ORTHO';d.ortho_scale=2.95 if hero=='Elyra' else 2.60;scene.camera=camera
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
