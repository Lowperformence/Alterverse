"""Replace only Head-bound triangles; preserve existing runtime body/weapon buffers.

Blender releases may triangulate the same arm envelope differently. A head-only
revision must not replace the already corrected arm normals, weights or weapons.
"""
import copy

def merge_head_export(previous,current):
 if previous['boneNames']!=current['boneNames'] or previous['anchors']!=current['anchors']:
  raise ValueError('Head revision cannot change the shared rig contract')
 result=copy.deepcopy(previous)
 vector_fields=('vertices','normals')
 scalar_fields=('boneIndices','boneIndices2','boneWeights2','groups')
 for field in vector_fields+scalar_fields:result[field]=[]
 result['submeshes']=[{'triangles':[]} for _ in current['submeshes']]
 maps=[]
 for payload,want_head in [(previous,False),(current,True)]:
  remap={}
  for i,bone in enumerate(payload['boneIndices']):
   if (payload['boneNames'][bone]=='Head')!=want_head:continue
   remap[i]=len(result['boneIndices'])
   for field in vector_fields:result[field].extend(payload[field][i*3:i*3+3])
   for field in scalar_fields:result[field].append(payload[field][i])
  for material,submesh in enumerate(payload['submeshes']):
   indices=submesh['triangles']
   for start in range(0,len(indices),3):
    triangle=indices[start:start+3]
    kept=[i in remap for i in triangle]
    if any(kept) and not all(kept):raise ValueError('Mixed head/body triangle cannot be isolated')
    if all(kept):result['submeshes'][material]['triangles'].extend(remap[i] for i in triangle)
  maps.append(remap)
 result['tailVertices']=[maps[0][i] for i in previous['tailVertices']]
 result['mouthVertices']=[maps[1][i] for i in current['mouthVertices']]
 return result
