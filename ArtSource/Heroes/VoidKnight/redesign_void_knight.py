"""Build the armor/sword revision against the frozen v1 rig and hand contacts.
Run with Blender Python/bpy. Source parts stay separate; actions and bones are preserved.
"""
from pathlib import Path
import bpy,bmesh,math,json,numpy as np
from mathutils import Vector
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight-v1-reference.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
for t in rig.animation_data.nla_tracks:t.mute=True
bpy.context.view_layer.update()
old_actions={a.name:len(a.slots) for a in bpy.data.actions}
old_bones={b.name:(list(b.head_local),list(b.tail_local)) for b in rig.data.bones}

# Recover the exact authored two-hand grip frame before replacing its geometry.
def mean(o):return sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
g=bpy.data.objects['VK_SwordGrip'];grip=mean(g)
_,_,vt=np.linalg.svd(np.array([list(v.co-grip) for v in g.data.vertices]));axis=Vector(vt[0]);axis*=1 if (mean(bpy.data.objects['VK_SwordBlade'])-grip).dot(axis)>0 else -1
b=bpy.data.objects['VK_SwordBlade'];_,_,vt=np.linalg.svd(np.array([list(v.co-mean(b)) for v in b.data.vertices]));normal=Vector(vt[-1]);normal*=1 if normal.y<0 else -1
across=normal.cross(axis).normalized();normal=axis.cross(across).normalized()
weapon_frame=lambda p:grip+across*p[0]+normal*p[1]+axis*p[2]

# Retain identity, understructure, fingers and all original animation data.
keep=lambda o:o.get('bone')=='Head' or o.name.startswith(('VK_Undersuit','VK_Joint','VK_Palm','VK_Finger','VK_Thumb','VK_Neck'))
for o in list(bpy.data.objects):
 if o.type=='MESH' and not keep(o):bpy.data.objects.remove(o,do_unlink=True)
for o in bpy.data.objects:
 if o.type!='MESH':continue
 if o.name.startswith('VK_Undersuit_') and any(part in o.name for part in ['Thigh','Shin','Forearm','UpperArm']):
  bone=rig.data.bones[o.get('bone')];origin=bone.head_local;direction=(bone.tail_local-origin).normalized()
  factor=.72 if 'Thigh' in o.name else .63 if 'Shin' in o.name else .78
  for v in o.data.vertices:
   delta=v.co-origin;along=direction*delta.dot(direction);v.co=origin+along+(delta-along)*factor
 if o.name=='VK_Undersuit_Pelvis':
  c=mean(o)
  for v in o.data.vertices:v.co=c+Vector(((v.co.x-c.x)*.86,(v.co.y-c.y)*.90,v.co.z-c.z))
 if o.name=='VK_Undersuit_Chest':
  c=mean(o)
  for v in o.data.vertices:v.co=c+Vector(((v.co.x-c.x)*.89,(v.co.y-c.y)*.86,(v.co.z-c.z)*.90))
 if o.name.startswith('VK_Joint_Hand'):
  c=mean(o)
  for v in o.data.vertices:v.co=c+(v.co-c)*.63
 elif o.name.startswith('VK_Joint_'):
  c=mean(o)
  for v in o.data.vertices:v.co=c+(v.co-c)*.84

mats={m.name:m for m in bpy.data.materials}
black=mats['Void black metal'];edge=mats['Void bevel metal'];cloth=mats['Void woven undersuit'];energy=mats['Void energy'];core=mats['Void core energy']
def material(m,color,metal,rough,emission=0):
 m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
material(black,(.024,.030,.046),.82,.33)
material(edge,(.17,.20,.27),.88,.24)
material(cloth,(.010,.012,.020),0,.78)
material(energy,(.27,.018,.70),.15,.30,2.2)
material(core,(.48,.075,.90),.1,.24,2.6)
created=[]
def mesh(name,verts,faces,mat,bone,bevel=.002,frame=None,face_mats=None):
 verts=[frame(v) if frame else Vector(v) for v in verts]
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
 bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
 o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.parent=rig;o['bone']=bone;o['design_version']=2
 for m in (mat if isinstance(mat,list) else [mat]):data.materials.append(m)
 if face_mats:
  for p,i in zip(data.polygons,face_mats):p.material_index=i
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 if bevel:
  mod=o.modifiers.new('Machined edge radii','BEVEL');mod.width=bevel;mod.segments=2;mod.affect='EDGES'
  bpy.ops.object.modifier_apply(modifier=mod.name)
 if bevel:
  for polygon in o.data.polygons:polygon.use_smooth=True
  o.data.set_sharp_from_angle(angle=math.radians(42))
  weighted=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');weighted.keep_sharp=True;weighted.weight=50
  bpy.ops.object.modifier_apply(modifier=weighted.name)
 vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(o.data.vertices))),1,'REPLACE')
 deform=o.modifiers.new('Original rig','ARMATURE');deform.object=rig
 created.append(o);return o

def plate(name,poly,front,depth,mat,bone,bevel=.003,frame=None,ridge=0):
 n=len(poly);ys=front if isinstance(front,list) else [front]*n
 verts=[(x,y,z) for (x,z),y in zip(poly,ys)]+[(x,y+depth,z) for (x,z),y in zip(poly,ys)]
 faces=[]
 if ridge:
  verts.append((sum(x for x,z in poly)/n,sum(ys)/n-ridge,sum(z for x,z in poly)/n));faces.extend((i,(i+1)%n,2*n) for i in range(n))
 else:faces.append(tuple(range(n)))
 faces.append(tuple(range(n,2*n)));faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
 return mesh(name,verts,faces,mat,bone,bevel,frame)

def loft(name,rings,mat,bone,frame=None,sides=12,bevel=.002):
 verts=[]
 for z,rx,ry,cx,cy in rings:
  for i in range(sides):
   a=2*math.pi*i/sides;verts.append((cx+rx*math.cos(a),cy+ry*math.sin(a),z))
 faces=[tuple(range(sides-1,-1,-1))]
 for j in range(len(rings)-1):
  for i in range(sides):faces.append((j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i))
 faces.append(tuple(range((len(rings)-1)*sides,len(rings)*sides)))
 return mesh(name,verts,faces,mat,bone,bevel,frame)

def strip(name,points,width,mat,bone,frame=None):
 # Thin physical inset ribbons on armor surfaces; all points follow one binding.
 verts=[]
 for i,p in enumerate(points):
  p=Vector(p);d=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
  side=Vector((d.z,0,-d.x)).normalized()*width*.5
  verts.extend([p+side,p-side])
 faces=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(points)-1)]
 return mesh(name,verts,faces,mat,bone,0,frame)

def bone_frame(bone):
 b=rig.data.bones[bone];head=b.head_local.copy();d=(b.tail_local-head).normalized();u=Vector((1,0,0));u=(u-d*u.dot(d)).normalized();v=u.cross(d).normalized()
 return lambda p:head+u*p[0]+v*p[1]+d*p[2],(b.tail_local-head).length

# Tapered chassis and split pectoral shells. Quiet large surfaces carry the silhouette.
loft('VK2_ChestChassis',[(1.20,.175,.112,0,0),(1.34,.230,.145,0,0),(1.46,.242,.143,0,0),(1.535,.190,.112,0,0)],black,'Chest',sides=16,bevel=.004)
for side,label in [(-1,'L'),(1,'R')]:
 def sx(poly):return [(side*x,z) for x,z in poly]
 plate('VK2_Pectoral_'+label,sx([(.012,1.485),(.125,1.515),(.244,1.452),(.215,1.340),(.048,1.290),(.014,1.357)]),[-.188,-.177,-.128,-.152,-.205,-.211],.018,black,'Chest',ridge=.019)
 strip('VK2_ChestEdge_'+label,[(side*.025,-.215,1.473),(side*.115,-.205,1.491),(side*.208,-.163,1.449)],.008,edge,'Chest')
 strip('VK2_ChestLight_'+label,[(side*.032,-.230,1.378),(side*.131,-.211,1.414),(side*.203,-.177,1.438)],.008,energy,'Chest')
 plate('VK2_Collar_'+label,sx([(.055,1.529),(.105,1.557),(.206,1.516),(.159,1.485),(.070,1.510)]),-.120,.035,edge,'Chest',.002)
# Compact diamond with its own dark housing, physically sitting on the breastplate.
plate('VK2_CoreHousing',[(0,1.463),(.050,1.397),(0,1.330),(-.050,1.397)],-.244,.025,edge,'Chest',.003)
plate('VK2_CoreSocket',[(0,1.448),(.035,1.397),(0,1.346),(-.035,1.397)],-.249,.010,black,'Chest',.001)
plate('VK2_CoreCrystal',[(0,1.434),(.023,1.397),(0,1.360),(-.023,1.397)],-.254,.012,core,'Chest',.001,ridge=.008)
# Articulated abdominal chevrons, waist and hip plates.
for i in range(3):
 z=1.08+i*.065;w=.166+i*.011
 plate('VK2_Abdominal_'+str(i),[(-w,z+.047),(-.05,z+.055),(0,z+.043),(.05,z+.055),(w,z+.047),(w*.86,z),(.04,z-.008),(0,z-.016),(-.04,z-.008),(-w*.86,z)],-.151-i*.007,.035,black,'Spine',.003,ridge=.012)
 strip('VK2_AbdomenSeam_'+str(i),[(-w*.72,-.178-i*.007,z+.010),(0,-.186-i*.007,z-.001),(w*.72,-.178-i*.007,z+.010)],.004,edge,'Spine')
loft('VK2_WaistBelt',[(1.005,.207,.142,0,0),(1.045,.205,.142,0,0)],black,'Hips',sides=16,bevel=.003)
plate('VK2_BeltBuckle',[(-.045,1.046),(.045,1.046),(.052,1.020),(0,.984),(-.052,1.020)],-.160,.026,edge,'Hips',.003)
strip('VK2_BuckleLight',[(-.021,-.191,1.026),(0,-.193,1.007),(.021,-.191,1.026)],.006,energy,'Hips')
for side,label in [(-1,'L'),(1,'R')]:
 plate('VK2_Hip_'+label,[(side*x,z) for x,z in [(.022,.986),(.164,1.008),(.235,.955),(.220,.846),(.162,.779),(.075,.873)]],-.145,.030,black,'Thigh'+label,.005,ridge=.010)
 strip('VK2_HipSeam_'+label,[(side*.183,-.187,.953),(side*.191,-.187,.875),(side*.162,-.187,.833)],.005,edge,'Thigh'+label)
# Rear spine enclosure and overlapping scapular plates.
loft('VK2_BackChassis',[(1.225,.172,.05,0,.08),(1.40,.222,.065,0,.08),(1.52,.187,.04,0,.08)],black,'Chest',sides=12,bevel=.003)
for i in range(5):
 z=1.27+i*.045
 plate('VK2_BackSpine_'+str(i),[(-.037,z+.029),(.037,z+.029),(.030,z),(0,z-.010),(-.030,z)],.165,.018,edge,'Chest',.002)
 strip('VK2_BackEnergy_'+str(i),[(0,.188,z+.004),(0,.188,z+.021)],.009,energy,'Chest')
# Curved shoulder caps and layered sleeves follow existing arm bones.
for side,label in [(-1,'L'),(1,'R')]:
 cx=side*.300
 loft('VK2_Pauldron_'+label,[(1.424,.105,.108,cx,0),(1.475,.149,.130,cx,0),(1.550,.140,.123,cx,0),(1.610,.077,.077,cx,0)],black,'UpperArm'+label,sides=12,bevel=.004)
 plate('VK2_PauldronFace_'+label,[(side*x,z) for x,z in [(.208,1.553),(.290,1.583),(.417,1.535),(.407,1.479),(.321,1.453),(.235,1.486)]],[-.079,-.100,-.088,-.098,-.138,-.129],.017,black,'UpperArm'+label,.004,ridge=.010)
 strip('VK2_ShoulderEdge_'+label,[(side*.230,-.102,1.554),(side*.295,-.120,1.566),(side*.394,-.111,1.531)],.008,edge,'UpperArm'+label)
 strip('VK2_ShoulderLight_'+label,[(side*.278,-.140,1.514),(side*.325,-.145,1.497),(side*.382,-.128,1.513)],.007,energy,'UpperArm'+label)
 if label=='L':
  plate('VK2_GuardShoulderLip',[(side*x,z) for x,z in [(.313,1.601),(.407,1.558),(.450,1.470),(.416,1.445),(.391,1.521),(.302,1.562)]],-.072,.100,black,'UpperArmL',.004)
 frame,length=bone_frame('UpperArm'+label)
 loft('VK2_Bicep_'+label,[(length*.30,.084,.082,0,0),(length*.56,.091,.084,0,0),(length*.82,.075,.073,0,0)],black,'UpperArm'+label,frame,sides=10,bevel=.003)
 frame,length=bone_frame('Forearm'+label)
 loft('VK2_Bracer_'+label,[(length*.12,.078,.077,0,0),(length*.36,.088,.083,0,0),(length*.78,.066,.068,0,0),(length*.94,.060,.060,0,0)],black,'Forearm'+label,frame,sides=10,bevel=.003)
 poly=[(-.058,length*.18),(.058,length*.18),(.062,length*.34),(.041,length*.88),(0,length*.97),(-.043,length*.86),(-.064,length*.34)]
 plate('VK2_VambraceFace_'+label,poly,-.086,.016,black,'Forearm'+label,.003,frame,ridge=.009)
 strip('VK2_BracerInlay_'+label,[(0,-.105,length*.28),(0,-.104,length*.70),(-.016,-.098,length*.82)],.007,energy,'Forearm'+label,frame)
 palm=bpy.data.objects['VK_Palm_'+label];c=mean(palm)
 plate('VK2_GlovePlate_'+label,[(c.x-.040,c.z+.047),(c.x+.040,c.z+.047),(c.x+.047,c.z-.020),(c.x+.024,c.z-.049),(c.x-.026,c.z-.049),(c.x-.047,c.z-.016)],c.y-.052,.018,black,'Hand'+label,.004,ridge=.006)
 for i in range(3):
  x=c.x+(i-1)*.025
  plate('VK2_Knuckle_'+label+'_'+str(i),[(x-.009,c.z-.012),(x+.009,c.z-.012),(x+.009,c.z-.031),(x-.009,c.z-.031)],c.y-.064,.008,edge,'Hand'+label,.002)
# Long split thigh plates, knee shields and fluted shin armor.
for side,label in [(-1,'L'),(1,'R')]:
 frame,length=bone_frame('Thigh'+label)
 loft('VK2_Cuisse_'+label,[(length*.19,.093,.083,0,0),(length*.48,.098,.086,0,0),(length*.83,.081,.074,0,0)],black,'Thigh'+label,frame,sides=12,bevel=.003)
 for sign in [-1,1]:
  poly=[(sign*.010,length*.22),(sign*.071,length*.20),(sign*.083,length*.43),(sign*.063,length*.79),(sign*.018,length*.87)]
  plate('VK2_ThighPlate_'+label+str(sign),poly,-.091,.022,black,'Thigh'+label,.004,frame,ridge=.006)
 strip('VK2_ThighLight_'+label,[(0,-.110,length*.31),(0,-.109,length*.68)],.007,energy,'Thigh'+label,frame)
 frame,length=bone_frame('Shin'+label)
 loft('VK2_Greave_'+label,[(.025,.086,.080,0,0),(.12,.091,.083,0,0),(.27,.074,.065,0,0),(.35,.061,.057,0,0)],black,'Shin'+label,frame,sides=12,bevel=.003)
 plate('VK2_Knee_'+label,[(-.075,-.025),(0,-.049),(.075,-.025),(.080,.034),(.040,.090),(0,.108),(-.040,.090),(-.080,.034)],-.096,.033,black,'Shin'+label,.005,frame,ridge=.012)
 strip('VK2_KneeEdge_'+label,[(-.055,-.119,.018),(0,-.132,.072),(.055,-.119,.018)],.007,edge,'Shin'+label,frame)
 plate('VK2_ShinRidge_'+label,[(-.035,.100),(0,.083),(.035,.100),(.025,.302),(0,.350),(-.025,.302)],-.091,.020,edge,'Shin'+label,.003,frame,ridge=.008)
 for sign in [-1,1]:strip('VK2_ShinLight_'+label+str(sign),[(sign*.047,-.101,.125),(sign*.034,-.090,.272),(sign*.021,-.078,.321)],.006,energy,'Shin'+label,frame)
 # Chamfered soles retain the original ground plane exactly.
 cx=side*.17
 outline=[(-.077,-.264),(.077,-.264),(.105,-.224),(.105,.078),(.070,.108),(-.070,.108),(-.105,.078),(-.105,-.224)]
 verts=[(cx+x,y,z) for z in [.012,.047] for x,y in outline];n=len(outline);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 sole=mesh('VK2_Sole_'+label,verts,faces,edge,'Foot'+label,.008)
 # Bevel reaches z=0.004; place an actual flat outsole at z=0.
 loft('VK2_Outsole_'+label,[(0,.095,.168,cx,-.075),(.018,.098,.170,cx,-.075)],cloth,'Foot'+label,sides=12,bevel=0)
 loft('VK2_Boot_'+label,[(.038,.100,.166,cx,-.073),(.100,.098,.157,cx,-.075),(.170,.078,.110,cx,-.025)],black,'Foot'+label,sides=12,bevel=.004)
 for i in range(3):
  y=-.228+i*.060
  plate('VK2_Sabaton_'+label+'_'+str(i),[(cx-.080,.063+i*.025),(cx+.080,.063+i*.025),(cx+.073,.107+i*.022),(cx,.123+i*.022),(cx-.073,.107+i*.022)],y,.063,black,'Foot'+label,.004,ridge=.004)
 strip('VK2_BootMark_'+label,[(cx-.038,-.235,.105),(cx,-.237,.120),(cx+.038,-.235,.105)],.005,energy,'Foot'+label)
# Broad diamond-section blade: actual steel facets, a narrow luminous fuller and swept guard.
rings=[(.214,.050,.025),(.338,.100,.030),(.433,.135,.030),(1.155,.087,.025),(1.378,.001,.001)]
verts=[]
for z,w,t in rings:
 for x,y in [(-w,0),(-w*.73,t*.72),(-w*.20,t),(w*.20,t),(w*.73,t*.72),(w,0),(w*.73,-t*.72),(w*.20,-t),(-w*.20,-t),(-w*.73,-t*.72)]:verts.append((x,y,z))
faces=[tuple(range(9,-1,-1))];fm=[0]
for j in range(len(rings)-1):
 for i in range(10):faces.append((j*10+i,j*10+(i+1)%10,(j+1)*10+(i+1)%10,(j+1)*10+i));fm.append(1 if i in [0,4,5,9] else 0)
faces.append(tuple(range(40,50)));fm.append(0)
mesh('VK2_SwordBlade',verts,faces,[black,edge],'WeaponGripR',.0015,weapon_frame,fm)
for sign in [-1,1]:
 poly=[(0,.315),(.024,.427),(.018,1.121),(0,1.292),(-.018,1.121),(-.024,.427)]
 # plate faces are mirrored across the blade plane, so the energy reads from both sides.
 plate('VK2_SwordFuller_'+str(sign),poly,sign*.034,-sign*.005,energy,'WeaponGripR',.001,weapon_frame)
 for k in range(3):
  z=.57+k*.16
  strip('VK2_BladeEtch_'+str(sign)+'_'+str(k),[(-.040,sign*.031,z),(-.058,sign*.029,z+.028),(-.044,sign*.031,z+.051)],.004,edge,'WeaponGripR',weapon_frame)
for sign in [-1,1]:
 poly=[(0,.233),(sign*.090,.275),(sign*.190,.228),(sign*.253,.291),(sign*.230,.172),(sign*.120,.158),(0,.185)]
 plate('VK2_SweptGuard_'+str(sign),poly,-.038,.076,black,'WeaponGripR',.005,weapon_frame)
 strip('VK2_GuardEdge_'+str(sign),[(sign*.027,.044,.235),(sign*.095,.044,.255),(sign*.185,.044,.213),(sign*.228,.044,.256)],.008,edge,'WeaponGripR',weapon_frame)
for sign in [-1,1]:
 poly=[(0,.293),(.057,.223),(0,.155),(-.057,.223)]
 plate('VK2_GuardCoreFrame_'+str(sign),poly,sign*.044,-sign*.018,edge,'WeaponGripR',.003,weapon_frame)
 plate('VK2_GuardCore_'+str(sign),[(0,.269),(.035,.223),(0,.178),(-.035,.223)],sign*.060,-sign*.010,core,'WeaponGripR',.002,weapon_frame,ridge=-sign*.004)
loft('VK2_SwordGrip',[(-.190,.024,.024,0,0),(.190,.024,.024,0,0)],cloth,'WeaponGripR',weapon_frame,sides=12,bevel=.001)
for i in range(13):
 z=-.177+i*.028
 loft('VK2_GripWrap_'+str(i),[(z-.004,.0255,.0255,0,0),(z+.004,.0255,.0255,0,0)],black,'WeaponGripR',weapon_frame,sides=10,bevel=.001)
loft('VK2_Pommel',[(-.255,.014,.014,0,0),(-.230,.043,.034,0,0),(-.195,.029,.026,0,0)],edge,'WeaponGripR',weapon_frame,sides=6,bevel=.002)
plate('VK2_PommelGem',[(0,-.247),(.018,-.228),(0,-.208),(-.018,-.228)],.037,-.008,energy,'WeaponGripR',.001,weapon_frame)
# No detached geometry: all new pieces have a single original bone group and armature modifier.
assert {a.name:len(a.slots) for a in bpy.data.actions}==old_actions
assert {b.name:(list(b.head_local),list(b.tail_local)) for b in rig.data.bones}==old_bones
rig['design_version']=2;rig['grip_center']=list(grip);rig['grip_axis']=list(axis);rig['grip_span']=.38
rig.data.pose_position='POSE';rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.context.scene['void_knight_design']='Obsidian articulated armor / swept-guard greatsword v2'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight.blend'))
print(json.dumps({'new_parts':len(created),'all_parts':len([o for o in bpy.data.objects if o.type=='MESH']),'materials':len(bpy.data.materials),'clips':len(bpy.data.actions),'grip_center':list(grip),'grip_axis':list(axis)},indent=2))
