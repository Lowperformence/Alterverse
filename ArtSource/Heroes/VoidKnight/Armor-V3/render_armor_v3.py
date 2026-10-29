"""Render actual saved geometry in a neutral studio; never saves the source scene.

These renders are Blender validation views, not Unity or Android screenshots.
"""
from pathlib import Path
import bpy, sys, argparse
from mathutils import Vector

HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser()
p.add_argument('--source',type=Path,default=HERE.parent/'VoidKnight.blend')
p.add_argument('--out',type=Path,default=HERE/'Renders')
p.add_argument('--size',type=int,default=1000)
p.add_argument('--poses-only',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(args.source))
scene=bpy.context.scene;rig=next(o for o in scene.objects if o.type=='ARMATURE')
rig.data.pose_position='REST';rig.animation_data.action=None
for t in rig.animation_data.nla_tracks:t.mute=True
scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True;scene.cycles.device='CPU'
scene.render.resolution_x=args.size;scene.render.resolution_y=args.size;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
world=bpy.data.worlds.new('V3 neutral studio');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.13,.15,.19,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
for obj in list(scene.objects):
    if obj.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(obj,do_unlink=True)
def aim(o,c):o.rotation_euler=(Vector(c)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size,color in [((2,-4,4),600,3,(.88,.93,1)),((-3,-2,2.5),350,2,(.82,.77,1)),((1,3,4),850,2,(.7,.8,1))]:
    d=bpy.data.lights.new('V3 softbox','AREA');d.energy=power;d.size=size;d.color=color
    o=bpy.data.objects.new(d.name,d);scene.collection.objects.link(o);o.location=pos;aim(o,(0,0,1))
d=bpy.data.cameras.new('V3 review camera');camera=bpy.data.objects.new(d.name,d)
scene.collection.objects.link(camera);scene.camera=camera;d.type='ORTHO'
args.out.mkdir(parents=True,exist_ok=True)
views=[('armor-front',(0,-6,1.0),(0,0,.97),2.06,False),
       ('armor-angle',(3,-6,2.5),(0,0,.98),2.1,False),
       ('armor-back',(0,6,1),(0,0,.97),2.06,False),
       ('helmet-angle',(2.4,-5,2.7),(0,-.02,1.744),.47,True),
       ('helmet-front',(0,-6,1.744),(0,-.02,1.744),.43,True)]
meshes=[o for o in scene.objects if o.type=='MESH']
for name,position,target,scale,head in views:
    if args.poses_only:continue
    for o in meshes:o.hide_render=o.get('bone')=='WeaponGripR' or (head and o.get('bone')!='Head')
    camera.location=position;aim(camera,target);d.ortho_scale=scale
    scene.render.filepath=str(args.out/(name+'.png'));bpy.ops.render.render(write_still=True)
rig.data.pose_position='POSE'
for o in meshes:o.hide_render=False
for label,action,frame in [('carry','Idle',1),('attack','Attack1',9),('guard','Skill1',10),('ultimate','Ultimate',21)]:
    rig.animation_data.action=bpy.data.actions[action];scene.frame_set(frame)
    camera.location=(3,-6,2.9);aim(camera,(0,0,1.13));d.ortho_scale=2.95
    scene.render.filepath=str(args.out/(label+'.png'));bpy.ops.render.render(write_still=True)
print('V3_STUDIO_AND_CONTACT_RENDERS_COMPLETE',flush=True)
