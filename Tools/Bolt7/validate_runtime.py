"""Run inside Blender with the authored BOLT7.blend open; compare shipped poses to native skinning.
blender -b BOLT7.blend --python validate_runtime.py -- --model path/to/Model.bytes
"""
import bpy,sys,json,gzip,argparse,math
from pathlib import Path
from mathutils import Matrix,Quaternion,Vector
p=argparse.ArgumentParser();p.add_argument('--model',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);d=json.loads(gzip.decompress(Path(a.model).read_bytes()))
rig=bpy.data.objects['BOLT7_Rig'];body=bpy.data.objects['BOLT7_LOD0'];C=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
def mat(p):return Matrix.Translation(Vector(p[:3]))@Quaternion((p[6],p[3],p[4],p[5])).to_matrix().to_4x4()
rest=[]
for b in d['bones']:rest.append(rest[b['parent']]@mat(b['pose']) if b['parent']>=0 else mat(b['pose']))
# Every exported vertex is from the same rest geometry; map its rounded rotated position and bone.
from collections import defaultdict
from itertools import product
lookup=defaultdict(list)
for vertex in body.data.vertices:
 point=C@(body.matrix_world@vertex.co);bone=max(vertex.groups,key=lambda g:g.weight);bone=body.vertex_groups[bone.group].name
 lookup[(tuple(math.floor(v*10000) for v in point),bone)].append((point.copy(),vertex.index))
mapping=[]
for i,b in enumerate(d['boneIndices']):
 point=Vector(d['vertices'][i*3:i*3+3]);cell=tuple(math.floor(v*10000) for v in point);name=d['bones'][b]['name'];candidates=[]
 for delta in product((-1,0,1),repeat=3):
  for original,index in lookup.get((tuple(cell[j]+delta[j] for j in range(3)),name),[]):candidates.append(((original-point).length_squared,index))
 distance,index=min(candidates);assert distance<1e-10,(point,name,distance);mapping.append(index)
maxerror=0;checks=0
for clip in d['clips']:
 rig.animation_data.action=bpy.data.actions['BOLT7_'+clip['name']]
 for frame in [0,(clip['frames']-1)//2,clip['frames']-1]:
  bpy.context.scene.frame_set(frame+1);bpy.context.view_layer.update();posed=[]
  for i,b in enumerate(d['bones']):
   local=mat(clip['poses'][(frame*19+i)*7:(frame*19+i+1)*7]);posed.append(posed[b['parent']]@local if b['parent']>=0 else local)
  skin=[posed[i]@rest[i].inverted() for i in range(19)]
  evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
  # Cover every deduplicated source vertex on all 15 clips at their start, middle and end.
  for i,b in enumerate(d['boneIndices']):
   expected=C@(body.matrix_world@mesh.vertices[mapping[i]].co);actual=skin[b]@Vector(d['vertices'][i*3:i*3+3]);error=(expected-actual).length
   maxerror=max(maxerror,error);assert error<.0001,(clip['name'],frame,i,error)
  evaluated.to_mesh_clear();checks+=1
assert len(d['bones'])==19 and len(d['clips'])==15 and len(d['submeshes'])==6
assert sum(len(s['triangles']) for s in d['submeshes'])//3==76642
assert {'socket_gatling_muzzle','socket_grenade_muzzle'}<={s['name'] for s in d['sockets']}
print('PASS BOLT-7 native model validation: '+str(checks)+' poses, '+str(len(mapping)*checks)+' vertex comparisons; max error '+str(maxerror)+'m; 19 bones, 15 clips, 76,642 triangles and both muzzle sockets.')
