import bpy,math
from pathlib import Path
from mathutils import Vector,Quaternion
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight-Riftkeeper.blend'))
for obj in list(bpy.data.objects):
 if obj.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(obj,do_unlink=True)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=40;s.cycles.use_denoising=True
s.render.resolution_x=1400;s.render.resolution_y=1700;s.render.resolution_percentage=100;s.render.film_transparent=True
s.world=bpy.data.worlds.new('Neutral studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.18,.26,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.35
s.view_settings.view_transform='AgX'
def area(name,pos,power,color,size,target=(0,0,.55)):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Key softbox',(1,-3,2.7),500,(.72,.82,1),3)
area('Silver strip',(-1.3,-1.1,.9),220,(1,.92,.8),1.2)
area('Violet rim',(1,1,1.7),240,(.42,.12,1),2)
area('Lower fill',(-.5,-2,-.5),110,(.60,.69,1),1)
d=bpy.data.cameras.new('Sword camera');cam=bpy.data.objects.new('Sword camera',d);s.collection.objects.link(cam);s.camera=cam;d.type='ORTHO'
def render(name,pos,target,scale,w=1400,h=1700):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale;s.render.resolution_x=w;s.render.resolution_y=h;s.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True)
render('front',(0,-5,.56),(0,0,.56),1.89)
render('side',(5,0,.56),(0,0,.56),1.89,700,1700)
render('beauty',(2.1,-5,2.0),(0,0,.56),1.98)
render('hilt-detail',(.8,-4,1.0),(0,0,.05),.65,1400,1400)
# Default open scene uses the full sword, separate editable source components.
cam.location=(2.1,-5,2.0);cam.rotation_euler=(Vector((0,0,.56))-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=1.98
for screen in bpy.data.screens:
 for a in screen.areas:
  if a.type=='VIEW_3D':
   a.spaces.active.region_3d.view_distance=2.3;a.spaces.active.region_3d.view_location=(0,0,.55);a.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2);a.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight-Riftkeeper.blend'),compress=True)
