"""Render current production geometry in a fixed neutral studio (not in-game screenshots)."""
import bpy,json,math
from mathutils import Vector
from pathlib import Path
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'VoidKnight.blend'))
scene=bpy.context.scene;rig=next(o for o in scene.objects if o.type=='ARMATURE')
rig.animation_data.action=bpy.data.actions['Idle'];rig.data.pose_position='POSE'
for track in rig.animation_data.nla_tracks:track.mute=True
scene.frame_set(1);scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.device='CPU'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
world=bpy.data.worlds.new('Void studio');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.065,.095,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
for obj in list(scene.objects):
    if obj.type in ['CAMERA','LIGHT']:bpy.data.objects.remove(obj,do_unlink=True)
def point(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for position,power,color,size in [((3,-4,5),650,(.82,.88,1),3),((-3,-2,2.6),450,(.76,.58,1),2),((0,3,4),800,(.55,.67,1),2)]:
    data=bpy.data.lights.new('Studio','AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new('Studio',data);scene.collection.objects.link(obj);obj.location=position;point(obj,(0,0,1.1))
data=bpy.data.cameras.new('Camera');camera=bpy.data.objects.new('Camera',data);scene.collection.objects.link(camera)
camera.location=(3,-6,2.9);point(camera,(0,0,1.1));data.type='ORTHO';data.ortho_scale=2.8;scene.camera=camera
scene.render.filepath=str(root/'VoidKnight-model-preview.png');bpy.ops.render.render(write_still=True)
out=root/'Construction';out.mkdir(exist_ok=True);meshes=[m for m in scene.objects if m.type=='MESH']
(out/'parts-manifest.json').write_text(json.dumps([{'part':m.name,'bone':m['bone'],'vertices':len(m.data.vertices),'materials':[s.material.name for s in m.material_slots]} for m in meshes],indent=2)+'\n')
scene.render.resolution_x=720;scene.render.resolution_y=720;scene.cycles.samples=12
for view,position in [('front',(0,-6,2.7)),('side',(6,-1,2.7)),('back',(0,6,2.7)),('front-left',(-3,-6,2.9)),('game',(3,-5,6))]:
    camera.location=position;point(camera,(0,0,1.1));data.ortho_scale=2.8
    scene.render.filepath=str(out/('carry-'+view+'.png'));bpy.ops.render.render(write_still=True)
# Retain construction drawings of editable rest-aligned assemblies.
rig.animation_data.action=None;rig.data.pose_position='REST'
body=lambda m:m.name.startswith(('VK_Undersuit','VK_Joint','VK_Palm','VK_Finger','VK_Thumb','VK_Neck'))
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.cycles.samples=10
for group in ['body','armor','sword']:
    for m in meshes:m.hide_render=not(body(m) if group=='body' else m.get('bone')=='WeaponGripR' if group=='sword' else not body(m) and m.get('bone')!='WeaponGripR')
    bpy.context.view_layer.update();bounds=[m.matrix_world@Vector(c) for m in meshes if not m.hide_render for c in m.bound_box]
    low=Vector(tuple(min(v[i] for v in bounds) for i in range(3)));high=Vector(tuple(max(v[i] for v in bounds) for i in range(3)));center=(low+high)*.5
    for view,offset in [('front',Vector((0,-5,0))),('side',Vector((5,0,0))),('back',Vector((0,5,0)))]:
        camera.location=center+offset;point(camera,center);data.ortho_scale=max(high.z-low.z,high.x-low.x,high.y-low.y)*1.2
        scene.render.filepath=str(out/(group+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
for m in meshes:m.hide_render=False
rig.data.pose_position='POSE';camera.location=(3,-6,2.9);point(camera,(0,0,1.1));data.ortho_scale=3.0
for action,frame,label in [('Attack1',9,'basic'),('Attack2',21,'recovery'),('Skill0',9,'Q-sweep'),('Skill1',10,'W-guard'),('Skill2',7,'E-lunge'),('Ultimate',21,'R-cleave')]:
    rig.animation_data.action=bpy.data.actions[action];scene.frame_set(frame)
    scene.render.filepath=str(out/('attack-'+label+'.png'));bpy.ops.render.render(write_still=True)
print('REFERENCE_RENDERS_DONE',flush=True)
