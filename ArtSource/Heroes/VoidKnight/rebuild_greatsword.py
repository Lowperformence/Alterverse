"""V5 greatsword. Only WeaponGripR-bound geometry changes; body/rig/actions stay intact.
Blender 5.0.1: run after opening the current VoidKnight.blend. Default reads adjacent source.
"""
import bpy,bmesh,math,json,hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np
HERE=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(HERE/'VoidKnight.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='REST'
def digest(o):return hashlib.sha256(np.array([list(v.co) for v in o.data.vertices],dtype=np.float32).tobytes()+str([(tuple(p.vertices),p.material_index) for p in o.data.polygons]).encode()).hexdigest()
body={o.name:digest(o) for o in bpy.data.objects if o.type=='MESH' and o.get('bone')!='WeaponGripR'}
actions={a.name:[(f.data_path,f.array_index,[(tuple(p.co),p.interpolation) for p in f.keyframe_points]) for layer in a.layers for st in layer.strips for slot in a.slots for f in (st.channelbag(slot).fcurves if st.channelbag(slot) else [])] for a in bpy.data.actions}
bones={b.name:(tuple(b.head_local),tuple(b.tail_local),b.parent.name if b.parent else None) for b in rig.data.bones}
old=[o for o in bpy.data.objects if o.type=='MESH' and o.get('bone')=='WeaponGripR']
oldtris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in old)
grip=Vector(rig['grip_center']);axis=Vector(rig['grip_axis']).normalized()
b=next(o for o in old if 'GreatswordBlade' in o.name or 'VKS_Blade_ForgedFacets' in o.name)
pts=np.array([list(v.co) for v in b.data.vertices]);_,_,vt=np.linalg.svd(pts-pts.mean(axis=0));normal=Vector(vt[-1]);normal*=1 if normal.y<0 else -1
across=normal.cross(axis).normalized();normal=axis.cross(across).normalized()
for o in old:bpy.data.objects.remove(o,do_unlink=True)
black,edge,cloth,energy,core=[bpy.data.materials[n] for n in ['Void black metal','Void bevel metal','Void woven undersuit','Void energy','Void core energy']]
created=[]
def mesh(name,verts,faces,mats,indices=None,bevel=0):
 d=bpy.data.meshes.new(name);d.from_pydata([grip+across*x+normal*y+axis*z for x,y,z in verts],[],faces);d.update()
 bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
 o=bpy.data.objects.new('VKS_'+name,d);bpy.context.collection.objects.link(o);o.parent=rig;o['bone']='WeaponGripR';o['sword_design']='Riftkeeper V5'
 for m in mats:d.materials.append(m)
 if indices:
  for p,i in zip(d.polygons,indices):p.material_index=i
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 if bevel:
  mod=o.modifiers.new('Precision edge chamfer','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 for p in d.polygons:p.use_smooth=False
 uv=d.uv_layers.new(name='SurfaceUV')
 for p in d.polygons:
  for i in p.loop_indices:
   co=d.vertices[d.loops[i].vertex_index].co-grip;uv.data[i].uv=(co.dot(across)*4,co.dot(axis)*4)
 vg=o.vertex_groups.new(name='WeaponGripR');vg.add(list(range(len(d.vertices))),1,'REPLACE')
 mod=o.modifiers.new('Original weapon rig','ARMATURE');mod.object=rig
 created.append(o);return o

def plate(name,poly,y,depth,mat,bevel=.001):
 n=len(poly);return mesh(name,[(x,y,z) for x,z in poly]+[(x,y+depth,z) for x,z in poly],[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],[mat],bevel=bevel)
def ring(name,z,length,rx,ry,mat,sides=12):
 v=[(rx*math.cos(2*math.pi*i/sides),ry*math.sin(2*math.pi*i/sides),h) for h in [z-length/2,z+length/2] for i in range(sides)]
 return mesh(name,v,[tuple(range(sides-1,-1,-1)),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)],[mat])
# A true double-edged section: bright cutting facets, dark shoulders, raised
# steel rails, recessed central channel. Both faces use the same geometry.
stations=[(.205,.045,.022),(.295,.080,.028),(.405,.132,.031),(.505,.129,.031),(.91,.101,.026),(1.15,.072,.021),(1.285,.031,.012),(1.378,.0008,.0008)]
section=[(-1,0),(-.76,.64),(-.30,1),(-.20,.75),(0,.75),(.20,.75),(.30,1),(.76,.64),(1,0),(.76,-.64),(.30,-1),(.20,-.75),(0,-.75),(-.20,-.75),(-.30,-1),(-.76,-.64)]
v=[(x*w,y*t,z) for z,w,t in stations for x,y in section];n=len(section)
f=[tuple(range(n-1,-1,-1))];mi=[0]
for j in range(len(stations)-1):
 for i in range(n):f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i));mi.append(1 if i in [0,7,8,15] else 0)
f.append(tuple(range((len(stations)-1)*n,len(stations)*n)));mi.append(1)
mesh('Blade_ForgedFacets',v,f,[black,edge],mi)
# Violet channel is a solid shallow insert below the forged rails, with an
# articulated taper and a deliberately dark interruption at each clamp.
for side in [-1,1]:
 poly=[(0,.330),(.020,.425),(.018,.64),(.013,.92),(.008,1.14),(0,1.295),(-.008,1.14),(-.013,.92),(-.018,.64),(-.020,.425)]
 def surface(z):
  return float(np.interp(z,[s[0] for s in stations],[s[2] for s in stations]))
 verts=[(x,side*surface(z)*h,z) for h in [.79,.775] for x,z in poly]
 n=len(poly)
 mesh('Recessed_Rift_Channel_'+str(side),verts,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],[energy])
 for k,z in enumerate([.49,.70,.91]):
  # paired machined chevron sigils, solid fitted silver inlay
  for sign in [-1,1]:
   x=sign*(.067-(z-.49)*.045)
   plate('Etched_Sigil_%s_%s_%s'%(side,k,sign),[(x,z),(x+sign*.017,z+.023),(x+sign*.014,z+.031),(x-sign*.003,z+.006)],side*.028,-side*.0015,edge,0)
 # guard at the original hand clearance: separate bevel rim, inset shell and rib.
 for sign in [-1,1]:
  outline=[(sign*.033,.245),(sign*.095,.270),(sign*.170,.224),(sign*.247,.300),(sign*.225,.178),(sign*.140,.154),(sign*.066,.195)]
  plate('Quillon_Rim_%s_%s'%(side,sign),outline,side*.035,-side*.016,edge,.002)
  center=(sign*.140,.224)
  inner=[(center[0]+(x-center[0])*.86,center[1]+(z-center[1])*.78) for x,z in outline]
  plate('Quillon_Obsidian_%s_%s'%(side,sign),inner,side*.040,-side*.010,black,.001)
  stripe=[(sign*.063,.228),(sign*.098,.243),(sign*.166,.204),(sign*.208,.240),(sign*.193,.211),(sign*.165,.187),(sign*.097,.228)]
  plate('Quillon_Inlay_%s_%s'%(side,sign),stripe,side*.041,-side*.002,energy,0)
 # recessed octagonal bezel around a physically faceted rhombus gem
 outline=[(0,.294),(.050,.253),(.062,.218),(.042,.174),(0,.151),(-.042,.174),(-.062,.218),(-.050,.253)]
 plate('Core_Housing_'+str(side),outline,side*.049,-side*.026,black,.002)
 plate('Core_Bezel_'+str(side),[(x*.81,.223+(z-.223)*.81) for x,z in outline],side*.053,-side*.012,edge,.001)
 plate('Core_Recess_'+str(side),[(0,.272),(.035,.224),(0,.171),(-.035,.224)],side*.055,-side*.010,black,0)
 verts=[(0,side*.057,.264),(.025,side*.057,.224),(0,side*.057,.186),(-.025,side*.057,.224),(0,side*.067,.224)]
 mesh('Core_FacetedGem_'+str(side),verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(3,2,1,0)],[core,energy],[1,0,1,0,1])
# Original 0.38m hand span; elliptical leather core and diagonal wrapping.
ring('Grip_LeatherCore',0,.38,.023,.024,cloth,12)
for i in range(12):
 z=-.175+i*.030
 verts=[]
 for dz in [-.004,.004]:
  for j in range(13):
   a=j*2*math.pi/12;verts.append((.0245*math.cos(a),.0255*math.sin(a),z+dz+.012*j/12))
 mesh('Grip_DiagonalWrap_%02d'%i,verts,[(j,j+1,j+14,j+13) for j in range(12)],[black])
for z in [-.187,.186]:ring('Grip_Ferrule_'+str(z),z,.018,.028,.029,edge,12)
# A pommel with a capped six-facet silhouette and an inset violet seam.
levels=[(-.255,.006,.006),(-.241,.027,.025),(-.221,.037,.031),(-.199,.026,.024)]
v=[(rx*math.cos(i*2*math.pi/8),ry*math.sin(i*2*math.pi/8),z) for z,rx,ry in levels for i in range(8)]
f=[tuple(range(7,-1,-1))]+[(j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i) for j in range(3) for i in range(8)]+[tuple(range(24,32))]
mesh('Pommel_Counterweight',v,f,[edge],bevel=.001)
for sign in [-1,1]:plate('Pommel_RiftInset_'+str(sign),[(0,-.244),(.012,-.224),(0,-.205),(-.012,-.224)],sign*.032,-sign*.002,energy,0)
assert body=={o.name:digest(o) for o in bpy.data.objects if o.type=='MESH' and o.get('bone')!='WeaponGripR'}
assert bones=={b.name:(tuple(b.head_local),tuple(b.tail_local),b.parent.name if b.parent else None) for b in rig.data.bones}
assert actions=={a.name:[(f.data_path,f.array_index,[(tuple(p.co),p.interpolation) for p in f.keyframe_points]) for layer in a.layers for st in layer.strips for slot in a.slots for f in (st.channelbag(slot).fcurves if st.channelbag(slot) else [])] for a in bpy.data.actions}
tri=lambda objs:sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs)
report={'design':'Riftkeeper V5','old_weapon_triangles':oldtris,'new_weapon_triangles':tri(created),'hero_triangles':tri([o for o in bpy.data.objects if o.type=='MESH']),'weapon_parts':len(created),'body_meshes_unchanged':len(body),'original_actions_unchanged':len(actions),'original_bones_unchanged':len(bones),'grip_span_m':.38,'length_m':1.633,'guard_width_m':.494}
assert report['hero_triangles']<=35000,report
for o in created:
 assert all(math.isfinite(c) for v in o.data.vertices for c in v.co)
 assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in o.data.vertices)
rig.data.pose_position='POSE';rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight.blend'),compress=True)
(HERE/'Sword-V5').mkdir(exist_ok=True);(HERE/'Sword-V5/validation.json').write_text(json.dumps(report,indent=2))
# Save a separately editable sword in local design coordinates. No lights in runtime source.
rig.data.pose_position='REST';bpy.context.view_layer.update()
for o in list(bpy.data.objects):
 if o not in created:bpy.data.objects.remove(o,do_unlink=True)
for o in created:
 o.modifiers.clear();o.parent=None
 for v in o.data.vertices:
  d=v.co-grip;v.co=(d.dot(across),d.dot(normal),d.dot(axis))
 o.vertex_groups.clear()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Sword-V5/VoidKnight-Riftkeeper.blend'),compress=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=str(HERE/'Sword-V5/VoidKnight-Riftkeeper.glb'),export_format='GLB',use_selection=True)
print('SWORD_V5',json.dumps(report),flush=True)
