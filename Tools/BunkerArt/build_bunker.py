"""Blender detail pass for the live DefenseBunker, preserving its gameplay collider and turret."""
import bpy,math,json,uuid
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/Alterverse/Resources/AuthoredBunker';ART=ROOT/'ArtSource/Bunker';OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);parts=[];materials=[]
def U(p):return (p[0],p[2],p[1])
for name,color,metal,rough in [('Concrete',(.43,.455,.48),.03,.8),('Metal',(.105,.125,.145),.66,.42),('Recess',(.018,.023,.032),.12,.9),('Trim',(.18,.23,.27),.72,.35)]:
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough;materials.append(m)
def tag(o,name,material=0,target='',mount='body'):
 o.name=name;o.data.materials.clear();o.data.materials.append(materials[material]);o['target']=target;o['mount']=mount;o['material']=material;parts.append(o);return o
def cube(name,pos,size,material=0,bevel=.018,target='',mount='body'):
 bpy.ops.mesh.primitive_cube_add(size=1,location=U(pos));o=bpy.context.object;o.scale=U(size);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 if bevel:
  b=o.modifiers.new('Rounded manufactured edges','BEVEL');b.width=bevel;b.segments=2;bpy.ops.object.modifier_apply(modifier=b.name)
 return tag(o,name,material,target,mount)
def prism(name,bottom,top,height,corner,target,preview):
 def ring(w,d,z):
  a=w/2;b=d/2;c=corner
  return [(a-c,b,z),(-a+c,b,z),(-a,b-c,z),(-a,-b+c,z),(-a+c,-b,z),(a-c,-b,z),(a,-b+c,z),(a,b-c,z)]
 verts=ring(*bottom,0)+ring(*top,height);faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
 b=o.modifiers.new('Concrete edge finishing','BEVEL');b.width=.035;b.segments=2;bpy.ops.object.modifier_apply(modifier=b.name);tag(o,name,0,target);o['preview']=preview;return o
def cut(o,pos,size):
 cutter=cube('Temporary recess tool',pos,size,2,.022);parts.remove(cutter);bpy.context.view_layer.objects.active=o;b=o.modifiers.new('Cast-in recessed panel','BOOLEAN');b.operation='DIFFERENCE';b.solver='EXACT';b.object=cutter;bpy.ops.object.modifier_apply(modifier=b.name);bpy.data.objects.remove(cutter,do_unlink=True)
def bolt(pos,r=.038,mount='body',axis='Y'):
 bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=r,depth=.035,location=U(pos));o=bpy.context.object
 if axis=='Z':o.rotation_euler.x=math.pi/2
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);return tag(o,'Hex fastener',3,mount=mount)
shell=prism('Cast concrete shell',(7.95,6.58),(7.38,6.02),1.32,.56,'DF_Bunker_MainShell',[0,.34,-.02])
# Rear blast-door recess and paired side maintenance panels, never through the shell.
cut(shell,(0,.57,-3.24),(1.62,1.04,.32))
for side in [-1,1]:
 for z in [-1.65,.45]:cut(shell,(side*3.83,.62,z),(.20,.56,1.16))
roof=prism('Chamfered reinforced roof',(8.62,7.18),(8.62,7.18),.48,.72,'DF_Bunker_RoofArmor',[0,1.69,-.08])
foundation=prism('Chamfered foundation',(8.45,7.05),(8.45,7.05),.48,.64,'DF_Bunker_Foundation',[0,.03,0])
# Blast-door assembly sits in the authored rear opening.
cube('Inset blast door',(0,.91,-3.125),(1.42,.92,.055),1,.025)
for x in [-.765,.765]:cube('Reinforced door jamb',(x,.91,-3.215),(.10,1.11,.11),3,.02)
for y in [.37,1.44]:cube('Door frame lintel',(0,y,-3.215),(1.62,.095,.11),3,.018)
for y in [.60,.91,1.22]:cube('Blast door reinforcing rib',(0,y,-3.177),(1.36,.05,.05),3,.012)
cube('Door split',(0,.91,-3.18),(.025,.96,.025),2,.003)
cube('Access reader housing',(.99,.96,-3.195),(.25,.35,.13),1,.022)
cube('Access reader screen',(.99,1.02,-3.267),(.16,.13,.016),2,.005)
for x in [-.61,.61]:
 for y in [.49,1.32]:bolt((x,y,-3.19),.038,axis='Z')
# Cast panel joints and recessed covers on both flanks.
for side in [-1,1]:
 for z in [-1.65,.45]:
  cube('Inset side access panel',(side*3.748,.96,z-.02),(.045,.43,.99),1,.015)
  for yy in [.80,.96,1.12]:cube('Horizontal flank louvre',(side*3.79,yy,z-.02),(.045,.035,.82),3,.008)
 # Roof edge expansion joints stay inside the existing building footprint.
 for z in [-2.2,-.7,.8,2.3]:
  cube('Roof cast joint',(side*3.94,2.173,z),(.06,.018,.58),2,.006)
  # Corner bolts are authored on the raised roof panels below.
# Framed service hatch with hinges and grip, alongside existing hatch.
for x in [-1.93,-.83]:cube('Hatch perimeter long',(x,2.228,-.18),(.045,.035,.80),3,.012)
for z in [-.56,.20]:cube('Hatch perimeter short',(-1.38,2.228,z),(1.13,.035,.045),3,.012)
for x in [-1.7,-1.06]:cube('Hatch hinge',(x,2.255,.21),(.16,.07,.09),1,.016)
cube('Hatch handle',(-1.38,2.27,-.23),(.28,.06,.055),3,.018)
# Fine grilles above the seven existing ventilation stacks.
for center,count in [((-2.48,2.20,-.66),3),((2.35,2.20,-.74),4)]:
 for i in range(count):
  x=center[0]+(-.34 if i%2==0 else .34);z=center[2]+(-.27 if i//2==0 else .27)
  for dx in [-.065,0,.065]:cube('Vent grille',(x+dx,2.436,z),(.019,.016,.16),3,.003)
# Roof plate tie-downs are deliberately larger than the concrete pores.
for x,z,top in [(-2.86,2.18,2.16),(0,2.34,2.19),(2.86,2.18,2.16),(-3.02,-2.20,2.14),(0,-2.42,2.17),(3.02,-2.20,2.14)]:
 for dx in [-.55,.55]:
  for dz in [-.35,.35]:bolt((x+dx,top+.14+.012,z+dz),.045)
# A small extra mesh stays parented to the original rotating turret.
for x in [-.43,.43]:
 cube('Turret side cooling block',(x,.23,.04),(.12,.24,.57),3,.018,mount='turret')
 for z in [-.14,-.02,.10,.22]:cube('Turret radiator rib',(x,.37,z),(.12,.035,.027),1,.005,mount='turret')
# Author tileable concrete pores and machined-metal normal data in Blender.
for index,name in enumerate(['Concrete','Metal']):
 n=256;heights=[];pixels=[]
 for y in range(n):
  for x in range(n):
   if index==0:
    grain=math.sin(x*12.989+y*78.233)*43758.5453;grain-=math.floor(grain);pores=max(0,(grain-.87)/.13);h=.5-pores*.10+math.sin(x*.37)*math.sin(y*.41)*.008;a=.94-pores*.16
   else:
    h=.5+math.sin(y*1.8)*.004+math.sin(x*.13+y*.03)*.006;a=.94+math.sin(y*1.8)*.025
   heights.append(h);pixels.extend((a,a,a,1))
 tex=bpy.data.images.new(name+'Surface',width=n,height=n);tex.pixels.foreach_set(pixels);tex.filepath_raw=str(OUT/f'{name}Surface.png');tex.file_format='PNG';tex.save();tex.pack()
 normal=bpy.data.images.new(name+'Normal',width=n,height=n);normal.colorspace_settings.name='Non-Color';normal_pixels=[]
 for y in range(n):
  for x in range(n):
   dx=heights[y*n+(x+1)%n]-heights[y*n+(x-1)%n];dy=heights[((y+1)%n)*n+x]-heights[((y-1)%n)*n+x];v=Vector((-dx*2,-dy*2,1)).normalized();normal_pixels.extend((v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1))
 normal.pixels.foreach_set(normal_pixels);normal.filepath_raw=str(OUT/f'{name}Normal.png');normal.file_format='PNG';normal.save();normal.pack()
 nodes=materials[index].node_tree.nodes;links=materials[index].node_tree.links;bs=nodes.get('Principled BSDF');it=nodes.new('ShaderNodeTexImage');it.image=normal;nm=nodes.new('ShaderNodeNormalMap');links.new(it.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal']);nm.inputs['Strength'].default_value=.35
# Aggregate added geometry by material and mount; retain named replacements.
groups={}
for obj in parts:
 key=(obj['target'],obj['mount'],obj['material']);groups.setdefault(key,[]).append(obj)
records=[];exports=[]
for (target,mount,material),objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 o=objects[0];bpy.context.view_layer.objects.active=o
 if len(objects)>1:bpy.ops.object.join()
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
 o.data.calc_loop_triangles();vertices=[];normals=[];uv=[];indices=[]
 for tri in o.data.loop_triangles:
  start=len(vertices)//3
  for li in tri.loops:
   p=o.data.vertices[o.data.loops[li].vertex_index].co;n=tri.normal;vertices.extend(round(a,7) for a in (p.x,p.z,p.y));normals.extend(round(a,7) for a in (n.x,n.z,n.y));uv.extend(round(a,7) for a in o.data.uv_layers.active.data[li].uv)
  indices.extend([start,start+2,start+1])
 records.append({'name':target or f'Detail_{mount}_{material}','target':target,'mount':mount,'material':material,'vertices':vertices,'normals':normals,'uv':uv,'triangles':indices})
 if target:o.location=U(o['preview'])
 elif mount=='turret':o.location=U((0,2.38,.22))
 exports.append(o)
(OUT/'BunkerDetails.json').write_text(json.dumps({'version':1,'roofPanelLift':.14,'parts':records,'hide':['DF_Bunker_RearTechPanel','DF_Bunker_RearStatus']},separators=(',',':')))
(ART/'manifest.json').write_text(json.dumps({'parts':[{'name':r['name'],'triangles':len(r['triangles'])//3} for r in records],'gameplayColliderChanged':False,'turretAndMuzzleChanged':False},indent=2))
# Studio support geometry approximates retained live pieces; tagged as preview-only.
for name,pos,size,mat in [('Front buttress left',(-2.55,.69,3.36),(1.20,1.72,1.62),0),('Front buttress center',(0,.69,3.36),(1.20,1.72,1.62),0),('Front buttress right',(2.55,.69,3.36),(1.20,1.72,1.62),0),('Turret retained',(0,2.55,.27),(1.18,.38,.78),1)]:cube('PREVIEW_ONLY '+name,pos,size,mat,.04)
for pos,size in [((-2.86,2.20,2.18),(1.82,.20,1.48)),((0,2.22,2.34),(2.25,.22,1.18)),((2.86,2.20,2.18),(1.82,.20,1.48)),((-3.02,2.19,-2.20),(1.68,.18,1.30)),((0,2.21,-2.42),(2.05,.20,1.05)),((3.02,2.19,-2.20),(1.68,.18,1.30)),((-3.23,2.18,0),(1.08,.18,1.58)),((3.23,2.18,0),(1.08,.18,1.58))]:cube('PREVIEW_ONLY raised original roof plate',pos,size,0,.025)
for center,count in [((-2.48,2.20,-.66),3),((2.35,2.20,-.74),4)]:
 for i in range(count):
  x=center[0]+(-.34 if i%2==0 else .34);z=center[2]+(-.27 if i//2==0 else .27)
  bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.155,depth=.20,location=U((x,2.28,z)));o=bpy.context.object;o.name='PREVIEW_ONLY original vent collar';o.data.materials.append(materials[1])
  bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.103,depth=.07,location=U((x,2.395,z)));o=bpy.context.object;o.name='PREVIEW_ONLY original vent recess';o.data.materials.append(materials[2])
for x in [-.22,.22]:cube('PREVIEW_ONLY barrel',(x,2.55,.97),(.12,.12,.66),1,.022)
bpy.ops.object.select_all(action='DESELECT')
for o in exports:o.select_set(True)
bpy.context.view_layer.objects.active=exports[0];bpy.ops.export_scene.fbx(filepath=str(ART/'BunkerDetails.fbx'),use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
bpy.ops.object.camera_add(location=(11,14,11));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=13.5;scene=bpy.context.scene;scene.camera=cam
for loc,power,size in [((3,5,12),2200,8),((-7,-4,7),1700,7),((5,-8,9),2000,6)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.10,.14,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ART/'BunkerPreview.png');bpy.ops.wm.save_as_mainfile(filepath=str(ART/'BunkerDetails.blend'),compress=True);bpy.ops.render.render(write_still=True);cam.location=(-10,-13,8);cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(ART/'BunkerRearPreview.png');bpy.ops.render.render(write_still=True);print('BUNKER_AUTHORED',len(records),sum(len(r['triangles'])//3 for r in records),flush=True)
