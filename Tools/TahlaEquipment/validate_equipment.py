"""Validate real native solids, exported skinning, original contacts and rest rig."""
import bpy,bmesh,csv,gzip,json,math
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/TahlaEquipment'
data=json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/TahlaEquipment/Model.bytes').read_bytes()))
bpy.ops.wm.open_mainfile(filepath=str(ART/'Tahla-equipment.blend'))
arm=bpy.data.objects['DF_Skeleton'];C=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)));B=C.inverted()
parts=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('TAH_')]
assert len(parts)==130,len(parts)
for ob in parts:
 bm=bmesh.new();bm.from_mesh(ob.data)
 assert all(e.is_manifold for e in bm.edges),(ob.name,'open edge')
 assert abs(bm.calc_volume())>1e-10,(ob.name,'zero volume');bm.free()
 assert len(ob.modifiers)==1 and ob.modifiers[0].type=='ARMATURE'
 assert all(len(v.groups)==1 and v.groups[0].weight==1 and ob.vertex_groups[v.groups[0].group].name==ob['rig_bone'] for v in ob.data.vertices)
fixtures={}
for row in csv.DictReader((ART/'reference-rig.csv').open()):
 p=Vector([float(row[k]) for k in ['px','py','pz']]);q=Quaternion([float(row[k]) for k in ['qw','qx','qy','qz']])
 local=Matrix.Translation(p)@q.to_matrix().to_4x4();fixtures[row['bone']]=(fixtures[row['parent']]@local if row['parent'] else local)
for name,frame in fixtures.items():
 actual=C@(arm.matrix_world@arm.data.bones[name].head_local)
 assert (actual-frame.translation).length<2e-6,(name,actual,frame.translation)
samples=[]
for assembly in data['assemblies']:
 n=len(assembly['vertices'])//3
 assert len(assembly['normals'])==n*3 and len(assembly['uv'])==n*2 and len(assembly['boneIndices'])==n
 assert all(math.isfinite(x) for key in ['vertices','normals','uv'] for x in assembly[key])
 assert all(0<=i<n for s in assembly['submeshes'] for i in s['triangles'])
 for part in assembly['parts']:
  ob=bpy.data.objects['TAH_'+part['id']];tree=KDTree(len(ob.data.vertices))
  for v in ob.data.vertices:tree.insert(v.co,v.index)
  tree.balance()
  for index in [part['firstVertex'],part['firstVertex']+part['vertexCount']//2,part['firstVertex']+part['vertexCount']-1]:
   p=B@Vector(assembly['vertices'][index*3:index*3+3]);_,original,distance=tree.find(p)
   assert distance<1e-6,(part['id'],distance)
   assert data['boneNames'][assembly['boneIndices'][index]]==part['bone']
   samples.append((ob,part['bone'],p,original))
for track in arm.animation_data.nla_tracks:track.mute=True
arm.data.pose_position='POSE';worst=0.;poses=0
for action in bpy.data.actions:
 if action.name not in ['Attack','Death','Hit','Idle','Run','Skill0','Skill1','Skill2','Ultimate']:continue
 arm.animation_data.action=action
 for frame in [int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])]:
  bpy.context.scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
  for ob in parts:
   evaluated=ob.evaluated_get(deps);mesh=evaluated.to_mesh()
   for _,bone,p,index in (s for s in samples if s[0]==ob):
    transform=arm.matrix_world@arm.pose.bones[bone].matrix@arm.data.bones[bone].matrix_local.inverted()
    error=(transform@p-evaluated.matrix_world@mesh.vertices[index].co).length
    worst=max(worst,error);assert error<2e-5,(ob.name,action.name,frame,error)
   evaluated.to_mesh_clear()
  poses+=1
assert poses==27
assert data['staffHandle']==json.loads((ART/'model.json').read_text())['originalStaffHandle']
arm.data.pose_position='REST';bpy.context.view_layer.update()
body=bpy.data.objects['DF_Anatomy'];body.data.calc_loop_triangles()
tree=BVHTree.FromPolygons([body.matrix_world@v.co for v in body.data.vertices],
                       [t.vertices[:] for t in body.data.loop_triangles],all_triangles=True)
overlaps={}
for ob in parts:
 if ob['module'] in ('Helmet','Staff','Focus','C03') or ob['part_id'].endswith('_mount'):continue
 if not any(ob['part_id'].endswith(x) for x in ['_backing','_gold_border','_ivory_face']):continue
 ob.data.calc_loop_triangles()
 own=BVHTree.FromPolygons([ob.matrix_world@v.co for v in ob.data.vertices],[t.vertices[:] for t in ob.data.loop_triangles],all_triangles=True)
 matches=tree.overlap(own)
 if matches:overlaps[ob.name]=len(matches)
report={'parts':len(parts),'plateModules':len(data['plateModules']),'closedManifoldPositiveVolume':True,
        'originalRestBonePositionsVerified':len(fixtures),'originalStaffHandlePreserved':True,
        'geometryAndIndicesValid':True,'rigidSingleBoneWeights':True,'animationPoseSamples':poses,
        'maxAttachmentErrorMetres':worst,'plateBodyIntersections':overlaps,'unityEditorExecuted':False}
(ART/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
assert not overlaps,overlaps
print('TAHLA_EQUIPMENT_VALIDATED',json.dumps(report),flush=True)
