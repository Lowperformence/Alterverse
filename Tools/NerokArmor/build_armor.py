"""Nerok armor only: original modular plates; no replacement character geometry.
Run in Blender 4+: blender -b --python Tools/NerokArmor/build_armor.py
Skeleton coordinates are the existing Alterverse attachment contract, not geometry.
"""
import bpy, bmesh, math, json, gzip, sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/NerokArmor'; ART.mkdir(parents=True,exist_ok=True)
OUT=ROOT/'Assets/Alterverse/Resources/CyberShark'; OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
BONES={'Hips':((0,.981,0),(0,1.1227,0)), 'Spine':((0,1.1227,0),(0,1.308,0)), 'Chest':((0,1.308,0),(0,1.5478,0)), 'Head':((0,1.5478,0),(0,1.9075,0))}
for side,s in [('L',-1),('R',1)]:
 BONES['UpperArm'+side]=((s*.16465,1.54453,-.01564),(s*.24831,1.2644,-.02116))
 BONES['Forearm'+side]=(BONES['UpperArm'+side][1],(s*.33553,1.03332,.02576))
 BONES['Hand'+side]=(BONES['Forearm'+side][1],(s*.35155,.9374,.04784))
 BONES['Thigh'+side]=((s*.1248,.981,0),(s*.1344,.5341,-.01))
 BONES['Shin'+side]=(BONES['Thigh'+side][1],(s*.1728,.1308,-.02))
 BONES['Foot'+side]=(BONES['Shin'+side][1],(s*.1728,.0654,.15))
 BONES['WeaponGrip'+side]=BONES['Hand'+side]
BN=list(BONES); parts=[]; registry=[]
PALETTE=[('Forged naval steel',(.06,.085,.10),.8,.34),('Saltwater ceramic',(.22,.38,.38),.35,.40),('Brushed copper',(.49,.23,.105),.75,.32),('Brown leather straps',(.11,.047,.025),0,.76),('Pale rivet metal',(.56,.60,.56),.7,.34),('Deep crimson braid',(.27,.032,.026),0,.75)]
mats=[]
for name,col,metal,rough in PALETTE:
 m=bpy.data.materials.new('DF_NerokArmor_'+name); m.use_nodes=True;m.diffuse_color=(*col,1)
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*col,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 noise=m.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=95 if metal==0 else 42
 bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.13;bump.inputs['Distance'].default_value=.0016
 m.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal']);mats.append(m)
def obj(name,vs,fs,mat,bone='Chest',group=0,blend=None):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
 o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);o.data.materials.append(mats[mat]);o['bone']=bone;o['materialSlot']=mat;o['group']=group
 for p in me.polygons:p.use_smooth=True
 o['source']='New analytic geometry authored for Nerok 2026-10-12';parts.append(o)
 registry.append({'name':name,'bone':bone,'group':group,'material':mat,'vertices':len(vs),'polygons':len(fs)})
 o.vertex_groups.new(name=bone).add(list(range(len(vs))),1,'REPLACE')
 if blend:
  for i,secondary,w in blend:
   if not o.vertex_groups.get(secondary):o.vertex_groups.new(name=secondary)
   o.vertex_groups[bone].add([i],1-w,'REPLACE');o.vertex_groups[secondary].add([i],w,'REPLACE')
 return o
def ell(name,c,scale,mat,bone='Chest',group=0,n=28,r=16):
 vs=[(c[0],c[1]+scale[1],c[2])];fs=[]
 for i in range(1,r):
  a=math.pi*i/r
  for j in range(n):
   b=math.tau*j/n;vs.append((c[0]+scale[0]*math.sin(a)*math.cos(b),c[1]+scale[1]*math.cos(a),c[2]+scale[2]*math.sin(a)*math.sin(b)))
 vs.append((c[0],c[1]-scale[1],c[2]));end=len(vs)-1
 for j in range(n):fs.append((0,1+(j+1)%n,1+j))
 for i in range(r-2):
  for j in range(n):a=1+i*n+j;b=1+i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 for j in range(n):fs.append((end,end-1-j,end-1-(j+1)%n))
 for i,f in enumerate(fs):
  a,b,d=[Vector(vs[k]) for k in f[:3]]
  if (b-a).cross(d-a).dot((a+b+d)/3-Vector(c))<0:fs[i]=tuple(reversed(f))
 return obj(name,vs,fs,mat,bone,group)
def tube(name,pts,rs,mat,bone='Chest',group=0,n=16):
 ps=list(map(Vector,pts));vs=[];fs=[];rs=[rs]*len(ps) if isinstance(rs,(float,int)) else rs
 for i,p in enumerate(ps):
  t=(ps[min(i+1,len(ps)-1)]-ps[max(i-1,0)]).normalized();u=t.cross(Vector((0,0,1)))
  if u.length<.01:u=t.cross(Vector((1,0,0)))
  u.normalize();v=t.cross(u)
  for j in range(n):vs.append(p+rs[i]*(u*math.cos(math.tau*j/n)+v*math.sin(math.tau*j/n)))
 for i in range(len(ps)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 fs += [tuple(reversed(range(n))),tuple((len(ps)-1)*n+j for j in range(n))]
 return obj(name,vs,fs,mat,bone,group)
def plate(name,outline,z,depth,mat,bone='Chest',group=0):
 n=len(outline);vs=[(x,y,z) for x,y in outline]+[(x,y,z-depth) for x,y in outline];fs=[tuple(range(n)),tuple(reversed(range(n,2*n)))]
 for i in range(n):j=(i+1)%n;fs.append((i,j,n+j,n+i))
 o=obj(name,vs,fs,mat,bone,group);b=o.modifiers.new('Rounded manufactured edges','BEVEL');b.width=.008;b.segments=2
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=b.name);return o
def loft(name,rows,mat,bone='Chest',n=36):
 vs=[];fs=[]
 for y,rx,rz,z in rows:
  for j in range(n):a=math.tau*j/n;vs.append((rx*math.cos(a),y,z+rz*math.sin(a)))
 for i in range(len(rows)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 fs += [tuple(reversed(range(n))),tuple((len(rows)-1)*n+j for j in range(n))]
 return obj(name,vs,fs,mat,bone)
def ring(name,c,rad,minor,mat,bone='Chest',axis='y',group=0,n=40,k=8):
 vs=[];fs=[]
 for i in range(n):
  a=math.tau*i/n
  for j in range(k):
   b=math.tau*j/k;u=(rad+minor*math.cos(b))*math.cos(a);v=(rad+minor*math.cos(b))*math.sin(a);w=minor*math.sin(b)
   q=(u,w,v) if axis=='y' else (u,v,w) if axis=='z' else (w,u,v);vs.append(tuple(c[t]+q[t] for t in range(3)))
 for i in range(n):
  for j in range(k):a=i*k+j;b=((i+1)%n)*k+j;fs.append((a,b,((i+1)%n)*k+(j+1)%k,i*k+(j+1)%k))
 return obj(name,vs,fs,mat,bone,group)
def rope(name,pts,r=.009,bone='Chest',group=0):
 ps=list(map(Vector,pts))
 # Three actual wound strands, newly generated, not a purchased texture.
 for strand in range(3):
  path=[]
  for i,p in enumerate(ps):
   t=(ps[min(i+1,len(ps)-1)]-ps[max(i-1,0)]).normalized();u=t.cross(Vector((0,1,0)))
   if u.length<.01:u=t.cross(Vector((0,0,1)))
   u.normalize();v=t.cross(u);a=i*.8+strand*math.tau/3;path.append(p+r*.5*(u*math.cos(a)+v*math.sin(a)))
  tube(name+' strand '+str(strand),path,r*.58,3,bone,group,8)
def fin(name,points,width,mat=1,bone='Chest'):
 # Sides oriented explicitly; closed organic fin with beveled edges.
 vs=[(x-width,y,z) for x,y,z in points]+[(x+width,y,z) for x,y,z in points];n=len(points)
 fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]
 for i in range(n):j=(i+1)%n;fs.append((i,j,n+j,n+i))
 return obj(name,vs,fs,mat,bone)

# Newly authored armor over yesterday's anatomy/clothes. No head or weapon meshes.
for j in range(4):
 y=1.48-j*.091;rx=.235-j*.027
 outline=[(-rx,y),(0,y-.047),(rx,y),(rx*.91,y-.045),(0,y-.102),(-rx*.91,y-.045)]
 plate('NA-TORSO-frame-'+str(j),outline,.236-j*.007,.036,0)
 plate('NA-TORSO-inlay-'+str(j),[(x*.91,yy+.004) for x,yy in outline],.246-j*.007,.022,1)
 tube('NA-TORSO-copper-keel-'+str(j),[(-rx*.78,y-.014,.251-j*.007),(0,y-.055,.263-j*.007),(rx*.78,y-.014,.251-j*.007)],.004,2,n=10)
 for s in [-1,1]:
  ell('NA-TORSO-rivet-'+str(j)+'-'+str(s),(s*rx*.87,y-.012,.254-j*.007),(.006,.006,.004),4,n=12,r=8)
# Split side plates permit waist flexion and preserve the tail/dorsal fin opening.
for side,s in [('L',-1),('R',1)]:
 plate('NA-TORSO-side-'+side,[(s*.185,1.47),(s*.262,1.42),(s*.254,1.26),(s*.19,1.28)],.15,.03,0)
 tube('NA-TORSO-side-trim-'+side,[(s*.25,1.43,.16),(s*.25,1.35,.165),(s*.24,1.28,.16)],.005,2)
 for j in range(3):
  y=1.51-j*.051;cx=s*(.205+j*.028);rx=.09-j*.008;z=.137+j*.027
  outline=[(cx-rx,y+.054),(cx+rx,y+.026),(cx+rx*.76,y-.039),(cx-rx*.76,y-.027)]
  plate('NA-SHOULDER-frame-'+side+str(j),outline,z,.034,0,'UpperArm'+side)
  plate('NA-SHOULDER-inlay-'+side+str(j),[(cx+(x-cx)*.82,yy+.003) for x,yy in outline],z+.009,.023,1,'UpperArm'+side)
  for dx in [-rx*.65,rx*.65]:ell('NA-SHOULDER-rivet-'+side+str(j)+str(dx),(cx+dx,y,z+.018),(.006,.006,.004),2,'UpperArm'+side,n=12,r=8)
 a=Vector(BONES['Forearm'+side][0]);w=Vector(BONES['Hand'+side][0]);mid=a.lerp(w,.6)
 x=mid.x;yt=mid.y+.069;yb=mid.y-.065
 outline=[(x-.072,yt),(x+.072,yt),(x+.059,yb),(x,yb-.02),(x-.059,yb)]
 plate('NA-BRACER-frame-'+side,outline,.107,.036,0,'Forearm'+side)
 plate('NA-BRACER-inlay-'+side,[(x+(xx-x)*.8,yy+.003) for xx,yy in outline],.117,.021,1,'Forearm'+side)
 for sign in [-1,1]:
  tube('NA-BRACER-rib-'+side+str(sign),[(x+sign*.025,yt-.015,.129),(x+sign*.022,yb+.015,.129)],.004,2,'Forearm'+side,n=8)
 axis=(w-a).normalized();u=axis.cross(Vector((0,0,1))).normalized();vv=axis.cross(u)
 for t in [.40,.85]:
  c=a.lerp(w,t);pts=[c+.107*(u*math.cos(math.tau*i/32)+vv*math.sin(math.tau*i/32)) for i in range(33)]
  tube('NA-BRACER-strap-'+side+str(t),pts,.007,3,'Forearm'+side,n=8)
 # Segmented knee and greaves, independent of foot/toes and original weapon sockets.
 cx=s*.162
 for j in range(2):
  y=.45-j*.115;rx=.082-j*.008
  outline=[(cx-rx,y),(cx+rx,y),(cx+rx*.8,y-.09),(cx,y-.105),(cx-rx*.8,y-.09)]
  plate('NA-GREAVE-frame-'+side+str(j),outline,.108+j*.005,.035,0,'Shin'+side)
  plate('NA-GREAVE-inlay-'+side+str(j),[(cx+(xx-cx)*.81,yy+.004) for xx,yy in outline],.118+j*.005,.021,1,'Shin'+side)
  for sign in [-1,1]:ell('NA-GREAVE-rivet-'+side+str(j)+str(sign),(cx+sign*rx*.76,y-.019,.128+j*.005),(.006,.006,.004),2,'Shin'+side,n=12,r=8)
 # Narrow thigh tassets ride with thighs, and leave hips and tail unobstructed.
 cx=s*.145;outline=[(cx-.07,.87),(cx+.07,.87),(cx+.06,.77),(cx-.055,.75)]
 plate('NA-TASSET-frame-'+side,outline,.17,.026,0,'Thigh'+side)
 plate('NA-TASSET-inlay-'+side,[(cx+(xx-cx)*.8,yy+.002) for xx,yy in outline],.18,.016,1,'Thigh'+side)
# Back plates split around the existing fin and tail; straps stay on the torso.
for s in [-1,1]:
 plate('NA-BACK-plate-'+str(s),[(s*.07,1.45),(s*.21,1.43),(s*.20,1.26),(s*.07,1.28)],-.155,.026,0)
 tube('NA-BACK-trim-'+str(s),[(s*.09,1.43,-.187),(s*.185,1.40,-.191),(s*.18,1.30,-.19)],.005,2)
 tube('NA-STRAP-shoulder-'+str(s),[(s*.15,1.47,-.18),(s*.15,1.56,-.05),(s*.16,1.49,.17),(s*.18,1.36,.21)],.010,3)
 plate('NA-STRAP-buckle-'+str(s),[(s*.167,1.41),(s*.204,1.41),(s*.204,1.37),(s*.167,1.37)],.224,.01,2)
# Export only armor and an editable rig with the project's existing bone coordinate contract.
armdata=bpy.data.armatures.new('NerokArmorRig');arm=bpy.data.objects.new('NerokArmorRig',armdata);bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name,(h,t) in BONES.items():b=armdata.edit_bones.new(name);b.head=h;b.tail=t
for name in BN:
 parent='Hips' if name=='Spine' else 'Spine' if name=='Chest' else 'Chest' if name=='Head' or name.startswith('UpperArm') else 'UpperArm'+name[-1] if name.startswith('Forearm') else 'Forearm'+name[-1] if name.startswith('Hand') else 'Hand'+name[-1] if name.startswith('WeaponGrip') else 'Hips' if name.startswith('Thigh') else 'Thigh'+name[-1] if name.startswith('Shin') else 'Shin'+name[-1] if name.startswith('Foot') else None
 if parent:armdata.edit_bones[name].parent=armdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');arm.select_set(False)
verts=[];normals=[];bones=[];subs=[{'triangles':[]} for _ in mats];shared={}
for o in parts:
 mod=o.modifiers.new('Armor skin','ARMATURE');mod.object=arm;o.parent=arm
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
 bm=bmesh.from_edit_mesh(o.data);bmesh.ops.dissolve_degenerate(bm,dist=1e-7,edges=list(bm.edges));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.update_edit_mesh(o.data);bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
 me=o.data;me.calc_loop_triangles();me.calc_normals_split()
 for tri in me.loop_triangles:
  a,b,c=[me.vertices[i].co for i in tri.vertices]
  if (b-a).cross(c-a).length<2e-12:continue
  for li in tri.loops:
   vi=me.loops[li].vertex_index;key=(o.name,vi,tuple(round(c,6) for c in me.loops[li].normal))
   if key not in shared:
    shared[key]=len(bones);verts.extend(me.vertices[vi].co);normals.extend(me.loops[li].normal);bones.append(BN.index(o['bone']))
   subs[o['materialSlot']]['triangles'].append(shared[key])
data={'version':1,'boneNames':BN,'anchors':[{'head':BONES[b][0],'tail':BONES[b][1]} for b in BN],'vertices':verts,'normals':normals,'boneIndices':bones,'groups':[0]*len(bones),'submeshes':subs,'tailVertices':[],'mouthVertices':[],'markers':[]}
with gzip.GzipFile(filename=str(OUT/'Armor20261012.bytes'),mode='wb',mtime=0) as f:f.write(json.dumps(data,separators=(',',':')).encode())
(ART/'parts.json').write_text(json.dumps(registry,indent=2))
arm.rotation_euler.x=math.pi/2
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(ART/'NerokArmor.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',object_types={'ARMATURE','MESH'})
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'NerokArmor.blend'),compress=True)
report={'parts':len(parts),'vertices':len(bones),'triangles':sum(len(s['triangles'])//3 for s in subs),'materials':len(mats),'baseBodyReplaced':False,'weaponsReplaced':False,'externalMeshes':0,'externalTextures':0}
(ART/'build-report.json').write_text(json.dumps(report,indent=2));print('ARMOR_COMPLETE',json.dumps(report),flush=True)
