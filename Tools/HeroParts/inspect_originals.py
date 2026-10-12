"""Read the exact authored sources before replacing any geometry."""
from pathlib import Path
import json, gzip
import bpy, bmesh

ROOT = Path(__file__).resolve().parents[2]
for name in ('CyberRanger', 'UmbralMage', 'CyberShark'):
    path = ROOT / 'Assets/Alterverse/Resources' / name / 'Model.bytes'
    data = json.loads(gzip.decompress(path.read_bytes()))
    print('ORIGINAL_RESOURCE', name, json.dumps({
        'vertices': len(data['vertices']) // 3,
        'keys': list(data), 'bones': data['boneNames'],
        'triangles': sum(len(s['triangles']) // 3 for s in data['submeshes'])
    }), flush=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/Heroes/Mira.blend'))
body = bpy.data.objects['DF_Anatomy']
arm = bpy.data.objects['DF_Skeleton']
print('TAHLA_BONES', json.dumps({b.name: list(b.head_local) for b in arm.data.bones}), flush=True)
print('TAHLA_MATERIALS', json.dumps([m.name for m in body.data.materials]), flush=True)
bm = bmesh.new(); bm.from_mesh(body.data)
deform = bm.verts.layers.deform.active
groups = {g.index: g.name for g in body.vertex_groups}
seen = set(); candidates = []
for first in bm.verts:
    if first in seen: continue
    stack = [first]; seen.add(first); component = []
    while stack:
        vertex = stack.pop(); component.append(vertex)
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other not in seen: seen.add(other); stack.append(other)
    lo = [min(v.co[k] for v in component) for k in range(3)]
    hi = [max(v.co[k] for v in component) for k in range(3)]
    weighted = sorted({groups.get(i,'?') for v in component for i,w in v[deform].items() if w > .5})
    if any(n.startswith('UpperArm') for n in weighted) and hi[2] > 1.30 and len(component) < 2000:
        candidates.append({'vertices': len(component), 'lo': lo, 'hi': hi, 'bones': weighted,
                           'materials': sorted({body.data.materials[f.material_index].name for v in component for f in v.link_faces})})
print('TAHLA_SHOULDER_COMPONENTS', json.dumps(candidates), flush=True)
bm.free()
