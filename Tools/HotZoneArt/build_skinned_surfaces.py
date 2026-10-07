"""Blender UV pass over the exact existing rigid-skinned monster geometry.
Run blender -b --python Tools/HotZoneArt/build_skinned_surfaces.py.
Only UV seams duplicate vertices; coordinates, normals, weights, joints and faces are preserved.
"""
from pathlib import Path
import bpy, gzip, hashlib, json, math, struct

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / 'Assets/Alterverse/Resources'
ART = ROOT / 'ArtSource/HotZoneMonsters'
FAMILIES = {'MarsSkeleton':'Skeleton', 'GreenStalker':'Xeno',
            'NoxaraClockworkSpider':'Spider', 'KryonBear':'Ice',
            'MechanicalScorpion':'Machine', 'GJMagentaGolem':'Sandstone',
            'PSRCrystalTitan':'Crystal'}
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
reports = []
for number, (resource, family) in enumerate(FAMILIES.items()):
    source = (RES / resource / 'Model.bytes').read_bytes()
    data = json.loads(gzip.decompress(source))
    vertices = [tuple(data['vertices'][i:i+3]) for i in range(0,len(data['vertices']),3)]
    faces, slots = [], []
    for slot, sub in enumerate(data['submeshes']):
        faces.extend(tuple(sub['triangles'][i:i+3]) for i in range(0,len(sub['triangles']),3))
        slots.extend([slot] * (len(sub['triangles'])//3))
    mesh = bpy.data.meshes.new(resource+'_OriginalGeometry')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(resource+'_BlenderUV',mesh)
    bpy.context.collection.objects.link(obj)
    obj['source_sha256'] = hashlib.sha256(source).hexdigest()
    obj['coordinates'] = 'Unity Y-up, bone-local; animation rig remains in Model.bytes'
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.006)
    bpy.ops.object.mode_set(mode='OBJECT')
    uv_data = mesh.uv_layers.active.data
    # Blender's face loop order is retained; do not triangulate or recalculate source normals.
    mapped, coords, lookup = [], [], {}
    submeshes = [[] for _ in data['submeshes']]
    for face, slot in zip(mesh.polygons, slots):
        assert len(face.vertices)==3
        for loop_index in face.loop_indices:
            original = mesh.loops[loop_index].vertex_index
            uv = tuple(round(float(v),7) for v in uv_data[loop_index].uv)
            assert all(math.isfinite(v) and -.001<=v<=1.001 for v in uv)
            key = (original,uv)
            if key not in lookup:
                lookup[key]=len(mapped);mapped.append(original);coords.append(uv)
            submeshes[slot].append(lookup[key])
    assert all(len(s)==len(old['triangles']) for s,old in zip(submeshes,data['submeshes']))
    assert len(mapped)<=len(faces)*3
    payload = bytearray(b'DFUV'+struct.pack('<IIII',1,len(vertices),len(mapped),len(submeshes)))
    payload.extend(hashlib.sha256(source).digest())
    for original,uv in zip(mapped,coords): payload.extend(struct.pack('<Iff',original,*uv))
    for indices in submeshes:
        payload.extend(struct.pack('<I',len(indices)))
        payload.extend(struct.pack('<'+'I'*len(indices),*indices))
    (RES/resource/'SkinUV.bytes').write_bytes(gzip.compress(payload,mtime=0))
    # Pack the existing new surface/normal atlases in the editable Blender authoring source.
    for slot in range(len(data['submeshes'])):
        mat=bpy.data.materials.new(resource+'_Surface_'+str(slot));mat.use_nodes=True
        shader=mat.node_tree.nodes.get('Principled BSDF')
        if slot!=3:
            image=bpy.data.images.load(str(RES/'AuthoredHotZone'/f'{family}Surface.png'),check_existing=True);image.pack()
            tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
            mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
            normal=bpy.data.images.load(str(RES/'AuthoredHotZone'/f'{family}Normal.png'),check_existing=True)
            normal.colorspace_settings.name='Non-Color';normal.pack()
            tex_n=mat.node_tree.nodes.new('ShaderNodeTexImage');tex_n.image=normal
            bump=mat.node_tree.nodes.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=.32
            mat.node_tree.links.new(tex_n.outputs['Color'],bump.inputs['Color'])
            mat.node_tree.links.new(bump.outputs['Normal'],shader.inputs['Normal'])
        mesh.materials.append(mat)
    for face,slot in zip(mesh.polygons,slots):face.material_index=slot
    obj.location=(number*8,0,0);obj.select_set(False)
    reports.append({'resource':resource,'family':family,'sourceSha256':hashlib.sha256(source).hexdigest(),
        'sourceVertices':len(vertices),'uvVertices':len(mapped),'triangles':len(faces),
        'uvBytes':(RES/resource/'SkinUV.bytes').stat().st_size,
        'geometry':'unchanged','rig':'unchanged','submeshes':'unchanged'})
ART.mkdir(parents=True,exist_ok=True)
(ART/'skinned-surfaces-report.json').write_text(json.dumps(reports,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'HotZoneSkinnedSurfaces.blend'),compress=True)
print('HOTZONE_SKINNED_UV_COMPLETE',json.dumps(reports))
