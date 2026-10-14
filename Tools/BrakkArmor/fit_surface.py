"""Conform runtime plate layers to the connected anatomical skin in bind space.

Run with Blender --background --python fit_surface.py. The original authored
solids remain editable; export_runtime.py applies the same fit on every export.
"""
import gzip
import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))
MOUNT_GAP = .008


def skin_surface(body):
    """Exclude disconnected cloth cages and the armor that is being replaced."""
    mesh = body.data
    parents = list(range(len(mesh.vertices)))

    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i

    faces, used = [], set()
    for face in mesh.polygons:
        name = mesh.materials[face.material_index].name.lower()
        if not ('fabric' in name or name.startswith(('skin', 'flesh'))):
            continue
        faces.append(face.index)
        used.update(face.vertices)
        for i in face.vertices[1:]:
            parents[find(i)] = find(face.vertices[0])
    seams = {}
    for i in used:
        key = tuple(round(v * 100000) for v in mesh.vertices[i].co)
        if key in seams:
            parents[find(i)] = find(seams[key])
        else:
            seams[key] = i
    counts = {}
    for i in used:
        root = find(i)
        counts[root] = counts.get(root, 0) + 1
    main = max(counts, key=counts.get)
    selected = {i for i in faces if find(mesh.polygons[i].vertices[0]) == main}
    mesh.calc_loop_triangles()
    vertices = [C @ (body.matrix_world @ v.co) for v in mesh.vertices]
    triangles = [list(t.vertices) for t in mesh.loop_triangles if t.polygon_index in selected]
    return BVHTree.FromPolygons(vertices, triangles, all_triangles=True)


def conform(data, plates, body):
    if data.get('surfaceFit') == 'connected-anatomy-contour-v1':
        return data
    tree = skin_surface(body)
    frames = {p['id']: Matrix(p['frameModelUnity']) for p in plates}
    supports = {}
    points = [Vector(data['vertices'][i:i + 3]) for i in range(0, len(data['vertices']), 3)]
    for part in data['parts']:
        frame = frames[part['id'].split('_')[0]]
        inverse = frame.inverted()
        normal = (frame.to_3x3() @ Vector((0, 0, 1))).normalized()
        # Cache by footprint, so duplicate vertices and all manufactured layers
        # share exactly the same support surface rather than separating again.
        for i in range(part['firstVertex'], part['firstVertex'] + part['vertexCount']):
            local = inverse @ points[i]
            plane = frame @ Vector((local.x, local.y, 0))
            key = (part['id'].split('_')[0], round(local.x, 6), round(local.y, 6))
            hit = supports.get(key)
            if hit is None:
                hit, _, _, distance = tree.ray_cast(plane + normal * .35, -normal, .7)
                if hit is None or (hit - plane).length > .22:
                    hit, _, _, distance = tree.find_nearest(plane)
                    if hit is not None:
                        # Retain a small tangential footprint at silhouette edges.
                        # Collapsing every overhanging corner onto the same nearest
                        # body vertex would create zero-area faces and bad normals.
                        tangent = plane - hit
                        tangent -= normal * tangent.dot(normal)
                        hit += tangent * .08
                supports[key] = hit
            if hit is None:
                raise ValueError('No anatomical support for ' + part['id'])
            # The back of each mounting plate is z=-.011 in its authoring frame.
            # Keep shell thickness and fastener height while removing empty air.
            points[i] = hit + normal * (MOUNT_GAP + local.z + .011)
    # Flat normals must follow the conformed geometry, not the old planar faces.
    normals = [Vector((0, 0, 0)) for _ in points]
    for sub in data['submeshes']:
        indices = sub['triangles']
        for k in range(0, len(indices), 3):
            a, b, c = indices[k:k + 3]
            n = (points[b] - points[a]).cross(points[c] - points[a])
            for i in (a, b, c):
                normals[i] += n
    data['vertices'] = [round(float(v), 8) for point in points for v in point]
    data['normals'] = [round(float(v), 8) for n in normals for v in n.normalized()]
    data['surfaceFit'] = 'connected-anatomy-contour-v1'
    return data


if __name__ == '__main__':
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'ArtSource/Heroes/Rook.blend'))
    resource = ROOT / 'Assets/Alterverse/Resources/BrakkArmor/Model.bytes'
    data = json.loads(gzip.decompress(resource.read_bytes()))
    catalogue = json.loads((ROOT / 'ArtSource/Heroes/BrakkArmor/catalogue.json').read_text())
    conform(data, catalogue['plates'], bpy.data.objects['DF_Anatomy'])
    resource.write_bytes(gzip.compress(json.dumps(data, separators=(',', ':')).encode(), mtime=0))
    print('BRAKK_ARMOR_SURFACE_FIT', len(data['parts']), 'parts', flush=True)
