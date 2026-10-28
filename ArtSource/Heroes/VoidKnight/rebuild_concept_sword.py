"""V6 concept-matched greatsword. Only WeaponGripR-bound geometry changes; body/rig/actions stay intact.
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
b=next(o for o in old if 'GreatswordBlade' in o.name or 'VKS_Blade_ForgedFacets' in o.name or 'VKC_Blade_Hull' in o.name)
pts=np.array([list(v.co) for v in b.data.vertices]);_,_,vt=np.linalg.svd(pts-pts.mean(axis=0));normal=Vector(vt[-1]);normal*=1 if normal.y<0 else -1
across=normal.cross(axis).normalized();normal=axis.cross(across).normalized()
for o in old:bpy.data.objects.remove(o,do_unlink=True)
black,edge,cloth,energy,core=[bpy.data.materials[n] for n in ['Void black metal','Void bevel metal','Void woven undersuit','Void energy','Void core energy']]
created=[]
def mesh(name,verts,faces,mats,indices=None,bevel=0):
 d=bpy.data.meshes.new(name);d.from_pydata([grip+across*x+normal*y+axis*z for x,y,z in verts],[],faces);d.update()
 bm=bmesh.new();bm.from_mesh(d);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(d);bm.free()
 o=bpy.data.objects.new('VKC_'+name,d);bpy.context.collection.objects.link(o);o.parent=rig;o['bone']='WeaponGripR';o['sword_design']='Concept edge V6'
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

# Concept silhouette: long near-parallel plate with angled root shoulders and
# a deliberate distal point. No luminous central stripe or wing crossguard.
stations=[(.208,.067,.029),(.288,.116,.034),(.370,.141,.034),(.850,.137,.031),(1.130,.117,.028),(1.290,.057,.019),(1.378,.0008,.001)]
# Multiple real longitudinal facets around a quiet dark central panel.
section=[(-1,0),(-.94,.33),(-.82,.64),(-.73,.77),(-.60,.89),(-.53,1),(-.42,.91),(0,.88),(.42,.91),(.53,1),(.60,.89),(.73,.77),(.82,.64),(.94,.33),(1,0),(.94,-.33),(.82,-.64),(.73,-.77),(.60,-.89),(.53,-1),(.42,-.91),(0,-.88),(-.42,-.91),(-.53,-1),(-.60,-.89),(-.73,-.77),(-.82,-.64),(-.94,-.33)]
n=len(section);verts=[(x*w,y*t,z) for z,w,t in stations for x,y in section]
faces=[tuple(range(n-1,-1,-1))];indices=[0]
for j in range(len(stations)-1):
 for i in range(n):
  faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i));indices.append(1 if i in [0,13,14,27] else 0)
faces.append(tuple(range((len(stations)-1)*n,len(stations)*n)));indices.append(1)
mesh('Blade_Hull',verts,faces,[black,edge],indices)
# The reference's violet belongs to BOTH cutting edges. Physical inlay bands
# follow the chamfer taper on both faces; all inserts are attached to the hull.
for face in [-1,1]:
 for edge_sign in [-1,1]:
  v=[]
  for z,w,t in stations:
   for u,h in [(.935,.353),(.825,.655)]:v.append((edge_sign*w*u,face*t*h,z))
  mesh('Violet_CuttingEdge_%s_%s'%(face,edge_sign),v,[(j*2,j*2+1,(j+1)*2+1,(j+1)*2) for j in range(len(stations)-1)],[energy])
 # A darker fitted raised central armor panel, broken into actual layered steps
 # and flanked by machined metal rails, rather than a glowing fuller.
 for i,(z0,z1,half,y) in enumerate([(.306,.434,.056,.034),(.440,.812,.054,.031),(.818,1.108,.046,.028)]):
  poly=[(-half,z0),(-half*.78,z0-.015),(half*.78,z0-.015),(half,z0),(half*.90,z1),(0,z1+.019),(-half*.90,z1)]
  plate('Spine_Panel_%s_%s'%(face,i),poly,face*y,-face*.003,black,.001)
 for i,z in enumerate([.445,.673,.905]):
  # Tiny flush hardware either side of the spine. No floating ornaments.
  for sg in [-1,1]:
   x=sg*(.073-(z-.445)*.029)
   plate('Spine_SilverKey_%s_%s_%s'%(face,i,sg),[(x-.004,z),(x+.004,z),(x+.004,z+.017),(x-.004,z+.017)],face*.031,-face*.002,edge,.0005)
# Compact technical crosspiece with swept shoulder blocks and TWO circular
# violet sockets, matching the round energy nodes visible in the supplied art.
plate('Guard_MainChassis',[(-.082,.169),(-.194,.187),(-.194,.236),(-.135,.277),(-.071,.270),(0,.251),(.071,.270),(.135,.277),(.194,.236),(.194,.187),(.082,.169)],-.033,.066,black,.003)
for face in [-1,1]:
 for sg in [-1,1]:
  p=[(sg*x,z) for x,z in [(.067,.215),(.105,.264),(.171,.265),(.194,.239),(.177,.196),(.112,.184)]]
  plate('Guard_SocketArmor_%s_%s'%(face,sg),p,face*.041,-face*.018,edge,.002)
  cx=sg*.133;cz=.227
  # Front-facing cylindrical metal socket with 12 sides and recessed facets.
  def disc(name,r0,r1,y0,y1,mat,sides=12):
   vv=[(cx+r*math.cos(i*2*math.pi/sides),face*y,cz+r*math.sin(i*2*math.pi/sides)) for r,y in [(r0,y0),(r1,y1)] for i in range(sides)]
   ff=[tuple(range(sides-1,-1,-1)),tuple(range(sides,2*sides))]+[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
   return mesh(name,vv,ff,[mat])
  disc('Guard_RingRim_%s_%s'%(face,sg),.032,.030,.044,.050,black)
  disc('Guard_SocketBevel_%s_%s'%(face,sg),.025,.021,.0505,.053,edge)
  disc('Guard_Recess_%s_%s'%(face,sg),.018,.018,.0535,.055,black)
  disc('Guard_VioletCore_%s_%s'%(face,sg),.0138,.009,.0555,.058,core)
  # Two short violet marks continue the edge energy into the technical guard.
  plate('Guard_EnergyReturn_%s_%s'%(face,sg),[(sg*.069,.236),(sg*.088,.253),(sg*.098,.248),(sg*.079,.228)],face*.043,-face*.002,energy,0)
 # Faceted protective center between the twin emitters.
 plate('Guard_CentralBridge_'+str(face),[(-.063,.173),(-.072,.227),(-.039,.278),(.039,.278),(.072,.227),(.063,.173)],face*.041,-face*.017,black,.002)
 plate('Guard_CentralBadge_'+str(face),[(-.021,.242),(.021,.242),(.026,.222),(0,.208),(-.026,.222)],face*.044,-face*.003,edge,.0008)
# Black, substantial two-handed handle. Original grip frame and hand span remain.
ring('Grip_TacticalCore',0,.38,.026,.027,cloth,12)
for i in range(11):
 z=-.172+i*.032
 ring('Grip_DarkWrap_%02d'%i,z,.010,.027,.028,black,12)
for z in [-.187,.186]:ring('Grip_LockingCollar_'+str(z),z,.022,.033,.034,edge,12)
# Blunt angular counterweight, small inset; no ornate jeweled medieval pommel.
levels=[(-.255,.019,.017),(-.243,.031,.027),(-.218,.033,.029),(-.199,.027,.025)]
v=[(rx*math.cos(i*2*math.pi/8),ry*math.sin(i*2*math.pi/8),z) for z,rx,ry in levels for i in range(8)]
f=[tuple(range(7,-1,-1))]+[(j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i) for j in range(3) for i in range(8)]+[tuple(range(24,32))]
mesh('Pommel_ArmoredCap',v,f,[black],bevel=.001)
for face in [-1,1]:plate('Pommel_VioletIndicator_'+str(face),[(-.010,-.240),(.010,-.240),(.010,-.228),(-.010,-.228)],face*.029,-face*.002,energy,0)

assert body=={o.name:digest(o) for o in bpy.data.objects if o.type=='MESH' and o.get('bone')!='WeaponGripR'}
assert bones=={b.name:(tuple(b.head_local),tuple(b.tail_local),b.parent.name if b.parent else None) for b in rig.data.bones}
assert actions=={a.name:[(f.data_path,f.array_index,[(tuple(p.co),p.interpolation) for p in f.keyframe_points]) for layer in a.layers for st in layer.strips for slot in a.slots for f in (st.channelbag(slot).fcurves if st.channelbag(slot) else [])] for a in bpy.data.actions}
tri=lambda objs:sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs)
report={'design':'Concept edge V6','old_weapon_triangles':oldtris,'new_weapon_triangles':tri(created),'hero_triangles':tri([o for o in bpy.data.objects if o.type=='MESH']),'weapon_parts':len(created),'body_meshes_unchanged':len(body),'original_actions_unchanged':len(actions),'original_bones_unchanged':len(bones),'grip_span_m':.38,'length_m':1.633,'reference_visible_features':['broad parallel blade','violet outer cutting edges','dark longitudinal spine','compact angular hilt','round energy sockets'],'unseen_tip':'inferred; screenshot crops the blade tip','guard_width_m':.388}
assert report['hero_triangles']<=35000,report
for o in created:
 assert all(math.isfinite(c) for v in o.data.vertices for c in v.co)
 assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in o.data.vertices)
rig.data.pose_position='POSE';rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight.blend'),compress=True)
(HERE/'Sword-V6').mkdir(exist_ok=True);(HERE/'Sword-V6/validation.json').write_text(json.dumps(report,indent=2))
# Save a separately editable sword in local design coordinates. No lights in runtime source.
rig.data.pose_position='REST';bpy.context.view_layer.update()
for o in list(bpy.data.objects):
 if o not in created:bpy.data.objects.remove(o,do_unlink=True)
for o in created:
 o.modifiers.clear();o.parent=None
 for v in o.data.vertices:
  d=v.co-grip;v.co=(d.dot(across),d.dot(normal),d.dot(axis))
 o.vertex_groups.clear()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Sword-V6/VoidKnight-Conceptschwert.blend'),compress=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=str(HERE/'Sword-V6/VoidKnight-Conceptschwert.glb'),export_format='GLB',use_selection=True)
print('SWORD_V6',json.dumps(report),flush=True)
