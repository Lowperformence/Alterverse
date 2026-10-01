"""Blender-authored, bounds-normalized art parts for the existing Hot Zone rigs.
Run: blender -b --python Tools/HotZoneArt/build_monsters.py
No gameplay collider, pivot, animation or stat is exported or replaced.
"""
import bpy,math,json,random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];RES=ROOT/'Assets/Alterverse/Resources/AuthoredHotZone';ART=ROOT/'ArtSource/HotZoneMonsters'
RES.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
families=['Machine','Skeleton','Xeno','Spider','Ice','Sandstone','Crystal'];shapes=['Box','Sphere','Capsule','Cylinder','Rock'];palette=[(.24,.37,.43),(.53,.56,.58),(.21,.43,.29),(.32,.19,.42),(.33,.70,.85),(.55,.29,.39),(.42,.70,.79)];records=[];sources=[]
def normalize(o):
 lo=Vector([min(v.co[k] for v in o.data.vertices) for k in range(3)]);hi=Vector([max(v.co[k] for v in o.data.vertices) for k in range(3)])
 for v in o.data.vertices:
  for k in range(3):v.co[k]=(v.co[k]-lo[k])/(hi[k]-lo[k])-.5
 o.data.update()
def image(family,index):
 # Procedural material atlas is authored and packed into this Blender source.
 n=256;im=bpy.data.images.new(f'{family}Surface',width=n,height=n);px=[];height=[]
 for y in range(n):
  for x in range(n):
   u=x/n;v=y/n;grain=.012*math.sin(x*13.19+y*7.13)
   if family in ['Machine','Skeleton']:
    seam=min(x%64,63-x%64,y%64,63-y%64);a=.52 if seam<2 else .76 if seam<5 else .94; a+=.025*math.sin(y*.2)
    if (x%64-10)**2+(y%64-10)**2<7:a=.45
   elif family in ['Xeno','Spider']:
    sx=(x+(16 if (y//32)%2 else 0))%32;sy=y%32;d=((sx-16)/16)**2+((sy-16)/16)**2;a=.72+.25*max(0,1-d);a-=.12 if d>.9 else 0
   else:
    vein=abs(math.sin(u*math.pi*9+math.sin(v*math.pi*4)*.65));a=.76+.20*vein
    if vein<.09:a=.58
   a=max(.1,min(1,a+grain));px.extend((a,a,a,1));height.append(a)
 im.pixels.foreach_set(px);im.filepath_raw=str(RES/f'{family}Surface.png');im.file_format='PNG';im.save();im.pack()
 normal=bpy.data.images.new(f'{family}Normal',width=n,height=n);normal.colorspace_settings.name='Non-Color';normal_px=[]
 for y in range(n):
  for x in range(n):
   dx=height[y*n+(x+1)%n]-height[y*n+(x-1)%n];dy=height[((y+1)%n)*n+x]-height[((y-1)%n)*n+x];v=Vector((-dx*2,-dy*2,1)).normalized();normal_px.extend((v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1))
 normal.pixels.foreach_set(normal_px);normal.filepath_raw=str(RES/f'{family}Normal.png');normal.file_format='PNG';normal.save();normal.pack();return im
for fi,family in enumerate(families):
 im=image(family,fi);mat=bpy.data.materials.new(f'{family}AuthoredSurface');mat.diffuse_color=(*palette[fi],1);mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*palette[fi],1);bs.inputs['Metallic'].default_value=.7 if fi<2 else .15;bs.inputs['Roughness'].default_value=.28 if fi in [0,1,4,6] else .48
 tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;mix=mat.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*palette[fi],1);mat.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);mat.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
 bump=mat.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.015;mat.node_tree.links.new(tex.outputs['Color'],bump.inputs['Height']);mat.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
 for si,shape in enumerate(shapes):
  if shape=='Box':
   bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;b=o.modifiers.new('Machined bevels','BEVEL');b.width=.075 if fi<2 else .12;b.segments=2;bpy.ops.object.modifier_apply(modifier=b.name)
  elif shape=='Rock':bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.5);o=bpy.context.object
  elif shape=='Cylinder':
   bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=.5,depth=1);o=bpy.context.object;b=o.modifiers.new('Joint edge bevel','BEVEL');b.width=.07;b.segments=2;bpy.ops.object.modifier_apply(modifier=b.name)
  else:
   bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=.5);o=bpy.context.object
   if shape=='Capsule':
    for vertex in o.data.vertices:vertex.co.z=vertex.co.z*.65+(.175 if vertex.co.z>0 else -.175 if vertex.co.z<0 else 0)
  if shape in ['Sphere','Capsule','Rock']:
   for vertex in o.data.vertices:
    p=vertex.co;amount=.018 if fi<2 else .035 if fi in [2,3] else .055;wave=math.sin(p.x*18+fi)*math.cos(p.y*15)*math.sin(p.z*14+si);vertex.co*=1+amount*wave
  o.name=f'{family}_{shape}';normalize(o)
  # Hard mineral facets, smooth anatomical shells, sharp beveled machinery.
  for p in o.data.polygons:p.use_smooth=shape in ['Sphere','Capsule','Cylinder'] and fi not in [4,5,6]
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
  o.data.materials.append(mat);o.data.calc_loop_triangles();uv=o.data.uv_layers.active.data;vertices=[];normals=[];uvs=[];triangles=[]
  for tri in o.data.loop_triangles:
   start=len(vertices)//3
   for li in tri.loops:
    loop=o.data.loops[li];v=o.data.vertices[loop.vertex_index];n=v.normal if o.data.polygons[tri.polygon_index].use_smooth else tri.normal
    vertices.extend(round(a,7) for a in (v.co.x,v.co.z,v.co.y));normals.extend(round(a,7) for a in (n.x,n.z,n.y));uvs.extend(round(a,7) for a in uv[li].uv)
   triangles.extend([start,start+2,start+1])
  records.append(dict(name=o.name,vertices=vertices,normals=normals,uv=uvs,triangles=triangles));sources.append(o);o.location=(si*1.7,fi*1.65,0)
# Barycentric fan is used on existing custom plates: every original edge stays intact.
profile={'inset':.008,'barycentric':[1/3,1/3,1/3]}
(RES/'Parts.json').write_text(json.dumps({'version':1,'parts':records,'surfaceInset':profile},separators=(',',':')))
report=[]
for row in records:
 v=row['vertices'];bounds=[[min(v[k::3]),max(v[k::3])] for k in range(3)];assert all(abs(a+.5)<1e-6 and abs(b-.5)<1e-6 for a,b in bounds);assert len(v)==len(row['normals']);assert len(row['uv'])==len(v)//3*2;report.append({'part':row['name'],'triangles':len(row['triangles'])//3,'bounds':bounds})
(ART/'parts-validation.json').write_text(json.dumps(report,indent=2))
# Blender library / neutral lighting overview, never presented as an in-game screenshot.
bpy.ops.object.select_all(action='DESELECT')
for o in sources:o.select_set(True)
bpy.context.view_layer.objects.active=sources[0];bpy.ops.export_scene.fbx(filepath=str(ART/'HotZoneParts.fbx'),use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
bpy.ops.object.camera_add(location=(11,-12,15));cam=bpy.context.object;target=Vector((3.4,4.7,0));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=15;bpy.context.scene.camera=cam
for loc,power,size in [((3,-4,12),2200,8),((-4,8,7),1800,7),((10,10,10),1800,6)]:
 bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.render.resolution_x=1200;s.render.resolution_y=1200;s.render.resolution_percentage=100;s.world.color=(.08,.08,.08);s.render.image_settings.file_format='PNG';s.render.filepath=str(ART/'PartsPreview.png');bpy.ops.wm.save_as_mainfile(filepath=str(ART/'HotZoneParts.blend'));bpy.ops.render.render(write_still=True);print('HOTZONE_ART_COMPLETE',len(records))
