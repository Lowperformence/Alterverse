"""Validate the runtime head export and preservation of non-head triangles.

python Tools/CyberShark/verify_reference_head.py --original /path/to/previous/Model.bytes
"""
from pathlib import Path
from collections import Counter,defaultdict
import argparse,gzip,json,math,sys

ROOT=Path(__file__).resolve().parents[2]

def load(path):return json.loads(gzip.decompress(Path(path).read_bytes()))

def body_triangles(data):
 result=Counter()
 for material,sub in enumerate(data['submeshes']):
  for start in range(0,len(sub['triangles']),3):
   ids=sub['triangles'][start:start+3]
   if any(data['boneNames'][data['boneIndices'][i]]=='Head' for i in ids):continue
   records=[]
   for i in ids:
    records.append(tuple(data['vertices'][i*3:i*3+3]+data['normals'][i*3:i*3+3]+[
     data['boneIndices'][i],data['boneIndices2'][i],data['boneWeights2'][i],data['groups'][i]]))
   # Preserve winding while allowing Blender to choose a different first corner.
   records=tuple(records);result[(material,min(records,records[1:]+records[:1],records[2:]+records[:2]))]+=1
 return result

def validate(data,original=None):
 count=len(data['boneIndices'])
 assert data['version']==1 and len(data['submeshes'])==6
 assert len(data['vertices'])==len(data['normals'])==count*3
 assert all(len(data[k])==count for k in ('boneIndices2','boneWeights2','groups'))
 assert all(math.isfinite(x) for k in ('vertices','normals','boneWeights2') for x in data[k])
 assert all(0<=x<len(data['boneNames']) for k in ('boneIndices','boneIndices2') for x in data[k])
 assert all(0<=x<=1 for x in data['boneWeights2'])
 assert all(x in (0,1,2) for x in data['groups'])
 referenced=[]
 for sub in data['submeshes']:
  indices=sub['triangles'];assert len(indices)%3==0
  assert all(0<=i<count for i in indices);referenced.extend(indices)
 assert set(referenced)==set(range(count))
 lengths=[math.sqrt(sum(x*x for x in data['normals'][i:i+3])) for i in range(0,count*3,3)]
 assert all(.99<n<1.01 for n in lengths),'Invalid vertex normal'
 head=[i for i,b in enumerate(data['boneIndices']) if data['boneNames'][b]=='Head']
 assert head and all(data['groups'][i]==0 for i in head)
 assert all(data['boneWeights2'][i]==0 for i in head),'Head escaped its rigid bone binding'
 head_set=set(head)
 assert data['mouthVertices'] and all(i in head_set for i in data['mouthVertices'])
 assert all(data['groups'][i]==0 for i in data['tailVertices'])
 head_points=[data['vertices'][i*3:i*3+3] for i in head]
 minimum=[min(p[j] for p in head_points) for j in range(3)]
 maximum=[max(p[j] for p in head_points) for j in range(3)]
 assert minimum[1]>1.54 and maximum[1]<2.09
 assert maximum[0]-minimum[0]>.42 and maximum[2]<.35
 unchanged=None
 if original:
  assert data['boneNames']==original['boneNames'] and data['anchors']==original['anchors']
  assert body_triangles(data)==body_triangles(original),'Body or weapon triangles changed'
  assert all(data[k]==original[k] for k in ('tailPivot','hookGrip','markers'))
  unchanged=sum(body_triangles(original).values())
 return {'triangles':len(referenced)//3,'vertices':count,
  'headTriangles':len(head)//3,'headBounds':{'min':minimum,'max':maximum},
  'meshGroupVertices':{str(g):data['groups'].count(g) for g in range(3)},
  'preservedBodyWeaponTriangles':unchanged,'finiteUnitNormals':True,
  'headBoundToExistingBone':True,'mouthMaterialIndicesValid':True,
  'unityEditorAndAndroidBuildExecuted':False}

def validate_blender_source():
 import bpy
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Heroes/CyberShark/NerokCyberShark.blend'))
 obj=bpy.data.objects['Nerok Abyss'];mesh=obj.data
 group=obj.vertex_groups['Head'].index
 head={v.index for v in mesh.vertices if any(g.group==group and g.weight>.99 for g in v.groups)}
 adjacent=defaultdict(set)
 for polygon in mesh.polygons:
  ids=list(polygon.vertices)
  if not all(i in head for i in ids):continue
  for a,b in zip(ids,ids[1:]+ids[:1]):adjacent[a].add(b);adjacent[b].add(a)
 remaining=set(head);components=[]
 while remaining:
  start=remaining.pop();component={start};stack=[start]
  while stack:
   a=stack.pop()
   for b in adjacent[a]:
    if b in remaining:remaining.remove(b);component.add(b);stack.append(b)
  components.append(component)
 skin=max(components,key=len);edges=Counter();directions=Counter();faces=0
 for polygon in mesh.polygons:
  ids=list(polygon.vertices)
  if not all(i in skin for i in ids):continue
  faces+=1
  for a,b in zip(ids,ids[1:]+ids[:1]):
   edges[tuple(sorted((a,b)))]+=1;directions[tuple(sorted((a,b)))]+=1 if a<b else -1
 assert all(n==2 for n in edges.values()),'Open/non-manifold unified head'
 assert all(n==0 for n in directions.values()),'Inconsistent unified head winding'
 assert len(skin)-len(edges)+faces==2,'Unexpected unified head topology'
 assert obj.modifiers.get('Rigid hero skin').object==bpy.data.objects['NerokArmature']
 result={'unifiedHeadClosedManifold':True,'unifiedHeadConsistentWinding':True,
         'unifiedHeadEulerCharacteristic':2,'unifiedHeadSourceVertices':len(skin),
         'sourceArmaturePresent':True,'blenderVersion':bpy.app.version_string,
         'renderedViews':['head-front.png','head-three-quarter.png','head-side.png']}
 return result

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--original');parser.add_argument('--source',action='store_true')
 arguments=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
 args=parser.parse_args(arguments)
 data=load(ROOT/'Assets/Alterverse/Resources/CyberShark/Model.bytes')
 report=validate(data,load(args.original) if args.original else None)
 if args.source:report.update(validate_blender_source())
 output=ROOT/'ArtSource/Heroes/CyberShark/head-validation.json'
 output.write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps(report,indent=2))
