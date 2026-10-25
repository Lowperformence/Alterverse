"""Blender: export BOLT-7's exact rigidly skinned geometry and sampled authored poses.
blender -b BOLT7.blend --python export_runtime.py -- --output Assets/Alterverse/Resources/Bolt7/Model.bytes
Coordinate mapping rotates Blender Z-up into Unity Y-up: (x, y, z) -> (x, z, -y).
The .blend remains the authoring source; no Unity FBX scale/retarget assumptions are used.
"""
import bpy,sys,json,gzip,math,argparse
from pathlib import Path
from mathutils import Matrix,Vector
p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--source-fbx');args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
if args.source_fbx:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(Path(args.source_fbx).resolve()))
rig=bpy.data.objects['BOLT7_Rig'];body=bpy.data.objects['BOLT7_LOD0'];scene=bpy.context.scene
rig.animation_data.action=None
for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
scene.frame_set(1);bpy.context.view_layer.update()
C=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)));CI=C.inverted()
bones=list(rig.data.bones);lookup={b.name:i for i,b in enumerate(bones)}
def packed(m):
 pos,rot,scale=(C@m@CI).decompose()
 assert max(abs(x-1) for x in scale)<.0001,tuple(scale)
 return [round(x,7) for x in [*pos,rot.x,rot.y,rot.z,rot.w]]
rest=[]
for i,b in enumerate(bones):
 parent=lookup[b.parent.name] if b.parent else -1
 assert parent<i
 local=b.parent.matrix_local.inverted()@b.matrix_local if b.parent else b.matrix_local
 pose=packed(local);rest.append({'name':b.name,'parent':parent,'pose':pose})
sockets=[]
for ob in scene.objects:
 if not ob.name.startswith('socket_') or ob.parent!=rig:continue
 bone=lookup.get(ob.parent_bone,-1)
 world=rig.matrix_world.inverted()@ob.matrix_world
 local=bones[bone].matrix_local.inverted()@world if bone>=0 else world
 sockets.append({'name':ob.name,'parent':bone,'pose':packed(local)})
me=body.data;me.calc_normals_split();me.calc_loop_triangles()
verts=[];normals=[];uvs=[];indices=[];sub=[{'triangles':[]} for _ in me.materials];unique={}
for tri in me.loop_triangles:
 for li in tri.loops:
  loop=me.loops[li];vertex=me.vertices[loop.vertex_index];uv=me.uv_layers.active.data[li].uv
  normal=loop.normal
  key=(vertex.index,*(round(x,6) for x in normal),*(round(x,6) for x in uv))
  if key not in unique:
   groups=[(g.weight,body.vertex_groups[g.group].name) for g in vertex.groups if g.weight>0]
   weight,bone=max(groups);assert weight>.999 and bone in lookup
   pos=C@(body.matrix_world@vertex.co);n=C.to_3x3()@(body.matrix_world.to_3x3().inverted().transposed()@normal)
   unique[key]=len(indices);verts.extend(round(x,7) for x in pos);normals.extend(round(x,7) for x in n.normalized());uvs.extend(round(x,7) for x in uv);indices.append(lookup[bone])
  sub[tri.material_index]['triangles'].append(unique[key])
clips=[];fps=30
for action in sorted(bpy.data.actions,key=lambda a:a.name):
 if 'BOLT7_' not in action.name:continue
 rig.animation_data.action=action;first,last=map(round,action.frame_range);poses=[]
 for frame in range(first,last+1):
  scene.frame_set(frame);bpy.context.view_layer.update()
  for b in bones:
   pb=rig.pose.bones[b.name]
   m=pb.parent.matrix.inverted()@pb.matrix if pb.parent else pb.matrix
   poses.extend(packed(m))
 name=action.name.rsplit('BOLT7_',1)[-1];clips.append({'name':name,'fps':fps,'frames':last-first+1,'poses':poses})
assert len(bones)==19 and len(clips)==15
assert len(verts)==len(normals)==len(indices)*3 and len(uvs)==len(indices)*2
assert sum(len(x['triangles']) for x in sub)//3==76642
assert all(math.isfinite(v) for v in verts+normals+uvs)
data={'version':1,'bones':rest,'sockets':sockets,'materials':[m.name for m in me.materials],'vertices':verts,'normals':normals,'uvs':uvs,'boneIndices':indices,'submeshes':sub,'clips':clips}
out=Path(args.output);out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))
print('BOLT7_RUNTIME',json.dumps({'bytes':out.stat().st_size,'vertices':len(indices),'triangles':76642,'bones':len(bones),'clips':len(clips),'sockets':[s['name'] for s in sockets],'bounds':[[min(verts[i::3]) for i in range(3)],[max(verts[i::3]) for i in range(3)]]}))
