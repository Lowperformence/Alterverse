"""Author the shared connected map border in Blender and export its adaptive ring data."""
import bpy,math,json,os,uuid
from pathlib import Path
from mathutils import Vector
root=next(p for p in Path(__file__).resolve().parents if (p/'Assets').is_dir())
out=root/'ArtSource/PlanetSurfaces';out.mkdir(parents=True,exist_ok=True)
resource=root/'Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorder.json'
N=256;depths=[-.7,-2.5,-5.0,-8.5,-12.5,-16.0];scales=[1.002,1.012,.990,1.000,.970,.910]
def noise(index,seed):
 n=(index*374761393+seed*668265263)&0xffffffff;n=((n^(n>>13))*1274126177)&0xffffffff
 return ((n^(n>>16))&0xffffff)/16777215
def angular(angle,count,seed):
 v=angle*count;i=math.floor(v);t=v-i
 return noise(i%count,seed)*(1-t)+noise((i+1)%count,seed)*t
profile={'version':1,'sampleCount':N,'ringCount':len(depths),'radialScale':[],'depth':[]}
for layer,(depth,scale) in enumerate(zip(depths,scales)):
 for i in range(N):
  angle=i/N;broad=angular(angle,16,19+layer*7);fracture=noise(int(angle*64),73+layer*11)
  radial=scale+(broad-.5)*(.012+layer*.004)+(fracture-.5)*.020
  y=depth+(broad-.5)*(.65+layer*.28)+(fracture-.5)*.45
  profile['radialScale'].append(round(radial,6));profile['depth'].append(round(y,6))
resource.write_text(json.dumps(profile,separators=(',',':')))
Path(str(resource)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/PlanetSurfaces/UniversalBorder').hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
def sample(values,row,angle):
 p=angle*N;i=int(p)%N;t=p-int(p);return values[row*N+i]*(1-t)+values[row*N+(i+1)%N]*t
bpy.ops.wm.read_factory_settings(use_empty=True)
# One continuous rectangular rim, matching the Unity universal outward outline.
raw=[(-42+x,-36) for x in range(84)]+[(42,-36+z) for z in range(72)]+[(42-x,36) for x in range(84)]+[(-42,36-z) for z in range(72)]
boundary=[]
for x,z in raw:
 angle=(math.atan2(z,x)/(2*math.pi))%1;radius=math.hypot(x,z);extension=.45+angular(angle,24,19)*2.40+angular(angle,80,50)*.55
 boundary.append((x+x/radius*extension,z+z/radius*extension))
vertices=[(x,-z,0) for x,z in boundary];faces=[];materials=[];count=len(boundary)
for layer in range(len(depths)):
 for x,z in boundary:
  angle=(math.atan2(z,x)/(2*math.pi))%1;scale=sample(profile['radialScale'],layer,angle);y=sample(profile['depth'],layer,angle)
  vertices.append((x*scale,-z*scale,y))
 for i in range(count):
  j=(i+1)%count;a=layer*count+i;b=layer*count+j;c=(layer+1)*count+i;d=(layer+1)*count+j
  faces.extend([(a,b,c),(b,d,c)]);materials.extend([0 if layer<2 else 1 if layer<4 else 2]*2)
vertices.append((0,0,-18.0));center=len(vertices)-1
for i in range(count):faces.append((center,len(depths)*count+i,len(depths)*count+(i+1)%count));materials.append(2)
me=bpy.data.meshes.new('Universal connected cliff');me.from_pydata(vertices,[],faces);me.update();ob=bpy.data.objects.new('DF_UniversalBorder',me);bpy.context.collection.objects.link(ob)
for name,color in [('Upper weathered rock',(.26,.24,.21,1)),('Exposed strata',(.16,.15,.14,1)),('Dark underside',(.065,.069,.079,1))]:
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=.85;ob.data.materials.append(m)

for p,index in zip(me.polygons,materials):p.material_index=index;p.use_smooth=False
# UVs for further manual texture baking; runtime materials already use world-space details.
uv=me.uv_layers.new(name='BorderUV')
for poly in me.polygons:
 for li in poly.loop_indices:
  v=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=((math.atan2(-v.y,v.x)/(2*math.pi))%1,-v.z/18.0)
bpy.context.view_layer.objects.active=ob;ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/'UniversalBorder.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=False;scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Border studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.035,.05,.08,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
# A preview top plane closes the sample only; it is not exported as border geometry.
top=bpy.data.meshes.new('Preview ground');top.from_pydata(vertices[:count],[],[tuple(range(count-1,-1,-1))]);top.update();ground=bpy.data.objects.new('Preview ground only',top);bpy.context.collection.objects.link(ground);ground.data.materials.append(ob.data.materials[0])
def aim(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,color in [('Key',(-40,-65,85),180000,65,(1,.88,.72)),('Fill',(65,-10,50),90000,60,(.65,.77,1)),('Rim',(0,60,65),140000,45,(.62,.72,1))]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=size;d.color=color;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;aim(o,(0,0,-3))
d=bpy.data.cameras.new('Border view');o=bpy.data.objects.new('Border view',d);bpy.context.collection.objects.link(o);o.location=(100,-130,50);aim(o,(0,0,-3));d.type='ORTHO';d.ortho_scale=124;scene.camera=o
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'UniversalBorder.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'UniversalBorder.blend'),compress=True)
# Topological checks: the only open loop is the boundary that joins the live floor.
from collections import Counter
edges=Counter(tuple(sorted(e)) for f in faces for e in zip(f,f[1:]+f[:1]));assert all(n<=2 for n in edges.values());assert sum(n==1 for n in edges.values())==count
assert all(profile['depth'][r*N+i]<(-.15 if r==0 else profile['depth'][(r-1)*N+i]) for r in range(len(depths)) for i in range(N))
manifest={'vertices':len(vertices),'triangles':len(faces),'rings':len(depths),'openBoundaryEdges':count,'connectedComponents':1,'detachedFragments':0,'maxRuntimeTriangles':704*(len(depths)*2+1),'topologyValidation':'passed','unityPlayModeValidation':'pending'}
(out/'universal-border-validation.json').write_text(json.dumps(manifest,indent=2));print('UNIVERSAL_BORDER_COMPLETE',json.dumps(manifest),flush=True)

bpy.ops.render.render(write_still=True)
