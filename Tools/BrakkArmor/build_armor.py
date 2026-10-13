"""Editable Brakk armor concept and production utility belt; each plate is a layered solid.
Blender 4.x: --python build_armor.py -- --source ArtSource/Heroes/Rook.blend
Run export_runtime.py after this builder to refresh the integrated armor resource.
"""
import argparse,gzip,hashlib,json,math,sys
from pathlib import Path
import bpy,bmesh
from mathutils import Matrix,Vector,Euler

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/BrakkArmor';RES=ROOT/'Assets/Alterverse/Resources/BrakkUtilityBelt'
ART.mkdir(parents=True,exist_ok=True);RES.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser();parser.add_argument('--source',required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
source=Path(args.source).resolve();bpy.ops.wm.open_mainfile(filepath=str(source))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');arm.data.pose_position='REST';bpy.context.view_layer.update()
C=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)));B=C.inverted()
body=bpy.data.objects['DF_Anatomy']
# Use the connected anatomical surface, excluding separate fabric cages/straps.
parent=list(range(len(body.data.vertices)));bodyFaces=[];used=set()
def find(i):
 while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
 return i
def join(a,b):
 a,b=find(a),find(b)
 if a!=b:parent[b]=a
for p in body.data.polygons:
 name=body.data.materials[p.material_index].name.lower()
 if not ('fabric' in name or name.startswith(('skin','flesh'))):continue
 bodyFaces.append(p);used.update(p.vertices)
 for i in p.vertices[1:]:join(p.vertices[0],i)
seams={}
for i in used:
 key=tuple(round(x*100000) for x in body.data.vertices[i].co)
 if key in seams:join(i,seams[key])
 else:seams[key]=i
counts={}
for i in used:counts[find(i)]=counts.get(find(i),0)+1
mainBody=max(counts,key=counts.get)
body.data.calc_loop_triangles()
mainFaces={p.index for p in bodyFaces if find(p.vertices[0])==mainBody}
bodyTriangles=[tuple(C@(body.matrix_world@body.data.vertices[i].co) for i in t.vertices)
               for t in body.data.loop_triangles if t.polygon_index in mainFaces]
palette=[('Graphite',(.030,.040,.047),.38,.44),('Forge gold',(.82,.56,.18),.76,.28),
         ('Dark steel',(.105,.128,.145),.72,.32),('Recess liner',(.008,.012,.016),.02,.64),
         ('Amber status lens',(1,.30,.02),.16,.34)]
mats=[]
for name,color,metal,rough in palette:
 mat=bpy.data.materials.new('BRK '+name);mat.diffuse_color=(*color,1);mat.use_nodes=True
 bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
 bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 if name=='Amber status lens':bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=.6
 mats.append(mat)
parts=[];plates=[];belt=[]
collection=bpy.data.collections.new('BRK Armor Concept');bpy.context.scene.collection.children.link(collection)

def finish(ob,name,material,bone,frame,group,layer):
 bpy.context.view_layer.objects.active=ob
 for mod in list(ob.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
 for v in ob.data.vertices:v.co=B@(frame@v.co)
 for poly in ob.data.polygons:poly.use_smooth=False
 ob.data.update();ob.name='BRK_'+name;ob.data.materials.append(mats[material])
 ob['part_id']=name;ob['material_slot']=material;ob['assembly_group']=group;ob['layer']=layer;ob['rig_bone']=bone
 for col in list(ob.users_collection):col.objects.unlink(ob)
 collection.objects.link(ob)
 vg=ob.vertex_groups.new(name=bone);vg.add(list(range(len(ob.data.vertices))),1.,'REPLACE')
 mod=ob.modifiers.new('Rigid existing rig binding','ARMATURE');mod.object=arm
 ob.parent=arm;ob.matrix_parent_inverse=arm.matrix_world.inverted()
 parts.append(ob)
 if group=='BELT':belt.append(ob)
 return ob

def prism(name,outline,z0,z1,mat,bone,frame,group,layer,bevel=.002):
 n=len(outline);verts=[(x,y,z) for z in [z0,z1] for x,y in outline]
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
 faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
 if bevel:
  mod=ob.modifiers.new('Editable manufactured bevel','BEVEL');mod.width=bevel;mod.segments=2
 return finish(ob,name,mat,bone,frame,group,layer)

def outline(w,h,kind='shield'):
 if kind=='shin':return [(-w*.42,-h*.5),(w*.42,-h*.5),(w*.5,-h*.32),(w*.40,h*.40),(w*.25,h*.5),(-w*.25,h*.5),(-w*.40,h*.40),(-w*.5,-h*.32)]
 if kind=='rib':return [(-w*.40,-h*.5),(w*.40,-h*.5),(w*.5,-h*.25),(w*.35,h*.5),(-w*.35,h*.5),(-w*.5,-h*.25)]
 return [(-w*.36,-h*.5),(w*.36,-h*.5),(w*.5,-h*.34),(w*.5,h*.32),(w*.30,h*.5),(-w*.30,h*.5),(-w*.5,h*.32),(-w*.5,-h*.34)]

def box(name,centre,size,mat,bone,group,layer=0,rotation=(0,0,0),bevel=.002):
 frame=Matrix.Translation(Vector(centre))@Euler(rotation).to_matrix().to_4x4()
 return prism(name,[(-size[0]/2,-size[1]/2),(size[0]/2,-size[1]/2),(size[0]/2,size[1]/2),(-size[0]/2,size[1]/2)],-size[2]/2,size[2]/2,mat,bone,frame,group,layer,bevel)

def cylinder(name,centre,radius,depth,mat,bone,group,layer=0,rotation=(0,0,0),segments=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=segments,radius=radius,depth=depth,location=(0,0,0))
 ob=bpy.context.object;mod=ob.modifiers.new('Cylinder chamfer','BEVEL');mod.width=min(.0015,radius*.12);mod.segments=1
 return finish(ob,name,mat,bone,Matrix.Translation(Vector(centre))@Euler(rotation).to_matrix().to_4x4(),group,layer)

specs=[]
def plate(pid,name,bone,centre,w,h,kind='shield',rotation=(0,0,0)):
 frame=Matrix.Translation(Vector(centre))@Euler(rotation).to_matrix().to_4x4()
 inv=frame.inverted()
 surface=[]
 for triangle in bodyTriangles:
  pp=[inv@p for p in triangle]
  if max(p.x for p in pp)<-w*.5 or min(p.x for p in pp)>w*.5:continue
  if max(p.y for p in pp)<-h*.5 or min(p.y for p in pp)>h*.5:continue
  surface.append(max(p.z for p in pp))
 if surface:
  # Test whole anatomical triangles in the footprint, including bone-blended seams.
  gap=.032 if pid=='C03' else .022
  frame.translation+=frame.to_3x3()@Vector((0,0,max(surface)+gap))
 start=len(parts)
 prism(pid+'_01_mount',outline(w*.90,h*.90,kind),-.011,-.007,2,bone,frame,pid,0,.0015)
 prism(pid+'_02_liner',outline(w*.95,h*.95,kind),-.007,-.003,3,bone,frame,pid,1,.001)
 prism(pid+'_03_gold_shell',outline(w,h,kind),-.003,.010,1,bone,frame,pid,2,.0025)
 prism(pid+'_04_graphite_face',outline(w*.82,h*.82,kind),.010,.014,0,bone,frame,pid,3,.0015)
 for i,(x,y) in enumerate([(-w*.31,-h*.31),(w*.31,-h*.31),(0,h*.32)],1):
  # Fastener axes remain perpendicular to the armor face.
  p=frame@Vector((x,y,.016))
  cylinder(pid+'_05_fastener_'+str(i),p,.004,.004,2,bone,pid,4,rotation,6)
 obset=parts[start:]
 entry={'id':pid,'name':name,'bone':bone,'frameModelUnity':[list(r) for r in frame],
        'widthMetres':w,'heightMetres':h,'depthMetres':.029,'shape':kind,
        'parts':[ob['part_id'] for ob in obset],'layerOrder':['mount','liner','gold_shell','graphite_face','fasteners']}
 plates.append(entry);specs.append((pid,name,bone,centre,w,h,kind,rotation))

# All outlines are closed beveled prisms. No sculpted detail or unresolved Boolean stack.
plate('C01','Brust links','Chest',(-.145,1.46,.257),.245,.29)
plate('C02','Brust rechts','Chest',(.145,1.46,.257),.245,.29)
plate('C03','Brustbruecke','Chest',(0,1.46,.283),.100,.195)
for side,sign in [('L',-1),('R',1)]:
 plate('S0'+('1' if side=='L' else '2'),'Schulter '+side,'UpperArm'+side,(sign*.407,1.518,.014),.195,.205,rotation=(-.95,sign*.5,sign*-.18))
 plate('R0'+('1' if side=='L' else '2'),'Flanke '+side,'Spine',(sign*.272,1.30,.120),.175,.23,'rib',rotation=(0,sign*.65,0))
 plate('B0'+('1' if side=='L' else '2'),'Ruecken '+side,'Chest',(sign*.142,1.393,-.179),.23,.30,rotation=(0,math.pi,0))
 plate('T0'+('1' if side=='L' else '2'),'Oberschenkel '+side,'Thigh'+side,(sign*.187,.826,.195),.162,.237)
 plate('K0'+('1' if side=='L' else '2'),'Knie '+side,'Shin'+side,(sign*.185,.549,.133),.137,.103)
 plate('L0'+('1' if side=='L' else '2'),'Schienbein '+side,'Shin'+side,(sign*.207,.333,.111),.113,.263,'shin')
for i,y in enumerate([1.272,1.197,1.122],1):
 plate('A0'+str(i),'Bauchsegment '+str(i),'Spine',(0,y,.223-i*.007),.292-i*.012,.066)
plates.sort(key=lambda x:x['id'])

# Separate utility belt. The elliptical band is an actual hollow solid.
height=.077;rx=.262;rz=.179;thickness=.011;cy=1.065;n=32
verts=[]
for y in [cy-height*.5,cy+height*.5]:
 for radii in [(rx,rz),(rx-thickness,rz-thickness)]:
  for i in range(n):
   a=2*math.pi*i/n;verts.append((radii[0]*math.cos(a),y,radii[1]*math.sin(a)))
faces=[]
for i in range(n):
 j=(i+1)%n
 faces.extend([(i,j,2*n+j,2*n+i),(n+j,n+i,3*n+i,3*n+j),(j,i,n+i,n+j),(2*n+i,2*n+j,3*n+j,3*n+i)])
mesh=bpy.data.meshes.new('Hollow utility belt');mesh.from_pydata(verts,[],faces);mesh.update()
ob=bpy.data.objects.new('Belt',mesh);bpy.context.collection.objects.link(ob)
finish(ob,'U01_band',0,'Hips',Matrix.Identity(4),'BELT',0)
box('U02_buckle',(0,1.065,.193),(.079,.061,.024),1,'Hips','BELT')
box('U03_buckle_inset',(0,1.065,.207),(.054,.034,.007),2,'Hips','BELT')
box('U04_status',(0,1.065,.212),(.024,.004,.003),4,'Hips','BELT',bevel=.0005)
for side,sign in [('L',-1),('R',1)]:
 centre=(sign*.172,1.019,.191)
 box('U05_'+side+'_pouch',centre,(.104,.119,.071),0,'Hips','BELT',bevel=.006)
 box('U06_'+side+'_pouch_flap',(sign*.172,1.065,.231),(.111,.027,.012),1,'Hips','BELT',bevel=.0025)
 box('U07_'+side+'_clasp',(sign*.172,1.035,.232),(.018,.041,.009),2,'Hips','BELT')
 box('U08_'+side+'_pouch_inset',(sign*.172,1.007,.230),(.074,.060,.004),3,'Hips','BELT')
 for i in [-1,1]:box('U09_'+side+'_belt_loop_'+str(i),(sign*.172+i*.032,1.086,.190),(.015,.048,.008),2,'Hips','BELT')
# Compact rear repair kit, not a thigh-hanging box.
box('U10_rear_kit',(0,1.025,-.197),(.144,.104,.060),0,'Hips','BELT',bevel=.008)
box('U11_rear_kit_lid',(0,1.063,-.231),(.148,.030,.012),1,'Hips','BELT')
box('U12_rear_kit_lock',(0,1.027,-.231),(.032,.032,.010),2,'Hips','BELT')
# Flashlight, along the vertical axis; faceted cylindrical grip and separate lens.
for name,y,r,d,mat in [('body',1.018,.023,.123,2),('grip',1.005,.024,.067,0),('head',1.087,.031,.026,1),('lens',1.102,.024,.004,4)]:
 cylinder('U13_flashlight_'+name,(.285,y,.02),r,d,mat,'Hips','BELT',rotation=(math.pi*.5,0,0))
for i,y in enumerate([.992,1.052],1):box('U14_torch_clip_'+str(i),(.257,y,.015),(.040,.018,.040),3,'Hips','BELT')
# A closed polygon forms the open-ended wrench profile; no Boolean is required.
toolFrame=Matrix.Translation(Vector((-.283,1.02,.009)))@Euler((0,-math.pi*.5,0)).to_matrix().to_4x4()
toolOutline=[(-.014,-.083),(.014,-.083),(.014,.035),(.035,.052),(.032,.078),(.017,.062),(-.003,.065),(-.013,.084),(-.029,.071),(-.026,.050),(-.014,.035)]
prism('U15_repair_wrench',toolOutline,-.004,.004,2,'Hips',toolFrame,'BELT',0,.001)
for i,y in enumerate([.968,1.033],1):box('U16_tool_clip_'+str(i),(-.268,y,.009),(.038,.018,.035),0,'Hips','BELT')
for side,sign in [('L',-1),('R',1)]:
 cylinder('U17_'+side+'_cartridge',(sign*.172,1.045,-.154),.021,.089,2,'Hips','BELT',rotation=(math.pi*.5,0,0))
 cylinder('U18_'+side+'_cartridge_cap',(sign*.172,1.094,-.154),.023,.014,1,'Hips','BELT',rotation=(math.pi*.5,0,0))
 box('U19_'+side+'_cartridge_holder',(sign*.172,1.026,-.166),(.051,.034,.029),0,'Hips','BELT')

# Authoritative per-part geometry for exact drawings and future armor export.
def geometry(objects):
 vv=[];nn=[];uv=[];slots=[{'triangles':[]} for _ in mats];table=[]
 for ob in objects:
  mesh=ob.data;mesh.calc_loop_triangles();cache={};start=len(vv)//3
  for tri in mesh.loop_triangles:
   pp=[C@mesh.vertices[i].co for i in tri.vertices]
   if (pp[1]-pp[0]).cross(pp[2]-pp[0]).length<2e-11:continue
   normal=C.to_3x3()@mesh.polygons[tri.polygon_index].normal;indices=[]
   for index in tri.vertices:
    p=C@mesh.vertices[index].co;key=tuple(round(x,8) for x in (*p,*normal))
    if key not in cache:
     cache[key]=len(vv)//3;vv.extend(round(x,8) for x in p);nn.extend(round(x,8) for x in normal);uv.extend([round(p.x*4,6),round(p.y*4,6)])
    indices.append(cache[key])
   slots[ob['material_slot']]['triangles'].extend(indices)
  table.append({'id':ob['part_id'],'bone':ob['rig_bone'],'material':ob['material_slot'],'firstVertex':start,'vertexCount':len(vv)//3-start})
 return {'vertices':vv,'normals':nn,'uv':uv,'submeshes':slots,'parts':table}
hips=C@(arm.matrix_world@arm.data.bones['Hips'].matrix_local).translation
spine=C@(arm.matrix_world@arm.data.bones['Spine'].matrix_local).translation
beltData=geometry(belt)
beltData.update({'version':1,'revision':'brakk-utility-belt-20261012','pivot':list(hips),'referenceRigSpan':(spine-hips).length,
                 'materials':[{'name':x[0],'color':list(x[1]),'metallic':x[2],'smoothness':1-x[3]} for x in palette]})
resource=RES/'Model.bytes';resource.write_bytes(gzip.compress(json.dumps(beltData,separators=(',',':')).encode(),mtime=0))

catalogue={'revision':'brakk-armor-concept-01-20261012','coordinateSpace':'Unity authored-model metres, X right/Y up/Z forward',
           'sourceRigSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'plates':plates,'utilityBeltParts':beltData['parts'],
           'armorIntegratedIntoGame':False,'utilityBeltRuntimeResource':'Assets/Alterverse/Resources/BrakkUtilityBelt/Model.bytes',
           'modeling':'Closed polygon prisms, 1-2 segment bevels, faceted cylinders; separate named layers and fasteners. Existing bones only.'}
(ART/'catalogue.json').write_text(json.dumps(catalogue,indent=2)+'\n')
# Native diagram data preserves full face topology. Coordinates are plate-local metres.
diagrams=[]
for plate in plates:
 inverse=Matrix(plate['frameModelUnity']).inverted();entries=[]
 for ob in parts:
  if ob['assembly_group']!=plate['id']:continue
  entries.append({'id':ob['part_id'],'material':ob['material_slot'],'layer':ob['layer'],
                  'vertices':[list(inverse@(C@v.co)) for v in ob.data.vertices],
                  'faces':[list(p.vertices) for p in ob.data.polygons]})
 diagrams.append({'plate':plate,'parts':entries})
(ART/'diagram-geometry.json').write_text(json.dumps(diagrams,separators=(',',':')))

# Clean prototype anatomy: keep actual fabric/skin and feet, excluding the old armor.
body=bpy.data.objects['DF_Anatomy'];bm=bmesh.new();bm.from_mesh(body.data)
remove=[]
for f in bm.faces:
 mat=body.data.materials[f.material_index].name.lower()
 foot=all(any(body.vertex_groups[g.group].name.startswith('Foot') and g.weight>.95 for g in body.data.vertices[v.index].groups) for v in f.verts)
 if not (find(f.verts[0].index)==mainBody and ('fabric' in mat or mat.startswith(('skin','flesh'))) or foot):remove.append(f)
bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(body.data);bm.free()
for mat in body.data.materials:
 if mat and mat.use_nodes and 'fabric' in mat.name.lower():mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.82,.56,.18,1)
for ob in list(bpy.data.objects):
 if ob not in parts and ob not in [arm,body]:bpy.data.objects.remove(ob,do_unlink=True)
# Keep the previously accepted fist design; this is a linked source reference, not a new fist export.
fistsPath=ROOT/'ArtSource/Heroes/BrakkFists/BrakkFists-assembly.blend'
with bpy.data.libraries.load(str(fistsPath),link=False) as (a,b):b.objects=[name for name in a.objects if name.startswith('FGV2_')]
for ob in b.objects:
 if ob is None:continue
 bpy.context.scene.collection.objects.link(ob);ob.parent=arm;ob.matrix_parent_inverse=arm.matrix_world.inverted()
 for mod in ob.modifiers:
  if mod.type=='ARMATURE':mod.object=arm
 for side in ['L','R']:arm.pose.bones['Hand'+side].scale=(.8,.8,.8)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=False;scene.use_nodes=False
for layer in scene.view_layers:layer.cycles.use_denoising=False
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.2
scene.world.color=(.12,.15,.18);scene.render.image_settings.file_format='PNG'
scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100

def aim(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(2,-3,4),500,3),('Fill',(-3,-1,2),300,3),('Rim',(1,3,3),450,2)]:
 bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.name=name;light.data.energy=power;light.data.size=size;aim(light,(0,0,1))
bpy.ops.object.camera_add(location=(3,-6,2.8));camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=2.45;scene.camera=camera;aim(camera,(0,0,.94))
# Pose-independent modeling file keeps all armor layers and inherited rig for editing.
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'BrakkArmor-concept.blend'),compress=True)
for name,location in [('front',(2.7,-6,2.55)),('back',(-2.7,6,2.55))]:
 camera.location=location;aim(camera,(0,0,.94));scene.render.filepath=str(ART/('BrakkArmor-'+name+'.png'));bpy.ops.render.render(write_still=True)
# A belt-only product view, preserving the exact runtime geometry.
for ob in bpy.data.objects:
 if ob.type=='MESH':ob.hide_render=ob not in belt
camera.location=(.8,-1.4,1.6);aim(camera,(0,0,1.06));camera.data.ortho_scale=.79
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.filepath=str(ART/'BrakkUtilityBelt.png');bpy.ops.render.render(write_still=True)
# Save a separate exploded native scene with all 18 plate modules laid out for inspection.
for ob in list(bpy.data.objects):
 if ob.type=='MESH' and ob not in parts:bpy.data.objects.remove(ob,do_unlink=True)
for index,plate in enumerate(plates):
 inverse=Matrix(plate['frameModelUnity']).inverted()
 for ob in parts:
  if ob['assembly_group']!=plate['id']:continue
  for mod in list(ob.modifiers):ob.modifiers.remove(mod)
  ob.parent=None;ob.hide_render=False
  for v in ob.data.vertices:
   local=inverse@(C@v.co);local.z+=ob['layer']*.055
   v.co=B@local+Vector(((index%4)*.5,(index//4)*.5,0))
for ob in belt:ob.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'BrakkArmor-exploded.blend'),compress=True)
# A neutral OBJ contains only new armor/belt parts, without inherited body geometry.
with (ART/'BrakkArmor-parts.obj').open('w') as out:
 out.write('# Named Brakk concept solids; use concept.blend for authored assembly positions.\n');offset=1
 for ob in parts:
  out.write('o '+ob.name+'\n')
  for v in ob.data.vertices:out.write('v '+' '.join(f'{x:.8f}' for x in v.co)+'\n')
  ob.data.calc_loop_triangles()
  for t in ob.data.loop_triangles:out.write('f '+' '.join(str(offset+i) for i in t.vertices)+'\n')
  offset+=len(ob.data.vertices)
assert len(plates)==18
assert all(math.isfinite(x) for x in beltData['vertices']+beltData['normals']+beltData['uv'])
assert all(0<=i<len(beltData['vertices'])//3 for s in beltData['submeshes'] for i in s['triangles'])
assert all(len(v.groups)==1 and v.groups[0].weight==1 for ob in belt for v in ob.data.vertices)
validation={'plateModules':len(plates),'armorSolidParts':sum(o['assembly_group']!='BELT' for o in parts),
            'beltParts':len(belt),'beltVertices':len(beltData['vertices'])//3,'beltTriangles':sum(len(s['triangles'])//3 for s in beltData['submeshes']),
            'checks':{'finiteBeltGeometry':True,'validBeltIndices':True,'rigidHipsWeights':True,'namedClosedPlatePrisms':True},
            'unityEditorExecuted':False,'armorRuntimeIntegrated':False,'runtimeSha256':hashlib.sha256(resource.read_bytes()).hexdigest()}
(ART/'validation.json').write_text(json.dumps(validation,indent=2)+'\n')
print('BRAKK_ARMOR_VALIDATED',json.dumps(validation),flush=True)
