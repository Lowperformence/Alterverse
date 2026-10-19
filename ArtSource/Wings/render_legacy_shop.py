"""Render actual legacy wing meshes captured by LegacyIconExport.cs.
Blender background script; writes four shop thumbnails and full inspection renders.
"""
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
DATA=json.loads((HERE/'LegacyShopGeometry.json').read_text())

def vector(v):return (v[0],v[2],v[1])

def material(definition,cache):
    name=definition['name']
    if name in cache:return cache[name]
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*definition['color'],1)
    p.inputs['Metallic'].default_value=definition['metallic']
    p.inputs['Roughness'].default_value=1-definition['smoothness']
    p.inputs['Emission Color'].default_value=(*definition['emission'],1)
    p.inputs['Emission Strength'].default_value=1
    cache[name]=m
    return m

def node(data,scene,parent,materials):
    if data['vertices']:
        faces=[];slots=[]
        for slot,indices in enumerate(data['triangles']):
            for i in range(0,len(indices),3):
                faces.append(indices[i:i+3][::-1]);slots.append(slot)
        mesh=bpy.data.meshes.new(data['name']);mesh.from_pydata([vector(v) for v in data['vertices']],[],faces);mesh.update()
        obj=bpy.data.objects.new(data['name'],mesh);scene.collection.objects.link(obj)
        for m in data['materials']:mesh.materials.append(material(m,materials))
        for face,slot in zip(mesh.polygons,slots):face.material_index=slot
    elif data['line']:
        line=data['line'];curve=bpy.data.curves.new(data['name'],'CURVE');curve.dimensions='3D'
        curve.resolution_u=1;curve.bevel_depth=line['width']*.5;curve.bevel_resolution=2
        spline=curve.splines.new('POLY');spline.points.add(len(line['points'])-1)
        for p,v in zip(spline.points,line['points']):p.co=(*vector(v),1)
        curve.materials.append(material(line['material'],materials))
        obj=bpy.data.objects.new(data['name'],curve);scene.collection.objects.link(obj)
    elif data['sphere']:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.5)
        obj=bpy.context.object;obj.name=data['name'];obj.data.materials.append(material(data['materials'][0],materials))
    else:
        obj=bpy.data.objects.new(data['name'],None);scene.collection.objects.link(obj)
    obj.parent=parent;obj.location=vector(data['position']);obj.scale=vector(data['scale'])
    for child in data['children']:node(child,scene,obj,materials)

def render(key,nodes):
    old=bpy.data.scenes.get('ShopIcon_'+key)
    if old:bpy.data.scenes.remove(old)
    scene=bpy.data.scenes.new('ShopIcon_'+key);bpy.context.window.scene=scene
    scene.world=bpy.data.worlds.new(key+'_World');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.035,.05,.08,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
    materials={}
    for item in nodes:node(item,scene,None,materials)
    bpy.context.view_layer.update()
    points=[obj.matrix_world@Vector(c) for obj in scene.objects if obj.type in ['MESH','CURVE'] for c in obj.bound_box]
    lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)))
    center=(lo+hi)*.5;size=max(hi.x-lo.x,(hi.z-lo.z)*1.6)*1.20
    camera_data=bpy.data.cameras.new(key+'_Camera');camera=bpy.data.objects.new(key+'_Camera',camera_data);scene.collection.objects.link(camera)
    # Deluxe's sparse cyan facets are on its +Z Unity face; show that face.
    facing=-1 if key=='Deluxe' else 1
    camera.location=center+Vector((.12*size,-7*facing,.35*size))
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=size;scene.camera=camera
    for name,loc,power,area in [('Key',(-2,-4,4),950,5),('Rim',(3,2,2),1200,3),('Fill',(1,-5,-1),200,4)]:
        light_data=bpy.data.lights.new(key+name,'AREA');light_data.energy=power;light_data.shape='DISK';light_data.size=area
        light=bpy.data.objects.new(key+name,light_data);scene.collection.objects.link(light);light.location=center+Vector((loc[0],loc[1]*facing,loc[2]))
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='BLENDER_EEVEE';scene.eevee.use_bloom=True;scene.eevee.bloom_intensity=.12
    scene.view_settings.view_transform='AgX'
    scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.render.filepath=str(HERE/(key+'_ShopPreview.png'));bpy.ops.render.render(write_still=True)
    image=bpy.data.images.load(scene.render.filepath,check_existing=False)
    image.scale(384,240);image.filepath_raw=str(ROOT/'Assets/Alterverse/Resources/Wings'/(key+'Icon.png'))
    image.file_format='PNG';image.save();bpy.data.images.remove(image)
    # Keep a native, editable studio scene for future production-mesh recaptures.
    return scene

only=sys.argv[sys.argv.index('--only')+1] if '--only' in sys.argv else None
for key,nodes in DATA.items():
    if only is None or key==only:render(key,nodes)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'LegacyShopPreviews.blend'),compress=True)
