"""Validate actual rebuilt mesh thickness against the prior Blender backup, without posing."""
import bpy,json,statistics
from pathlib import Path
from mathutils import Vector
root=next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir())
rows=[]
def read(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));arm=bpy.data.objects['DF_Skeleton'];o=bpy.data.objects['DF_Anatomy']
 names={g.index:g.name for g in o.vertex_groups};radii={}
 for v in o.data.vertices:
  for g in v.groups:
   n=names[g.group]
   if g.weight<.75 or not n.startswith(('UpperArm','Forearm','Thigh','Shin')):continue
   b=arm.data.bones[n];axis=b.tail_local-b.head_local;t=(v.co-b.head_local).dot(axis)/axis.length_squared
   if .30<t<.70:radii.setdefault(n,[]).append((v.co-b.head_local-axis*t).length)
 return {k:statistics.median(v) for k,v in radii.items()},{b.name:(tuple(b.head_local),tuple(b.tail_local)) for b in arm.data.bones}
for hero in ['Veyr','Kaelon','Nyra','Elyra','Mira','Rook','Traveler']:
 before,bones=read(root/'ArtSource/Heroes'/f'{hero}.blend1');after,newbones=read(root/'ArtSource/Heroes'/f'{hero}.blend')
 assert bones==newbones,(hero,'joint positions changed')
 ratios={n:after[n]/r for n,r in before.items()};female=hero in ['Nyra','Elyra','Mira']
 for n,r in ratios.items():
  assert r<.99 if female else r>1.03,(hero,n,r)
 rows.append({'hero':hero,'female':female,'medianRadialThicknessRatio':ratios,'jointPositionsUnchanged':True})
 print('LIMB_VOLUME_VALIDATED',hero,ratios,flush=True)
(root/'ArtSource/Heroes/limb-volume-validation.json').write_text(json.dumps(rows,indent=2))
