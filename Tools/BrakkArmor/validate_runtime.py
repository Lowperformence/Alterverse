"""Validate exported armor weights against Blender skinning and source torso cleanup."""
import argparse, bpy, gzip, json, math, sys
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/'ArtSource/Heroes/BrakkArmor'
parser = argparse.ArgumentParser()
parser.add_argument('--source', default=str(ROOT/'ArtSource/Heroes/Rook.blend'))
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
data = json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/BrakkArmor/Model.bytes').read_bytes()))
bpy.ops.wm.open_mainfile(filepath=str(ART/'BrakkArmor-concept.blend'))
arm = bpy.data.objects['DF_Skeleton']
arm.data.pose_position = 'POSE'
for track in arm.animation_data.nla_tracks:
    track.mute = True
C = Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
B = C.inverted()
assert data['plateCount'] == 18 and data['solidCount'] == len(data['parts']) == 126
count = len(data['vertices'])//3
assert count == len(data['boneIndices']) and len(data['normals']) == count*3
assert all(math.isfinite(x) for key in ['vertices','normals','uv','pivot'] for x in data[key])
assert all(0 <= i < count for sub in data['submeshes'] for i in sub['triangles'])
samples = []
for part in data['parts']:
    ob = bpy.data.objects['BRK_'+part['id']]
    if data.get('surfaceFit') == 'connected-anatomy-contour-v1':
        # The editable concept is planar; validate the actual shipped contour
        # through Blender's armature modifier rather than the old concept mesh.
        first = part['firstVertex']
        end = first + part['vertexCount']
        points = [B @ Vector(data['vertices'][i*3:i*3+3]) for i in range(first, end)]
        faces = []
        for sub in data['submeshes']:
            for i in range(0, len(sub['triangles']), 3):
                tri = sub['triangles'][i:i+3]
                if all(first <= v < end for v in tri):
                    faces.append(tuple(v-first for v in tri))
        mesh = bpy.data.meshes.new('Runtime contour validation')
        mesh.from_pydata(points, [], faces); mesh.update()
        ob = bpy.data.objects.new('Runtime_'+part['id'], mesh)
        bpy.context.collection.objects.link(ob)
        group = ob.vertex_groups.new(name=part['bone'])
        group.add(list(range(len(points))), 1., 'REPLACE')
        modifier = ob.modifiers.new('Validate shipped skinning', 'ARMATURE')
        modifier.object = arm
        for index in [first, first+part['vertexCount']//2, end-1]:
            assert data['boneNames'][data['boneIndices'][index]] == part['bone']
            samples.append((ob, part['bone'], points[index-first], index-first))
        continue
    tree = KDTree(len(ob.data.vertices))
    for v in ob.data.vertices:
        tree.insert(v.co,v.index)
    tree.balance()
    for index in [part['firstVertex'],part['firstVertex']+part['vertexCount']//2,
                  part['firstVertex']+part['vertexCount']-1]:
        point = B @ Vector(data['vertices'][index*3:index*3+3])
        _,original,distance = tree.find(point)
        assert distance < 1e-6,(part['id'],distance)
        assert data['boneNames'][data['boneIndices'][index]] == part['bone']
        samples.append((ob,part['bone'],point,original))
worst = 0.
poses = 0
for action in bpy.data.actions:
    if action.name not in ['Attack','Death','Hit','Idle','Run','Skill0','Skill1','Skill2','Ultimate']:
        continue
    arm.animation_data.action = action
    for frame in [int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])]:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        deps = bpy.context.evaluated_depsgraph_get()
        for ob in {s[0] for s in samples}:
            evaluated = ob.evaluated_get(deps)
            mesh = evaluated.to_mesh()
            for _,bone,point,index in (s for s in samples if s[0] == ob):
                transform = arm.matrix_world @ arm.pose.bones[bone].matrix @ arm.data.bones[bone].matrix_local.inverted()
                error = (transform@point - evaluated.matrix_world@mesh.vertices[index].co).length
                worst = max(worst,error)
                assert error < 2e-5,(ob.name,action.name,frame,error)
            evaluated.to_mesh_clear()
        poses += 1
assert poses == 27

# Apply the same material/component selection as runtime cleanup to the original anatomy.
bpy.ops.wm.open_mainfile(filepath=str(Path(args.source).resolve()))
ob = bpy.data.objects['DF_Anatomy']
mesh = ob.data
parent = list(range(len(mesh.vertices)))
def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i
def join(a,b):
    a,b = find(a),find(b)
    if a != b:
        parent[b] = a
extra, selected, body = set(),set(),set()
for p in mesh.polygons:
    name = mesh.materials[p.material_index].name.lower()
    if 'fabric' in name or name.startswith(('skin','flesh')):
        body.add(p.index)
        continue
    extra.add(p.index)
    selected.update(p.vertices)
    for i in p.vertices[1:]:
        join(p.vertices[0],i)
seams = {}
for i in selected:
    key = tuple(round(x*100000) for x in mesh.vertices[i].co)
    if key in seams:
        join(i,seams[key])
    else:
        seams[key] = i
limbs = {'Chest','Spine'}
roots = {find(i) for i in selected if sum(g.weight for g in mesh.vertices[i].groups
         if ob.vertex_groups[g.group].name in limbs) >= .2}
removed = {p.index for p in mesh.polygons if p.index in extra and find(p.vertices[0]) in roots}
assert removed and not removed & body
for p in mesh.polygons:
    names = {ob.vertex_groups[g.group].name for i in p.vertices for g in mesh.vertices[i].groups if g.weight > 0}
    if names and names <= {'Head','Neck','HandL','HandR','FootL','FootR','WeaponGripL','WeaponGripR'}:
        assert p.index not in removed,('protected region removed',p.index,names)
    if p.index in extra and any(sum(g.weight for g in mesh.vertices[i].groups
       if ob.vertex_groups[g.group].name in limbs) >= .2 for i in p.vertices):
        assert p.index in removed
report = json.loads((ART/'validation.json').read_text())
report['checks'].update(armorAnimationSamples=poses,maxArmorAttachmentErrorMetres=worst,
                       removedOldTorsoFaces=len(removed),torsoBodyHeadHandsAndFeetPreserved=True)
(ART/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('ARMOR_RUNTIME_VALIDATED',count,'vertices',poses,'poses',worst,'max error',
      len(removed),'old torso faces removed',flush=True)

