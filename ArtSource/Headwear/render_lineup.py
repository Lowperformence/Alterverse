"""Render a labeled review sheet of the five exact model thumbnails. Run in Blender."""
import argparse,sys
from pathlib import Path
import bpy
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
scene.render.resolution_x=1560;scene.render.resolution_y=1040;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='Standard';scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.012,.019,.033,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=1
def emission(name,color):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear()
    out=n.new('ShaderNodeOutputMaterial');e=n.new('ShaderNodeEmission');e.inputs[0].default_value=(*color,1)
    m.node_tree.links.new(e.outputs[0],out.inputs['Surface']);return m
white=emission('White',(.87,.91,.97));muted=emission('Muted',(.39,.54,.67))
def text(body,x,y,size,material):
    data=bpy.data.curves.new(body,'FONT');data.body=body;data.align_x='CENTER';data.size=size
    ob=bpy.data.objects.new(body,data);bpy.context.collection.objects.link(ob);ob.location=(x,y,.02);ob.data.materials.append(material)
text('DIMENSIONS FRONT',0,5.05,.30,muted)
text('HEADWEAR COLLECTION',0,4.52,.46,white)
items=[('BetaCap','BETA TRUCKER',-5,1.8),('UfoTestHat','UFO TEST HAT',0,1.8),
       ('WizardHat','ZAUBERERHUT',5,1.8),('PropellerCap','PROPELLER-CAP',-2.6,-2.7),
       ('MushroomHat','PILZHUT',2.6,-2.7)]
for name,label,x,y in items:
    path=root/'Assets/Alterverse/Resources/Headwear'/(name+'Icon.png')
    image=bpy.data.images.load(str(path));m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear()
    tex=n.new('ShaderNodeTexImage');tex.image=image;e=n.new('ShaderNodeEmission')
    clear=n.new('ShaderNodeBsdfTransparent');mix=n.new('ShaderNodeMixShader');out=n.new('ShaderNodeOutputMaterial')
    m.node_tree.links.new(tex.outputs['Color'],e.inputs[0]);m.node_tree.links.new(tex.outputs['Alpha'],mix.inputs[0])
    m.node_tree.links.new(clear.outputs[0],mix.inputs[1]);m.node_tree.links.new(e.outputs[0],mix.inputs[2])
    m.node_tree.links.new(mix.outputs[0],out.inputs['Surface'])
    bpy.ops.mesh.primitive_plane_add(size=2,location=(x,y,0));ob=bpy.context.object
    width=min(4.6,3.6*image.size[0]/image.size[1]);height=width*image.size[1]/image.size[0]
    ob.scale=(width/2,height/2,1);ob.data.materials.append(m)
    text(label,x,y-2.03,.24,white)
    text('TESTMODELL' if name in ('WizardHat','PropellerCap','MushroomHat') else 'BESTEHENDER HUT',x,y-2.31,.14,muted)
camera_data=bpy.data.cameras.new('Review');camera=bpy.data.objects.new('Review',camera_data);bpy.context.collection.objects.link(camera)
camera.location=(0,0,10);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=16.5;scene.camera=camera
scene.render.filepath=str(root/'ArtSource/Headwear/Headwear-Lineup.png');bpy.ops.render.render(write_still=True)

