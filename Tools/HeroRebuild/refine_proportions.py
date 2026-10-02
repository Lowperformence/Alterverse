"""Reference-led silhouette refinement of existing Blender heroes.
Geometry-only pass: original rest bones, animation curves, grip sockets and weapons stay intact.
Can also be called by build_heroes.py immediately before FBX export.
"""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
PROFILES={
 'Veyr':dict(head=.94,waist=.92,chest=1.04,thigh=1.08,calf=1.025,shoulder=.82),
 'Kaelon':dict(head=.95,waist=.94,chest=1.055,thigh=1.12,calf=1.055,shoulder=.64),
 'Nyra':dict(head=.95,waist=.96,chest=1.02,thigh=1.035,calf=1.01,shoulder=.90),
 'Elyra':dict(head=.95,waist=.98,chest=1.025,thigh=1.035,calf=1.01,shoulder=.90),
 'Mira':dict(head=.95,waist=.98,chest=1.025,thigh=1.035,calf=1.01,shoulder=.90),
 'Rook':dict(head=.97,waist=1.075,chest=1.04,thigh=1.15,calf=1.07,shoulder=1.0),
 'Traveler':dict(head=.94,waist=.92,chest=1.045,thigh=1.08,calf=1.025,shoulder=.85)}
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def components(mesh):
 parent=list(range(len(mesh.vertices)))
 def find(a):
  while parent[a]!=a:parent[a]=parent[parent[a]];a=parent[a]
  return a
 for edge in mesh.edges:
  a,b=map(find,edge.vertices)
  if a!=b:parent[a]=b
 out={}
 for i in range(len(parent)):out.setdefault(find(i),[]).append(i)
 return list(out.values())
def refine(hero,arm,objects):
 if arm.get('reference_proportions_v1'):return None
 cfg=PROFILES[hero];body=next(o for o in objects if o.name=='DF_Anatomy');names={g.index:g.name for g in body.vertex_groups};bones=arm.data.bones
 original=[v.co.copy() for v in body.data.vertices];head=bones['Head'].head_local.copy();height=bones['Head'].tail_local.z;scale=height/1.75;waist_z=1.10*scale;chest_z=1.32*scale
 groups=components(body.data);shoulders=[];collars=[]
 for ids in groups:
  points=[original[i] for i in ids];center=sum(points,Vector())/len(points);span=max(p.x for p in points)-min(p.x for p in points)
  rigid=[]
  for i in ids:
   gs=body.data.vertices[i].groups;strong=[names[g.group] for g in gs if g.weight>.999]
   if len(strong)!=1:break
   rigid.append(strong[0])
  if len(rigid)!=len(ids) or len(set(rigid))!=1:continue
  name=rigid[0]
  if name.startswith('UpperArm') and center.z>bones[name].head_local.z-.14*scale and span>.075:
   shoulders.append((ids,bones[name].head_local.copy()))
  elif name in ['Chest','Head'] and abs(center.x)<.075 and abs(center.z-head.z)<.075*scale and span>.23:
   collars.append(ids)
 shoulder_ids={i for ids,_ in shoulders for i in ids};collar_ids={i for ids in collars for i in ids}
 for vertex in body.data.vertices:
  p=original[vertex.index];groups={names[g.group]:g.weight for g in vertex.groups};new=p.copy()
  # Reduce head/headdress around its neck attachment, with a continuous throat blend.
  hw=groups.get('Head',0)*smooth((p.z-head.z)/(.14*scale))
  new=head+(new-head)*(1+(cfg['head']-1)*hw)
  # Torso taper never reaches a hand, weapon or arm-bound armor part.
  torso=sum(groups.get(n,0) for n in ['Hips','Spine','Chest'])
  if vertex.index not in shoulder_ids and vertex.index not in collar_ids:
   waist=math.exp(-((p.z-waist_z)/(.12*scale))**2);chest=math.exp(-((p.z-chest_z)/(.11*scale))**2)
   new.x*=1+torso*((cfg['waist']-1)*waist+(cfg['chest']-1)*chest)
   new.y*=1+torso*((cfg['waist']-1)*waist*.35+(cfg['chest']-1)*chest*.4)
  if vertex.index not in shoulder_ids:
   for name,weight in groups.items():
    if not name.startswith(('UpperArm','Forearm','Thigh','Shin')):continue
    bone=bones[name];axis=bone.tail_local-bone.head_local;t=(p-bone.head_local).dot(axis)/axis.length_squared
    if not 0<t<1:continue
    center=bone.head_local+axis*t;radial=p-center
    if name.startswith(('UpperArm','Forearm')):
     # Preserve the full +30% at mid-arm, replace abrupt end ramps by a smooth muscle taper.
     old=min(1,t/.10,(1-t)/.10);envelope=math.sin(math.pi*t)**.85
     factor=(1+.30*envelope)/(1+.30*old)
    else:factor=1+(cfg['thigh']-1 if name.startswith('Thigh') else cfg['calf']-1)*math.sin(math.pi*t)**.8
    new+=radial*(factor-1)*weight
  vertex.co=new
 for ids,anchor in shoulders:
  for i in ids:body.data.vertices[i].co=anchor+(body.data.vertices[i].co-anchor)*cfg['shoulder']
 for ids in collars:
  for i in ids:
   p=body.data.vertices[i].co;p.x*=.84;p.y*=.84
 body.data.update();arm['reference_proportions_v1']=True
 return {'hero':hero,'profile':cfg,'shoulderPieces':len(shoulders),'collarPieces':len(collars),'restBonesUnchanged':True,'weaponsAndSocketsUnchanged':True,'modifiedVertices':sum((v.co-original[v.index]).length>1e-7 for v in body.data.vertices)}
def main():
 root=next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir());out=root/'Assets/Alterverse/Resources/AuthoredHeroes';out.mkdir(parents=True,exist_ok=True);preview=root/'ArtSource/Heroes/Preview';preview.mkdir(parents=True,exist_ok=True);reports=[]
 for hero in PROFILES:
  bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Heroes'/f'{hero}.blend'));arm=bpy.data.objects['DF_Skeleton'];objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith(('DF_Anatomy','DF_Weapon','DF_Gauntlet'))]
  before={b.name:(tuple(b.head_local),tuple(b.tail_local)) for b in arm.data.bones};weapons={o.name:[tuple(v.co) for v in o.data.vertices] for o in objects if o.name!='DF_Anatomy'}
  report=refine(hero,arm,objects)
  assert before=={b.name:(tuple(b.head_local),tuple(b.tail_local)) for b in arm.data.bones}
  assert weapons=={o.name:[tuple(v.co) for v in o.data.vertices] for o in objects if o.name!='DF_Anatomy'}
  if report:reports.append(report)
  sockets=[o for o in bpy.context.scene.objects if o.type=='EMPTY' and o.name.startswith('DF_')]
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects+[arm]+sockets:o.select_set(True)
  bpy.context.view_layer.objects.active=arm
  bpy.ops.export_scene.fbx(filepath=str(out/f'{hero}.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.5,path_mode='AUTO')
  scene=bpy.context.scene;scene.cycles.samples=16;scene.render.filepath=str(preview/f'{hero}.png');bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/Heroes'/f'{hero}.blend'),compress=True);bpy.ops.render.render(write_still=True);print('PROPORTIONS_REFINED',hero,flush=True)
 (root/'ArtSource/Heroes/reference-proportions-validation.json').write_text(json.dumps(reports,indent=2))
if __name__=='__main__':main()
