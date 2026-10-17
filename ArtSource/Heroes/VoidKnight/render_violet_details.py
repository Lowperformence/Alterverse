"""Actual geometry close-ups and unobstructed armor turnarounds."""
from pathlib import Path
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight.blend'))
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.device='CPU'
scene.render.resolution_x=1000;scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
world=bpy.data.worlds.new('Violet armor studio');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.035,.041,.055,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
def aim(obj,center):obj.rotation_euler=(Vector(center)-obj.location).to_track_quat('-Z','Y').to_euler()
for obj in list(scene.objects):
    if obj.type in ['CAMERA','LIGHT']:bpy.data.objects.remove(obj,do_unlink=True)
for position,power,color,size in [((2,-4,4),600,(.85,.90,1),3),((-3,-1,3),350,(.83,.73,1),2),((1,3,4),800,(.65,.77,1),2)]:
    data=bpy.data.lights.new('Softbox','AREA');data.energy=power;data.color=color;data.size=size
    obj=bpy.data.objects.new('Softbox',data);scene.collection.objects.link(obj);obj.location=position;aim(obj,(0,0,1.3))
data=bpy.data.cameras.new('Detail camera');camera=bpy.data.objects.new('Detail camera',data)
scene.collection.objects.link(camera);scene.camera=camera;data.type='ORTHO'
out=HERE/'Construction';meshes=[o for o in scene.objects if o.type=='MESH']
views=[('violet-helmet-front',(0,-5,.1),(0,0,1.745),.43,'helmet'),
       ('violet-helmet-angle',(3,-5,1.3),(0,0,1.745),.48,'helmet'),
       ('violet-armor-front',(0,-6,1),(0,0,1),2.05,'armor'),
       ('violet-armor-angle',(3,-6,1.7),(0,0,1),2.10,'armor'),
       ('violet-armor-back',(-2,6,1.6),(0,0,1),2.10,'armor')]
for name,offset,center,scale,kind in views:
    for obj in meshes:obj.hide_render=(obj.get('bone')!='Head') if kind=='helmet' else obj.get('bone')=='WeaponGripR'
    camera.location=Vector(center)+Vector(offset);aim(camera,center);data.ortho_scale=scale
    scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
print('VIOLET_DETAILS_RENDERED',flush=True)
