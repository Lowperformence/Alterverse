"""Write individually editable, rest-aligned Blender armor assemblies.

Each file opens on its own, includes only that assembly and a rest-pose binding rig,
retains original fitting coordinates, and carries packed material textures.
"""
from pathlib import Path
import bpy,json
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight.blend'))
source_scene=bpy.context.scene
rig=next(o for o in source_scene.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'

def assembly(obj):
    # V3 blueprints explicitly separate hip tassets from the thigh shells.
    if obj.get('assembly'):return obj['assembly']
    bone=obj.get('bone','')
    if bone=='WeaponGripR':return 'Greatsword'
    if bone=='Head':return 'Helmet'
    if bone in ['Chest','Spine']:return 'Cuirass'
    if bone=='Hips':return 'Waist'
    if bone.startswith('UpperArm'):return 'Pauldron_'+bone[-1]
    if bone.startswith('Forearm'):return 'Vambrace_'+bone[-1]
    if bone.startswith('Hand'):return 'Gauntlet_'+bone[-1]
    if obj.name.startswith(('VK2_Hip_','VK2_HipSeam_','VK3_Tasset_')):return 'Tassets'
    if bone.startswith('Thigh'):return 'Cuisse_'+bone[-1]
    if bone.startswith('Shin'):return 'Greave_'+bone[-1]
    if bone.startswith('Foot'):return 'Sabaton_'+bone[-1]
    raise ValueError((obj.name,bone))
groups={}
for obj in list(source_scene.objects):
    if obj.type=='MESH':groups.setdefault(assembly(obj),[]).append(obj)
# Organize the main source too; no hierarchy or binding change is required.
for name,parts in groups.items():
    collection=bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if collection.name not in source_scene.collection.children:source_scene.collection.children.link(collection)
    for obj in parts:
        for old in list(obj.users_collection):old.objects.unlink(obj)
        collection.objects.link(obj)
module_dir=HERE/'Modules';module_dir.mkdir(exist_ok=True)
rig.data.pose_position='POSE';rig.animation_data.action=bpy.data.actions['Idle'];source_scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight.blend'),compress=True)
manifest=[{'file':name+'.blend','parts':[o.name for o in parts],'binding':'original rest skeleton','packed_textures':True} for name,parts in sorted(groups.items())]
# A library-only file opens on an empty default scene. Save real scene files instead,
# appending only the chosen meshes and their binding rig into a clean factory scene.
for item in manifest:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(HERE/'VoidKnight.blend'),link=False) as (available,loaded):
        loaded.objects=list(item['parts'])
    objects=list(loaded.objects)
    copy_rig=objects[0].parent
    assert copy_rig is not None and copy_rig.type=='ARMATURE'
    scene=bpy.context.scene;scene.name=Path(item['file']).stem
    scene.collection.objects.link(copy_rig)
    copy_rig.animation_data_clear();copy_rig.data.pose_position='REST'
    for bone in copy_rig.pose.bones:bone.matrix_basis.identity()
    for obj in objects:scene.collection.objects.link(obj)
    for action in list(bpy.data.actions):bpy.data.actions.remove(action,do_unlink=True)
    scene['assembly']=scene.name;scene['fitting_coordinates']='Original 19-bone rest rig; append at world origin'
    bpy.context.view_layer.update()
    from mathutils import Vector
    bounds=[obj.matrix_world@Vector(c) for obj in objects for c in obj.bound_box]
    low=Vector(tuple(min(v[i] for v in bounds) for i in range(3)))
    high=Vector(tuple(max(v[i] for v in bounds) for i in range(3)))
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                region=area.spaces.active.region_3d
                region.view_location=(low+high)*.5
                region.view_distance=max((high-low).length*1.7,.25)
                region.view_rotation=Vector((.6,-1,.5)).to_track_quat('Z','Y')
                area.spaces.active.shading.type='MATERIAL'
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(module_dir/item['file']),compress=True)
(module_dir/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight.blend'))
print('MODULES_EXPORTED',len(manifest),flush=True)

