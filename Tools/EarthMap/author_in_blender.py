"""Open the exact runtime Earth kit in Blender; export edited kit geometry back to Unity.

blender --background --python Tools/EarthMap/author_in_blender.py -- --render
blender --background ArtSource/EarthMap/EarthDressing.blend --python Tools/EarthMap/author_in_blender.py -- --export

Kit mesh parts are linked into the layout. Edit the KIT objects, keep their material index
and ground pivot, then --export. Layout objects are a preview; gameplay is never exported.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
RESOURCE=ROOT/'Assets/Alterverse/Resources/EarthMap/Dressing.json'
OUT=ROOT/'ArtSource/EarthMap'


def to_blender(v): return (v[0],-v[2],v[1])
def to_unity(v): return (v.x,v.z,-v.y)


def export_kit():
    data=json.loads(RESOURCE.read_text())
    kit=bpy.data.collections.get('KIT - edit these meshes')
    if kit is None: raise RuntimeError('Open EarthDressing.blend before exporting.')
    for asset in data['assets']:
        parts=[]
        for ob in sorted(kit.objects,key=lambda o:o.name):
            if ob.get('earth_asset')!=asset['name']: continue
            if ob.type!='MESH': raise RuntimeError('Kit parts must be mesh objects.')
            # Nonzero object transforms would move the pivot; apply them in Blender first.
            if any(abs(ob.matrix_basis[i][j]-(1 if i==j else 0))>1e-5 for i in range(4) for j in range(4)):
                raise RuntimeError('Apply object transforms before export: '+ob.name)
            evaluated=ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh=evaluated.to_mesh()
            try:
                mesh.calc_loop_triangles()
                vertices=[];uv=[];triangles=[]
                for tri in mesh.loop_triangles:
                    for index,loop_index in zip(tri.vertices,tri.loops):
                        triangles.append(len(vertices)//3)
                        vertices.extend(round(x,5) for x in to_unity(mesh.vertices[index].co))
                        tex=mesh.uv_layers.active.data[loop_index].uv if mesh.uv_layers.active else (0,0)
                        uv.extend(round(float(x),5) for x in tex)
                parts.append(dict(material=int(ob['earth_material']),vertices=vertices,triangles=triangles,uv=uv))
            finally: evaluated.to_mesh_clear()
        if not parts: raise RuntimeError('Missing kit asset: '+asset['name'])
        asset['parts']=parts
    RESOURCE.write_text(json.dumps(data,separators=(',',':'))+'\n')
    print('Exported edited mesh kit to '+str(RESOURCE))


def mesh_object(name,part,material,collection):
    coords=[to_blender(part['vertices'][i:i+3]) for i in range(0,len(part['vertices']),3)]
    faces=[part['triangles'][i:i+3] for i in range(0,len(part['triangles']),3)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(coords,[],faces);mesh.update()
    for polygon in mesh.polygons:polygon.use_smooth=True
    uv=mesh.uv_layers.new(name='ShoreDistance')
    for loop in mesh.loops: uv.data[loop.index].uv=part['uv'][loop.vertex_index*2:loop.vertex_index*2+2]
    mesh.materials.append(material)
    ob=bpy.data.objects.new(name,mesh);collection.objects.link(ob)
    return ob


def build(render=False):
    data=json.loads(RESOURCE.read_text());bpy.ops.wm.read_factory_settings(use_empty=True)
    kit=bpy.data.collections.new('KIT - edit these meshes');bpy.context.scene.collection.children.link(kit)
    layout=bpy.data.collections.new('LAYOUT - linked preview');bpy.context.scene.collection.children.link(layout)
    materials=[]
    for entry in data['materials']:
        m=bpy.data.materials.new(entry['name']);m.diffuse_color=entry['color'];m.use_nodes=True
        bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=entry['color']
        bs.inputs['Roughness'].default_value=.24 if entry['name']=='Water' else .78
        if entry['name']=='Water': bs.inputs['Metallic'].default_value=.18
        # Small-scale surface grain on masonry/timber, shared across linked objects.
        if entry['name'] not in ('Water','Window'):
            noise=m.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=18
            bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12
            bump.inputs['Distance'].default_value=.04
            m.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
        materials.append(m)
    models={}
    for asset in data['assets']:
        models[asset['name']]=[]
        for part in asset['parts']:
            ob=mesh_object('KIT_'+asset['name']+'_'+str(part['material']),part,materials[part['material']],kit)
            ob['earth_asset']=asset['name'];ob['earth_material']=part['material']
            models[asset['name']].append(ob)
    # Kit is hidden in the preview. Unhide the collection to inspect/edit its ground-pivot parts.
    kit.hide_render=True;kit.hide_viewport=True
    # Match the conservative fallback structure aprons used by SceneryBuildingClearance.
    footprints=[(s*36,0,11,11) for s in (-1,1)]+[(s*27.5,0,10,10) for s in (-1,1)]
    for side in (-1,1):
        for flank in (-1,1): footprints.extend([(side*26.25,flank*21.5,10,9),(side*25.25,flank*10.75,7,9)])
    def blocked(bounds):
        xmin,xmax,zmin,zmax=bounds
        for x,z,w,d in footprints:
            dx=max(0,xmin-x-w/2,x-w/2-xmax);dz=max(0,zmin-z-d/2,z-d/2-zmax)
            if dx*dx+dz*dz<=4: return True
        return False
    for index,site in enumerate(data['placements']):
        angle=math.radians(site['yaw']);co=math.cos(angle);si=math.sin(angle);points=[]
        for ob in models[site['asset']]:
            for v in ob.data.vertices:
                ux,_,uz=to_unity(v.co);points.append((site['x']+(ux*co+uz*si)*site['scale'],site['z']+(-ux*si+uz*co)*site['scale']))
        bounds=(min(p[0] for p in points),max(p[0] for p in points),min(p[1] for p in points),max(p[1] for p in points))
        if bounds[2]<7 and bounds[3]>-7 or blocked(bounds): continue
        for original in models[site['asset']]:
            ob=bpy.data.objects.new(f"Placed_{index}_{original.name}",original.data);layout.objects.link(ob)
            ob.location=(site['x'],-site['z'],.015);ob.rotation_euler.z=math.radians(site['yaw']);ob.scale=(site['scale'],)*3
    for surface in data['surfaces']:
        for part in surface['parts']:
            ob=mesh_object(surface['name'],part,materials[part['material']],layout)
            # Preview on flat terrain; actual runtime strips follow the existing ground.
            if not surface['water']: ob.location.z=.015
    bpy.ops.mesh.primitive_plane_add(size=2,location=(0,0,-.005));ground=bpy.context.object
    ground.name='Preview terrain only - no gameplay export';ground.scale=(42,36,1)
    grass=bpy.data.materials.new('Preview ground');grass.diffuse_color=(.24,.30,.14,1);ground.data.materials.append(grass)
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=1440;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Earth sky');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.50,.63,.76,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
    bpy.ops.object.light_add(type='AREA',location=(0,-10,40));light=bpy.context.object
    light.data.energy=15000;light.data.shape='DISK';light.data.size=35
    def camera(name,location,target,scale):
        bpy.ops.object.camera_add(location=location);cam=bpy.context.object;cam.name=name
        cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        cam.data.type='ORTHO';cam.data.ortho_scale=scale;return cam
    wide=camera('Earth layout',(50,-65,75),(0,0,0),105)
    close=camera('Village and river',(20,-52,35),(-2,-25,1),37)
    scene.camera=wide;OUT.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'EarthDressing.blend'))
    if render:
        scene.render.filepath=str(OUT/'EarthLayout.png');bpy.ops.render.render(write_still=True)
        scene.camera=close;scene.render.filepath=str(OUT/'EarthVillage.png');bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--render',action='store_true');parser.add_argument('--export',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.export: export_kit()
    else: build(args.render)
