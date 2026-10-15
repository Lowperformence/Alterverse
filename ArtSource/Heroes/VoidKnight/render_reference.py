import bpy,json
from mathutils import Vector
from pathlib import Path
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'VoidKnight.blend'))
scene=bpy.context.scene;rig=next(o for o in scene.objects if o.type=='ARMATURE')
rig.animation_data.action=bpy.data.actions['Idle']
for track in rig.animation_data.nla_tracks:track.mute=True
scene.frame_set(1);scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.device='CPU'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
world=bpy.data.worlds.new('Void studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.028,.018,.05,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;scene.world=world

def point(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for p,energy,color,size in [((3,-4,5),500,(.75,.8,1),4),((-3,-1,3),400,(.55,.28,1),3),((0,4,4),650,(.8,.6,1),3)]:
 d=bpy.data.lights.new('Studio','AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new('Studio',d);scene.collection.objects.link(o);o.location=p;point(o,(0,0,1))
d=bpy.data.cameras.new('Camera');o=bpy.data.objects.new('Camera',d);scene.collection.objects.link(o);o.location=(3,-5,2.7);point(o,(0,0,1));d.type='ORTHO';d.ortho_scale=2.8;scene.camera=o
scene.render.filepath=str(root/'VoidKnight-model-preview.png');bpy.ops.render.render(write_still=True)
out=root/'Construction';out.mkdir(exist_ok=True);meshes=[m for m in scene.objects if m.type=='MESH']
(out/'parts-manifest.json').write_text(json.dumps([{'part':m.name,'bone':m['bone'],'vertices':len(m.data.vertices),'materials':[s.material.name for s in m.material_slots]} for m in meshes],indent=2)+'\n')
scene.render.resolution_x=700;scene.render.resolution_y=700;scene.cycles.samples=8;rig.animation_data.action=None;rig.data.pose_position='REST'
body=lambda m:m.name.startswith(('VK_Undersuit','VK_Joint','VK_Palm','VK_Finger','VK_Thumb','VK_Neck'))
for group in ['body','armor','sword']:
 for m in meshes:m.hide_render=not (body(m) if group=='body' else m.get('bone')=='WeaponGripR' if group=='sword' else not body(m) and m.get('bone')!='WeaponGripR')
 visible=[m for m in meshes if not m.hide_render];bpy.context.view_layer.update();bounds=[m.matrix_world@Vector(c) for m in visible for c in m.bound_box]
 low=Vector(tuple(min(v[i] for v in bounds) for i in range(3)));high=Vector(tuple(max(v[i] for v in bounds) for i in range(3)));center=(low+high)*.5
 for view,offset in [('front',Vector((0,-5,0))),('side',Vector((5,0,0))),('back',Vector((0,5,0)))]:
  scene.camera.location=center+offset;point(scene.camera,center);scene.camera.data.ortho_scale=max(high.z-low.z,high.x-low.x,high.y-low.y)*1.2;scene.render.filepath=str(out/(group+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
for m in meshes:m.hide_render=False
rig.data.pose_position='POSE';scene.camera.location=(3,-5,2.7);point(scene.camera,(0,0,1));scene.camera.data.ortho_scale=3.2
for action,frame,label in [('Attack1',9,'basic'),('Skill0',9,'Q-sweep'),('Skill1',10,'W-guard'),('Skill2',7,'E-lunge'),('Ultimate',21,'R-cleave')]:
 rig.animation_data.action=bpy.data.actions[action];scene.frame_set(frame);scene.render.filepath=str(out/('attack-'+label+'.png'));bpy.ops.render.render(write_still=True)
