"""Validate native Blender geometry, canonical Unity JSON and FBX/GLB round trips.
blender --background --python validate_ufo_hat.py -- /absolute/export/folder
"""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

folder=Path(sys.argv[sys.argv.index('--')+1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(folder/'UFO-Testhut.blend'))
hat=bpy.data.objects['DF_UFO_TEST_HAT']
expected=tuple(hat.dimensions)
assert len(hat.data.polygons)==4984 and len(hat.data.materials)==6
assert len(hat.data.vertices)==2579 and hat.location.length<.00001
assert not hat.modifiers and not hat.animation_data
data=json.loads((folder/'UfoTestHat.json').read_text())
assert len(data['vertices'])==len(hat.data.vertices)*3
assert len(data['triangles'])==4984*3 and len(data['submeshes'])==4984
assert all(math.isfinite(x) and abs(x)<10 for x in data['vertices'])
assert all(0<=i<2579 for i in data['triangles'])
assert all(0<=i<6 for i in data['submeshes'])
for i,v in enumerate(hat.data.vertices):
    canonical=(v.co.x/.145,v.co.z/.145,-v.co.y/.145)
    assert max(abs(a-b) for a,b in zip(canonical,data['vertices'][i*3:i*3+3]))<.000001
for suffix in ['fbx','glb']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    if suffix=='fbx':bpy.ops.import_scene.fbx(filepath=str(folder/'UFO-Testhut.fbx'))
    else:bpy.ops.import_scene.gltf(filepath=str(folder/'UFO-Testhut.glb'))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert meshes and not any(o.type in {'CAMERA','LIGHT','ARMATURE'} for o in bpy.context.scene.objects)
    coords=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
    dimensions=[max(c[i] for c in coords)-min(c[i] for c in coords) for i in range(3)]
    assert max(abs(a-b) for a,b in zip(expected,dimensions))<.0001,(suffix,dimensions,expected)
    triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
    assert triangles==4984,(suffix,triangles)
    print('PASS',suffix,'round trip:',triangles,'triangles; units and axes retained.',flush=True)

# The icon is a real Blender render of the same geometry.
bpy.ops.wm.open_mainfile(filepath=str(folder/'UFO-Testhut.blend'))
scene=bpy.context.scene
bpy.data.objects['STUDIO_FLOOR'].hide_render=True
scene.render.film_transparent=True
scene.render.resolution_x=scene.render.resolution_y=256
scene.cycles.samples=24
scene.render.filepath=str(folder/'UfoTestHatIcon.png')
bpy.ops.render.render(write_still=True)
print('PASS native mesh, Unity JSON, head-seat pivot and two export round trips.',flush=True)
