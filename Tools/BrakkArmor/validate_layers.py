"""Inspect the shipped plates, including face coverage and separation of layers.

Run with Blender Python. --compare accepts the previous runtime resource and
must demonstrate its missing/buried panels. No Unity renderer is simulated.
"""
import argparse, gzip, json, math
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/BrakkArmor'


def inspect(path):
    data = json.loads(gzip.decompress(Path(path).read_bytes()))
    points = [Vector(data['vertices'][i:i+3]) for i in range(0, len(data['vertices']), 3)]
    assert all(math.isfinite(v) for key in ['vertices', 'normals', 'uv'] for v in data[key])
    assert len(points) == len(data['boneIndices'])
    assert len(data['parts']) == 126 and data['plateCount'] == 18
    diagrams = json.loads((ART / 'diagram-geometry.json').read_text())
    holes = buried = checked = 0; worst_gap = 1.; per_plate = {}
    for diagram in diagrams:
        plate = diagram['plate']; pid = plate['id']; inverse = Matrix(plate['frameModelUnity']).inverted()
        trees = {}
        for layer in ['03_gold_shell', '04_graphite_face']:
            part = next(p for p in data['parts'] if p['id'] == pid + '_' + layer)
            start = part['firstVertex']; end = start + part['vertexCount']
            local = [inverse @ p for p in points[start:end]]
            indices = data['submeshes'][part['material']]['triangles']
            triangles = [tuple(i-start for i in indices[t:t+3]) for t in range(0, len(indices), 3) if start <= indices[t] < end]
            trees[layer] = BVHTree.FromPolygons(local, triangles, all_triangles=True)
        plate_holes = plate_buried = 0
        for ix in range(-3, 4):
            for iy in range(-3, 4):
                origin = Vector((plate['widthMetres'] * ix / 12, plate['heightMetres'] * iy / 12, 2))
                gold = trees['03_gold_shell'].ray_cast(origin, Vector((0, 0, -1)), 4)[0]
                inset = trees['04_graphite_face'].ray_cast(origin, Vector((0, 0, -1)), 4)[0]
                checked += 1
                if gold is None or inset is None:
                    holes += 1; plate_holes += 1; continue
                gap = inset.z - gold.z; worst_gap = min(worst_gap, gap)
                if gap < .001:
                    buried += 1; plate_buried += 1
        per_plate[pid] = {'missing_samples': plate_holes, 'buried_samples': plate_buried}
    minimum_area = float('inf')
    for sub in data['submeshes']:
        for i in range(0, len(sub['triangles']), 3):
            a, b, c = [points[j] for j in sub['triangles'][i:i+3]]
            area = (b-a).cross(c-a).length * .5
            assert area > 1e-12
            minimum_area = min(minimum_area, area)
    volume_error = 0.
    if data.get('surfaceFit') == 'layered-height-field-v2':
        originals = {p['id']:p for d in diagrams for p in d['parts']}
        exported = np.asarray(data['vertices']).reshape(-1,3)
        for part in data['parts']:
            source = originals[part['id']]; original = np.asarray(source['vertices'])
            triangles = [(f[0], f[i], f[i+1]) for f in source['faces'] for i in range(1,len(f)-1)]
            expected = sum(np.dot(original[a],np.cross(original[b],original[c]))/6 for a,b,c in triangles)
            first=part['firstVertex'];end=first+part['vertexCount'];ids=data['submeshes'][part['material']]['triangles']
            actual=0.
            for t in range(0,len(ids),3):
                if first<=ids[t]<end:
                    a,b,c=exported[ids[t:t+3]];actual+=np.dot(a,np.cross(b,c))/6
            assert expected>0 and actual>0,(part['id'],expected,actual)
            error=abs(actual-expected)/expected;volume_error=max(volume_error,error)
            assert error<.003,(part['id'],error)
    return {'revision': data['revision'], 'vertices': len(points), 'triangles': sum(len(s['triangles'])//3 for s in data['submeshes']),
            'layer_samples': checked, 'missing_samples': holes, 'buried_samples': buried, 'minimum_layer_gap_metres': worst_gap,
            'minimum_triangle_area': minimum_area, 'maximum_relative_solid_volume_error':volume_error, 'plates': per_plate}


def animation_check():
    data = json.loads(gzip.decompress((ROOT / 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes').read_bytes()))
    # The accepted fist assembly retains Brakk's original skeleton and actions.
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/Heroes/BrakkFists/BrakkFists-assembly.blend'))
    arm = bpy.data.objects['DF_Skeleton']; arm.data.pose_position = 'POSE'
    for track in arm.animation_data.nla_tracks: track.mute = True
    B = Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
    points = [B @ Vector(data['vertices'][i:i+3]) for i in range(0, len(data['vertices']), 3)]
    mesh = bpy.data.meshes.new('Shipped armor vertices'); mesh.from_pydata(points, [], []); mesh.update()
    obj = bpy.data.objects.new('Shipped armor validation', mesh); bpy.context.collection.objects.link(obj)
    for index, name in enumerate(data['boneNames']):
        assert name in arm.data.bones
        group = obj.vertex_groups.new(name=name)
        group.add([i for i,bone in enumerate(data['boneIndices']) if bone == index], 1., 'REPLACE')
    modifier = obj.modifiers.new('Original Brakk rig', 'ARMATURE'); modifier.object = arm
    samples = [i for part in data['parts'] for i in [part['firstVertex'], part['firstVertex']+part['vertexCount']//2, part['firstVertex']+part['vertexCount']-1]]
    maximum = 0.; poses = 0
    for action in bpy.data.actions:
        arm.animation_data.action = action
        for frame in [int(action.frame_range[0]), int(sum(action.frame_range)*.5), int(action.frame_range[1])]:
            bpy.context.scene.frame_set(frame); bpy.context.view_layer.update()
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()); result = evaluated.to_mesh()
            for i in samples:
                bone = data['boneNames'][data['boneIndices'][i]]
                expected = arm.pose.bones[bone].matrix @ arm.data.bones[bone].matrix_local.inverted() @ points[i]
                maximum = max(maximum, (expected-result.vertices[i].co).length)
            evaluated.to_mesh_clear(); poses += 1
    assert poses == 27 and maximum < 2e-5
    return {'poses':poses, 'sampled_vertices_per_pose':len(samples), 'maximum_attachment_error_metres':maximum}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--compare', type=Path); args = parser.parse_args()
    after = inspect(ROOT / 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes')
    report = {'after': after, 'unity_editor_executed': False}
    if args.compare:
        report['before'] = inspect(args.compare)
        assert report['before']['missing_samples'] + report['before']['buried_samples'] > 0
    assert after['missing_samples'] == 0 and after['buried_samples'] == 0
    report['animation'] = animation_check()
    print(json.dumps(report, indent=2))
    (ART / 'layer-validation.json').write_text(json.dumps(report, indent=2) + '\n')
