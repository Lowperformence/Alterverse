"""Native exporter; no external models, textures, rigs or animations are loaded."""
import bpy, bmesh, json, math, hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];O=R/'ArtSource/Heroes/ZhaevOriginal'
s=json.loads((O/'parts.json').read_text());bpy.ops.wm.read_factory_settings(use_empty=True)
mats={}
for name,c in s['colors'].items():
 rgb=tuple(int(c[i:i+2],16)/255 for i in (1,3,5));m=bpy.data.materials.new(name+' '+('Energy' if name=='energy' else 'Metal' if name in ['metal','edge'] else 'Fabric' if name=='fabric' else 'Surface'));m.diffuse_color=(*rgb,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Metallic'].default_value=.65 if name in ['metal','edge'] else 0;p.inputs['Roughness'].default_value=.3 if name in ['metal','edge'] else .65
 if name=='energy':p.inputs['Emission Color'].default_value=(*rgb,1);p.inputs['Emission Strength'].default_value=2
 mats[name]=m
ad=bpy.data.armatures.new('DF_Skeleton');arm=bpy.data.objects.new('DF_Skeleton',ad);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def coord(v):return (v[0],-v[1],v[2])
for name,h,t,parent in s['bones']:
 b=ad.edit_bones.new(name);b.head=coord(h);b.tail=coord(t)
 if parent:b.parent=ad.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');arm.select_set(False)
objects=[]
for part in s['parts']:
 mesh=bpy.data.meshes.new(part['id']);mesh.from_pydata([coord(v) for v in part['vertices']],[],part['faces']);mesh.update()
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 obj=bpy.data.objects.new(part['id']+' '+part['name'],mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(mats[part['material']]);obj['part_id']=part['id'];obj['category']=part['category'];obj['source_revision']=s['revision'];obj['bone']=part['bone']
 group=obj.vertex_groups.new(name=part['bone']);group.add(list(range(len(mesh.vertices))),1,'REPLACE');mod=obj.modifiers.new('DF_Skin','ARMATURE');mod.object=arm;obj.parent=arm;obj.hide_render=part['category']=='head';objects.append(obj)
 for poly in mesh.polygons:poly.use_smooth=part['name'].startswith(('Original head','Upper sleeve','Lower sleeve','Trouser','Neck','Glove','Waist','Rib','Pelvic','Boot shell'))
# Own animation keys; named runtime contract. No imported mocap/CC0 template.
for name,end in [('Idle',61),('Run',25),('Attack',25),('Skill0',31),('Skill1',34),('Skill2',37),('Ultimate',49),('Hit',16),('Death',40)]:
 arm.animation_data_create();action=bpy.data.actions.new(name);arm.animation_data.action=action;action.use_fake_user=True
 for f in range(1,end+1,3):
  t=(f-1)/(end-1)
  for bone in arm.pose.bones:
   bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
   if name=='Run' and bone.name.startswith(('Thigh','UpperArm')):bone.rotation_euler.x=(.32 if bone.name.startswith('Thigh') else -.18)*math.sin(t*2*math.pi)*(1 if bone.name.endswith('L') else -1)
   if name=='Idle' and bone.name=='Chest':bone.rotation_euler.x=.012*math.sin(t*2*math.pi)
   if name in ['Attack','Skill0','Skill1','Skill2','Ultimate'] and bone.name.startswith('UpperArm'):bone.rotation_euler.x=-.7*math.sin(t*math.pi);bone.rotation_euler.z=.25*math.sin(t*math.pi)*(1 if bone.name.endswith('L') else -1)
   if name=='Hit' and bone.name=='Chest':bone.rotation_euler.x=.16*math.sin(t*math.pi)
   if name=='Death' and bone.name=='Hips':bone.rotation_euler.x=1.4*t
   bone.keyframe_insert('rotation_euler',frame=f,group=bone.name);bone.keyframe_insert('location',frame=f,group=bone.name)
arm.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1);bpy.context.scene.render.fps=30
marker=bpy.data.objects.new('DF_ZhaevOriginalV1',None);bpy.context.collection.objects.link(marker);marker.parent=arm
# Save editable source BEFORE temporary mesh joins used for runtime export.
bpy.ops.wm.save_as_mainfile(filepath=str(O/'Zhaev.blend'),compress=True)
heads=[o for o in objects if o.get('category')=='head']
objects=[o for o in objects if o.get('category')!='head']
for obj in heads:bpy.data.objects.remove(obj,do_unlink=True)
body_parts=[o for o in objects if o.get('category')!='weapon']
weapon_parts={k:[o for o in objects if o.get('category')=='weapon' and o.get('bone')=='WeaponGrip'+k] for k in ['L','R']}
def join(parts,name):
 bpy.ops.object.select_all(action='DESELECT')
 for ob in parts:ob.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();parts[0].name=name;return parts[0]
body=join(body_parts,'DF_Anatomy')
for k in ['L','R']:join(weapon_parts[k],'DF_Weapon'+k)
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(O/'Veyr.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_UNITS')
# FBX reimport validation independent of source scene.
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(O/'Veyr.fbx'))
arms=[o for o in bpy.data.objects if o.type=='ARMATURE'];meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(arms)==1;assert len(arms[0].data.bones)==19;assert {o.name for o in meshes}=={'DF_Anatomy','DF_WeaponL','DF_WeaponR'}
for ob in meshes:
 for v in ob.data.vertices:assert v.groups and abs(sum(g.weight for g in v.groups)-1)<1e-5
names={a.name.split('|')[-1] for a in bpy.data.actions};assert {'Idle','Run','Attack','Skill0','Skill1','Skill2','Ultimate','Hit','Death'}<=names,names
report={'Blender':bpy.app.version_string,'fbxReimport':True,'bones':19,'meshes':[o.name for o in meshes],'clips':sorted(names),'weightsNormalized':True,'UnityAndroid':'not executed','status':'native model validated; engine/device acceptance pending'}
(O/'native-validation.json').write_text(json.dumps(report,indent=2))
# Native render of actual geometry, rather than a separately imagined illustration.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.device='CPU';scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new('Studio World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.18,.18,1)
from mathutils import Vector
for pos,power,size in [((3,-4,5),1300,4),((-3,-2,3),900,3),((0,3,4),1400,3)]:
 d=bpy.data.lights.new('Studio','AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new('Studio',d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdata);scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=2.35
for name,pos in [('front',(2,-5,2.4)),('back',(-2,5,2.4))]:
 cam.location=pos;cam.rotation_euler=(Vector((0,0,.9))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
print(json.dumps(report))
