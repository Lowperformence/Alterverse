"""Render the Blender authoring materials for visual inspection, not Unity gameplay.
Run: blender -b --python Tools/MaterialFinishes/render_review.py
"""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/MaterialFinishes/GameFinishes.blend'))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(.045,.055,.075)
base=bpy.data.materials.new('ReviewBackdrop');base.diffuse_color=(.018,.025,.038,1);base.use_nodes=True
base.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=base.diffuse_color
base.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.7
bpy.ops.mesh.primitive_plane_add(size=200);bpy.context.object.data.materials.append(base)
label=bpy.data.materials.new('ReviewLabels');label.diffuse_color=(.72,.78,.85,1)
for i,name in enumerate(('Metal','Stone','Concrete','Soil','Sand','Bark','Leaf','Ice','Fabric')):
    x=(i%3-1)*2.3;y=(1-i//3)*2.3
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=24,radius=.76,location=(x,y,.77))
    obj=bpy.context.object;material=bpy.data.materials['DF_Baked_'+name]
    for node in material.node_tree.nodes:
        if node.type=='BUMP':node.inputs['Distance'].default_value=.06
    obj.data.materials.append(material)
    for p in obj.data.polygons:p.use_smooth=True
    bpy.ops.object.text_add(location=(x,y-1.15,.025))
    text=bpy.context.object;text.data.body=name.upper();text.data.align_x='CENTER';text.data.size=.19;text.data.materials.append(label)
for pos,power,size in [((-4,-3,8),1200,5),((5,1,5),850,4),((-1,5,4),1000,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,.5))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-4,18));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.1))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=8.4;scene.camera=camera
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB';scene.render.image_settings.color_depth='8';scene.render.image_settings.compression=90
scene.render.filepath=str(ROOT/'ArtSource/MaterialFinishes/authoring-review.png')
bpy.ops.render.render(write_still=True)
print('AUTHORING_REVIEW_RENDERED — Blender material samples; not a Unity screenshot.')
