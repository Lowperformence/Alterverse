"""Blender: author a small smooth halo and export its actual mesh for Unity."""
import bpy, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_torus_add(major_segments=64, minor_segments=8, major_radius=1.0, minor_radius=.065)
ring = bpy.context.object
ring.name = 'Tahla_Emerald_Halo'
for polygon in ring.data.polygons:
    polygon.use_smooth = True
material = bpy.data.materials.new('Tahla Emerald Light')
material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (.10, 1.0, .32, 1)
shader.inputs['Emission Color'].default_value = (.10, 1.0, .32, 1)
shader.inputs['Emission Strength'].default_value = 3.0
ring.data.materials.append(material)
ring.data.calc_loop_triangles()
# Swap Blender Z-up to Unity Y-up and reverse winding for the reflected basis.
data = {'vertices': [{'x':v.co.x,'y':v.co.z,'z':v.co.y} for v in ring.data.vertices],
        'triangles':[i for t in ring.data.loop_triangles for i in (t.vertices[0],t.vertices[2],t.vertices[1])]}
out = ROOT/'Assets/Alterverse/Resources/HeroDetails'
out.mkdir(parents=True,exist_ok=True)
(out/'TahlaHalo.json').write_text(json.dumps(data,separators=(',',':'))+'\n')
source = ROOT/'ArtSource/Heroes/TahlaHalo.blend'
source.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
assert len(data['vertices']) == 512 and len(data['triangles']) == 3072
print('HALO_VALIDATED: 512 vertices, 1024 triangles, closed torus')
