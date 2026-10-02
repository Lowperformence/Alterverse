"""Run with Blender -b --python: add fitted armor and technical props to the current Traveler mesh."""
import bpy, gzip, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
TARGET=ROOT/'Assets/Alterverse/Resources/ChronoTraveler/Model.bytes'
ART=ROOT/'ArtSource/Heroes/ChronoTraveler'
data=json.loads(gzip.decompress(TARGET.read_bytes()))
# Rebuilding replaces this pass instead of accumulating duplicate details.
base=data.get('blenderDetailBaseVertexCount',len(data['boneIndices']))
data['vertices']=data['vertices'][:base*3];data['normals']=data['normals'][:base*3];data['boneIndices']=data['boneIndices'][:base]
for sub in data['submeshes']:
 t=sub['triangles'];sub['triangles']=[v for i in range(0,len(t),3) if max(t[i:i+3])<base for v in t[i:i+3]]
data['blenderDetailBaseVertexCount']=base
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
colors=[(.018,.024,.029,1),(.062,.082,.10,1),(.30,.38,.42,1),(.02,.65,.94,1),(.003,.008,.012,1),(.025,.18,1,1)]
materials=[]
for i,color in enumerate(colors):
 m=bpy.data.materials.new('Traveler slot '+str(i));m.diffuse_color=color;m.use_nodes=True
 shader=m.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=color
 shader.inputs['Metallic'].default_value=.7 if i in (1,2) else .1
 if i in (3,5):shader.inputs['Emission Color'].default_value=color;shader.inputs['Emission Strength'].default_value=2.5
 materials.append(m)
verts=[(data['vertices'][i],data['vertices'][i+2],data['vertices'][i+1]) for i in range(0,len(data['vertices']),3)]
faces=[];slots=[]
for slot,sub in enumerate(data['submeshes']):
 for i in range(0,len(sub['triangles']),3):
  a,b,c=sub['triangles'][i:i+3];faces.append((a,c,b));slots.append(slot)
mesh=bpy.data.meshes.new('Current Traveler');mesh.from_pydata(verts,[],faces);mesh.update()
body=bpy.data.objects.new('Traveler existing model',mesh);bpy.context.collection.objects.link(body)
for m in materials:mesh.materials.append(m)
for polygon,slot in zip(mesh.polygons,slots):polygon.material_index=slot;polygon.use_smooth=True
details=[]
def box(name,center,size,slot,bone,bevel=.006):
 bpy.ops.mesh.primitive_cube_add(size=1,location=(center[0],center[2],center[1]))
 obj=bpy.context.object;obj.name=name;obj.dimensions=(size[0],size[2],size[1])
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 mod=obj.modifiers.new('Machined bevel','BEVEL');mod.width=bevel;mod.segments=2
 bpy.ops.object.modifier_apply(modifier=mod.name)
 obj.data.materials.append(materials[slot]);obj['bone']=bone;obj['material_slot']=slot;details.append(obj)
 return obj
for side,sign in [('L',-1),('R',1)]:
 # Separate plates stay on the corresponding thigh and shin, leaving the knee free.
 for label,bone,y,x,width in [('Upper thigh','Thigh'+side,.79,.126,.113),('Lower thigh','Thigh'+side,.655,.129,.103),('Upper shin','Shin'+side,.395,.142,.087),('Lower shin','Shin'+side,.265,.155,.078)]:
  box(side+' '+label+' titanium plate',(sign*x,y,.094),(width,.115,.031),2,bone)
  box(side+' '+label+' graphite inset',(sign*x,y,.112),(width*.66,.080,.009),1,bone,.003)
  box(side+' '+label+' cyan status line',(sign*(x+width*.24),y,.119),(.007,.061,.005),3,bone,.001)
 box(side+' belt module',(sign*.162,1.00,.075),(.049,.070,.037),1,'Hips')
 box(side+' belt module indicator',(sign*.162,1.009,.096),(.024,.008,.006),3,'Hips',.001)
 box(side+' chest telemetry module',(sign*.106,1.32,.135),(.041,.058,.023),2,'Chest',.004)
 for j in range(3):box(side+' chest indicator '+str(j),(sign*.106,1.305+j*.012,.149),(.024,.004,.005),3,'Chest',.001)
 box(side+' forearm diagnostic panel',(sign*.294,1.068,.083),(.039,.059,.022),1,'Forearm'+side,.004)
 box(side+' forearm display',(sign*.294,1.073,.097),(.023,.032,.005),3,'Forearm'+side,.002)
 box(side+' rear power module',(sign*.079,1.30,-.127),(.061,.113,.042),1,'Chest')
 for j in range(4):box(side+' cooling fin '+str(j),(sign*.079,1.265+j*.022,-.153),(.050,.008,.012),2,'Chest',.002)
for obj in details:
 obj.data.calc_loop_triangles();offset=len(data['boneIndices']);bone=data['boneNames'].index(obj['bone'])
 for v in obj.data.vertices:
  p=obj.matrix_world@v.co;n=obj.matrix_world.to_3x3()@v.normal;n.normalize()
  data['vertices'].extend(round(x,6) for x in (p.x,p.z,p.y));data['normals'].extend(round(x,6) for x in (n.x,n.z,n.y));data['boneIndices'].append(bone)
 for tri in obj.data.loop_triangles:
  a,b,c=tri.vertices;data['submeshes'][obj['material_slot']]['triangles'].extend((offset+a,offset+c,offset+b))
TARGET.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))
ART.mkdir(parents=True,exist_ok=True)
report={'baseVertices':base,'addedVertices':len(data['boneIndices'])-base,'detailObjects':len(details),'legPlates':8,'boneNames':sorted(set(o['bone'] for o in details))}
(ART/'blender-details-report.json').write_text(json.dumps(report,indent=2)+'\n')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.color=(.10,.10,.10)
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(-3,4,4),700,4),('Fill',(3,2,2),500,3),('Rim',(0,-3,3),800,3)]:
 light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size
 obj=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(obj);obj.location=loc;aim(obj,(0,0,1))
cam=bpy.data.cameras.new('Preview');obj=bpy.data.objects.new('Preview',cam);bpy.context.collection.objects.link(obj);obj.location=(2.3,5,2.1);aim(obj,(0,0,.94));cam.type='ORTHO';cam.ortho_scale=2.07;scene.camera=obj
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ART/'blender-details-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'TravelerDetails.blend'),compress=True)
bpy.ops.render.render(write_still=True)
print('TRAVELER_BLENDER_DETAILS',json.dumps(report),flush=True)
