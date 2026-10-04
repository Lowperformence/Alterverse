"""Blender-authored Kryon armored frost bear; Y-up rigid-skin export for the existing combat rig."""
from pathlib import Path
import bpy, math, json, gzip
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/Alterverse/Resources/KryonBear';ART=ROOT/'ArtSource/HotZoneMonsters/KryonBear'
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
colors=[(.065,.105,.13),(.53,.72,.81),(.83,.93,.96),(.015,.75,1),(.24,.48,.62)]
mats=[]
for i,c in enumerate(colors):
 m=bpy.data.materials.new(['Graphite joints','Glacier armor','Frost edges','Cyan reactor','Blue titanium'][i]);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=.55 if i!=2 else .22;p.inputs['Roughness'].default_value=.37
 if i==3:p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=2.5
 mats.append(m)
parts=[];joints=[];positions={'':Vector((0,0,0))};paths=['']
def joint(path,parent,position):
 paths.append(path);positions[path]=positions[parent]+Vector(position);joints.append(dict(path=path,parent=parent,position=list(position)))
joint('DF_KryonBearHeadPivot','',(0,.64,.94));joint('Tail','',(0,.5,-1.2))
for i in range(4):
 front=i<2;s=-1 if i%2==0 else 1
 p=f'DF_KryonBearLeg_{i}';joint(p,'',(s*.66,.30,.58 if front else -.78));joint(p+'/Knee',p,(0,-.70,.06));joint(p+'/Knee/Paw',p+'/Knee',(0,-.64,.28))
def finish(o,name,mat,bone):
 o.name=name;o.data.materials.append(mats[mat]);o['rig_joint']=bone;parts.append((o,bone));return o
def box(name,pos,size,mat=1,bone='',rot=(0,0,0),bevel=.055):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.rotation_euler=tuple(math.radians(a) for a in rot)
 if bevel:
  mod=o.modifiers.new('Manufactured chamfers','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish(o,name,mat,bone)
def ball(name,pos,size,mat=0,bone=''):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,radius=1,location=pos);o=bpy.context.object;o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,name,mat,bone)
def cylinder(name,pos,radius,depth,mat=0,bone='',axis=(1,0,0)):
 bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=radius,depth=depth,location=pos);o=bpy.context.object;o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler();return finish(o,name,mat,bone)
def ring(name,pos,radius,mat=3,bone=''):
 bpy.ops.mesh.primitive_torus_add(major_radius=radius,minor_radius=.018,major_segments=20,minor_segments=6,location=pos,rotation=(0,math.pi/2,0));return finish(bpy.context.object,name,mat,bone)
def rod(name,a,b,r=.04,mat=0,bone=''):
 a,b=Vector(a),Vector(b);return cylinder(name,(a+b)/2,r,(b-a).length,mat,bone,b-a)
def plate(name,x,points,depth,mat=1,bone=''):
 vs=[(x+s*depth/2,y,z) for s in [-1,1] for y,z in points];n=len(points);fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,name,mat,bone)
ball('barrel chassis',(0,.48,-.15),(.73,.50,1.16))
box('chest structural cage',(0,.42,.69),(1.08,.72,.75),0)
for k in range(5):
 z=.73-k*.39;box('overlapping dorsal carapace',(0,.9,z),(1.12 if k<3 else .96,.20,.35),2,rot=(-7,0,0))
 for s in [-1,1]:
  plate('segmented flank shield',s*.7,[(.22,z+.14),(.65,z+.21),(.86,z+.10),(.83,z-.17),(.33,z-.18)],.15,1)
  box('cyan recessed seam',(s*.79,.42,z),(.025,.12,.055),3,bevel=.009)
  for j in range(3):box('machined vent',(s*.79,.64-j*.055,z+.04),(.018,.022,.14),0,bevel=.004)
  cylinder('plate bolt',(s*.8,.75,z),.032,.018,0)
box('sternum armor',(0,.4,1.05),(.75,.65,.18),2,rot=(-15,0,0))
box('chest reactor',(0,.48,1.17),(.23,.16,.035),3,bevel=.03)
# A broad short muzzle, rounded bear ears and metal teeth, not a feline skull.
bone='DF_KryonBearHeadPivot'
ball('neck bellows',(0,.65,1.02),(.46,.4,.48),0,bone)
box('brow skull',(0,.82,1.4),(.8,.46,.60),1,bone,rot=(-12,0,0),bevel=.12)
box('snout armor',(0,.59,1.74),(.65,.31,.53),2,bone,rot=(5,0,0),bevel=.085)
box('black nose',(0,.62,2.01),(.34,.15,.095),0,bone,bevel=.04)
box('lower jaw',(0,.35,1.70),(.55,.13,.62),0,bone,rot=(-7,0,0),bevel=.05)
for s in [-1,1]:
 ball('rounded armored ear',(s*.34,1.08,1.18),(.145,.165,.10),1,bone)
 ball('ear inset',(s*.34,1.08,1.265),(.075,.09,.026),0,bone)
 plate('cheek plate',s*.4,[(.43,1.39),(.7,1.31),(.87,1.54),(.6,1.76)],.10,2,bone)
 box('cyan eye',(s*.355,.79,1.685),(.13,.064,.036),3,bone,rot=(0,s*20,s*-8),bevel=.014)
 box('eyebrow',(s*.36,.85,1.68),(.21,.065,.09),0,bone,rot=(0,0,s*-12),bevel=.014)
 cylinder('jaw hinge',(s*.445,.48,1.35),.11,.04,0,bone);ring('jaw cyan seal',(s*.47,.48,1.35),.075,3,bone)
 for j in range(4):
  bpy.ops.mesh.primitive_cone_add(vertices=5,radius1=.011,radius2=.048,depth=.13,location=(s*.23,.415,1.49+j*.115),rotation=(math.pi/2,0,0));finish(bpy.context.object,'metal fang',2,bone)
# Four independently rigged legs, broad paws, exposed pistons and recessed circular joint housings.
for i in range(4):
 p=f'DF_KryonBearLeg_{i}';k=p+'/Knee';paw=k+'/Paw';s=-1 if i%2==0 else 1;z=.58 if i<2 else -.78;x=s*.66
 ball('shoulder muscle chassis',(x,.19,z),(.36,.43,.36),0,p)
 plate('shoulder armor',x+s*.13,[(.57,z-.32),(.63,z+.12),(.37,z+.4),(-.13,z+.32),(-.31,z),(-.12,z-.31)],.33,2,p)
 cylinder('shoulder rotary housing',(x+s*.32,.22,z),.27,.11,0,p);cylinder('shoulder hub',(x+s*.39,.22,z),.17,.06,4,p);ring('shoulder illuminated gasket',(x+s*.43,.22,z),.20,3,p)
 for a in range(6):
  t=a*math.tau/6;cylinder('hub fastener',(x+s*.40,.22+math.cos(t)*.24,z+math.sin(t)*.24),.022,.02,2,p)
 box('upper limb armor',(x,-.14,z+.15),(.36,.53,.32),1,p,rot=(-12,0,s*4))
 for q in [-1,1]:rod('hydraulic ram',(x+q*.17,.00,z-.18),(x+q*.17,-.47,z-.1),.045,4,p)
 cylinder('knee joint',(x,-.40,z+.06),.16,.42,0,k);ring('knee ring',(x+s*.23,-.40,z+.06),.11,3,k)
 box('shin armor',(x,-.69,z+.22),(.35,.46,.29),2,k,rot=(-14,0,0))
 box('shin inset',(x,-.65,z+.38),(.16,.24,.035),4,k,bevel=.02)
 box('shin status strip',(x,-.66,z+.405),(.04,.16,.014),3,k,bevel=.005)
 ball('wrist',(x,-.96,z+.30),(.2,.12,.22),0,paw)
 box('paw dorsal plate',(x,-1.04,z+.41),(.59,.18,.61),1,paw,bevel=.075)
 for toe in range(4):
  tx=x+(toe-1.5)*.135
  box('toe knuckle',(tx,-1.10,z+.67),(.12,.13,.24),0,paw,bevel=.035)
  rod('ice titanium claw',(tx,-1.11,z+.73),(tx,-1.15,z+.91),.039,2,paw)
# Short armored bear tail and frost-scuffed panel details.
for i in range(4):
 ball('segmented short tail',(0,.47-i*.065,-1.27-i*.12),(.12-i*.016,.13-i*.012,.14),1,'Tail')
for s in [-1,1]:
 for j in range(9):
  z=-.95+j*.23;box('frost edge chip',(s*.59,.91,z),(.08,.018,.045),2,rot=(0,0,s*12),bevel=.005)
# Export triangulated evaluated geometry in each rigid bone's local frame.
vertices=[];normals=[];bones=[];sub=[dict(triangles=[]) for _ in colors]
for o,bone in parts:
 o.data.calc_loop_triangles();mat=o.data.materials[0];mi=mats.index(mat);normalmat=o.matrix_world.to_3x3().inverted().transposed()
 for tri in o.data.loop_triangles:
  for vi in tri.vertices:
   pos=o.matrix_world@o.data.vertices[vi].co-positions[bone];n=(normalmat@tri.normal).normalized();idx=len(bones)
   vertices.extend(pos);normals.extend(n);bones.append(paths.index(bone));sub[mi]['triangles'].append(idx)
data=dict(version=1,bonePaths=paths,joints=joints,vertices=vertices,normals=normals,boneIndices=bones,submeshes=sub)
with gzip.GzipFile(filename=str(OUT/'Model.bytes'),mode='wb',mtime=0) as f:f.write(json.dumps(data,separators=(',',':')).encode())
# Editable Blender armature with rigid vertex-group attachment.
armdata=bpy.data.armatures.new('KryonBearSkeleton');arm=bpy.data.objects.new('KryonBearSkeleton',armdata);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for p in paths:
 b=armdata.edit_bones.new(p or 'Body');b.head=positions[p];b.tail=positions[p]+Vector((0,.16,0))
 if p:b.parent=armdata.edit_bones[next(j['parent'] for j in joints if j['path']==p) or 'Body']
bpy.ops.object.mode_set(mode='OBJECT')
for o,p in parts:
 vg=o.vertex_groups.new(name=p or 'Body');vg.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Rigid articulated skin','ARMATURE');mod.object=arm
# Y-up game data -> Z-up Blender scene for source, preview and FBX.
root=bpy.data.objects.new('KryonFrostBear',None);bpy.context.collection.objects.link(root)
for o,p in parts:o.parent=root
arm.parent=root;root.rotation_euler.x=math.pi/2
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=750;scene.render.resolution_percentage=100;scene.world.color=(.3,.3,.3)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-1.18));floor=bpy.context.object;fm=bpy.data.materials.new('studio');fm.diffuse_color=(.16,.21,.25,1);floor.data.materials.append(fm)
def aim(o,point):o.rotation_euler=(Vector(point)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(5,-6,3.1));cam=bpy.context.object;aim(cam,(0,-.15,.05));cam.data.type='ORTHO';cam.data.ortho_scale=5.3;scene.camera=cam
for pos,power,size in [((3,-4,6),1000,5),((-4,-2,3),850,4),((0,4,5),1200,4)]:
 bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;aim(l,(0,0,0))
scene.view_settings.view_transform='AgX';scene.render.filepath=str(ART/'preview.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'KryonFrostBear.blend'),compress=True)
(ART/'geometry-report.json').write_text(json.dumps(dict(parts=len(parts),triangles=len(bones)//3,bones=len(paths),materials=len(colors)),indent=2))
print('KRYON_BEAR_EXPORT',len(parts),len(bones)//3)
