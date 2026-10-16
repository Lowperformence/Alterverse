"""Export the assembled Blender armor onto the existing imported Unity skeleton.
Blender -b --python export_runtime.py (run after build_armor.py).
"""
import bpy, gzip, hashlib, json, math
import sys
from pathlib import Path
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/BrakkArmor'
RES = ROOT / 'Assets/Alterverse/Resources/BrakkArmor'
bpy.ops.wm.open_mainfile(filepath=str(ART / 'BrakkArmor-concept.blend'))
arm = bpy.data.objects['DF_Skeleton']
arm.data.pose_position = 'REST'
C = Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
objects = sorted((o for o in bpy.data.objects if o.type == 'MESH' and
                  o.name.startswith('BRK_') and o.get('assembly_group') != 'BELT'), key=lambda o:o.name)
assert len(objects) == 126
groups = {o['assembly_group'] for o in objects}
assert len(groups) == 18
boneNames = sorted({o['rig_bone'] for o in objects})
vertices, normals, uv, boneIndices, parts = [], [], [], [], []
submeshes = [{'triangles':[]} for _ in range(5)]
for ob in objects:
    mesh = ob.data
    mesh.calc_loop_triangles()
    cache = {}
    first = len(vertices)//3
    assert ob['rig_bone'] in arm.data.bones
    assert all(len(v.groups) == 1 and v.groups[0].weight == 1 and
               ob.vertex_groups[v.groups[0].group].name == ob['rig_bone'] for v in mesh.vertices)
    for tri in mesh.loop_triangles:
        pp = [C @ mesh.vertices[i].co for i in tri.vertices]
        if (pp[1]-pp[0]).cross(pp[2]-pp[0]).length < 2e-11:
            continue
        normal = C.to_3x3() @ mesh.polygons[tri.polygon_index].normal
        indices = []
        for i in tri.vertices:
            p = C @ mesh.vertices[i].co
            key = tuple(round(x,8) for x in (*p,*normal))
            if key not in cache:
                cache[key] = len(vertices)//3
                vertices.extend(round(x,8) for x in p)
                normals.extend(round(x,8) for x in normal)
                uv.extend([round(p.x*4,6),round(p.y*4,6)])
                boneIndices.append(boneNames.index(ob['rig_bone']))
            indices.append(cache[key])
        submeshes[ob['material_slot']]['triangles'].extend(indices)
    parts.append({'id':ob['part_id'],'bone':ob['rig_bone'],'material':ob['material_slot'],
                  'firstVertex':first,'vertexCount':len(vertices)//3-first})
hips = C @ (arm.matrix_world @ arm.data.bones['Hips'].matrix_local).translation
spine = C @ (arm.matrix_world @ arm.data.bones['Spine'].matrix_local).translation
belt = json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/BrakkUtilityBelt/Model.bytes').read_bytes()))
data = {'version':1,'revision':'brakk-armor-runtime-20261013','plateCount':18,'solidCount':126,
        'vertices':vertices,'normals':normals,'uv':uv,'pivot':list(hips),
        'referenceRigSpan':(spine-hips).length,'boneNames':boneNames,'boneIndices':boneIndices,
        'submeshes':submeshes,'materials':belt['materials'],'parts':parts}
# Do not position a whole plate using the maximum of broad body triangles.
# Curved anatomy needs a contour fit, including the mounting and shell layers.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from fit_surface import conform
catalogue = json.loads((ART/'catalogue.json').read_text())
conform(data, catalogue['plates'], bpy.data.objects['DF_Anatomy'])
assert len(data['boneIndices']) == len(data['vertices'])//3
assert all(math.isfinite(x) for key in ['vertices','normals','uv'] for x in data[key])
assert all(0 <= i < len(data['vertices'])//3 for s in data['submeshes'] for i in s['triangles'])
RES.mkdir(parents=True,exist_ok=True)
resource = RES/'Model.bytes'
resource.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))
catalogue = json.loads((ART/'catalogue.json').read_text())
catalogue['armorIntegratedIntoGame'] = True
catalogue['armorRuntimeResource'] = 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes'
(ART/'catalogue.json').write_text(json.dumps(catalogue,indent=2)+'\n')
validation = json.loads((ART/'validation.json').read_text())
validation.update(armorRuntimeIntegrated=True,armorVertices=len(data['vertices'])//3,
                  armorTriangles=sum(len(s['triangles'])//3 for s in data['submeshes']),
                  armorRuntimeSha256=hashlib.sha256(resource.read_bytes()).hexdigest())
validation['checks'].update(finiteArmorGeometry=True,validArmorIndices=True,rigidExistingArmorBoneWeights=True)
(ART/'validation.json').write_text(json.dumps(validation,indent=2)+'\n')
print('ARMOR_RESOURCE_EXPORTED',len(data['parts']),'parts',len(groups),'plates',len(data['vertices'])//3,'vertices',flush=True)
