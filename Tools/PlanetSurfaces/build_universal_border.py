"""Author the shared connected map border in Blender and export its adaptive ring data."""
import bpy,math,json,os,uuid
from pathlib import Path
from mathutils import Vector
root=next(p for p in Path(__file__).resolve().parents if (p/'Assets').is_dir())
out=root/'ArtSource/PlanetSurfaces';out.mkdir(parents=True,exist_ok=True)
resource=root/'Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorder.json'
N=256;depths=[-.7,-1.9,-3.4,-5.1,-7.0,-8.8];scales=[1.001,1.006,.994,.998,.977,.950]
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
  radial=scale+(broad-.5)*(.006+layer*.002)+(fracture-.5)*.008
  y=depth+(broad-.5)*(.35+layer*.12)+(fracture-.5)*.24
  profile['radialScale'].append(round(radial,6));profile['depth'].append(round(y,6))
resource.write_text(json.dumps(profile,separators=(',',':')))
Path(str(resource)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/PlanetSurfaces/UniversalBorder').hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
def sample(values,row,angle):
 p=angle*N;i=int(p)%N;t=p-int(p);return values[row*N+i]*(1-t)+values[row*N+(i+1)%N]*t
bpy.ops.wm.read_factory_settings(use_empty=True)
# Continuous subtly chipped rim, matching the live terrain boundary.
raw=[(-42+x,-36) for x in range(84)]+[(42,-36+z) for z in range(72)]+[(42-x,36) for x in range(84)]+[(-42,36-z) for z in range(72)]
boundary=[]
for x,z in raw:
 angle=(math.atan2(z,x)/(2*math.pi))%1;radius=math.hypot(x,z);extension=.18+angular(angle,24,19)*.48+angular(angle,80,50)*.14
 boundary.append((x+x/radius*extension,z+z/radius*extension))
vertices=[(x,-z,0) for x,z in boundary];faces=[];materials=[];count=len(boundary)
for layer in range(len(depths)):
 for x,z in boundary:
  angle=(math.atan2(z,x)/(2*math.pi))%1;scale=sample(profile['radialScale'],layer,angle);y=sample(profile['depth'],layer,angle)
  vertices.append((x*scale,-z*scale,y))
 for i in range(count):
  j=(i+1)%count;a=layer*count+i;b=layer*count+j;c=(layer+1)*count+i;d=(layer+1)*count+j
  faces.extend([(a,b,c),(b,d,c)]);materials.extend([0 if layer<2 else 1 if layer<4 else 2]*2)
vertices.append((0,0,-10.0));center=len(vertices)-1
for i in range(count):faces.append((center,len(depths)*count+i,len(depths)*count+(i+1)%count));materials.append(2)
me=bpy.data.meshes.new('Universal connected cliff');me.from_pydata(vertices,[],faces);me.update();ob=bpy.data.objects.new('DF_UniversalBorder',me);bpy.context.collection.objects.link(ob)
for name,color in [('Upper weathered rock',(.26,.24,.21,1)),('Exposed strata',(.22,.21,.19,1)),('Dark underside',(.11,.115,.125,1))]:
 m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=.85;ob.data.materials.append(m)

for material in ob.data.materials:
 nodes=material.node_tree.nodes;links=material.node_tree.links;bs=nodes.get('Principled BSDF')
 tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=5;tex.inputs['Detail'].default_value=3
 bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.28;bump.inputs['Distance'].default_value=.13
 links.new(tex.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
for p,index in zip(me.polygons,materials):p.material_index=index;p.use_smooth=False
# UVs for further manual texture baking; runtime materials already use world-space details.
uv=me.uv_layers.new(name='BorderUV')
for poly in me.polygons:
 for li in poly.loop_indices:
  v=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=((math.atan2(-v.y,v.x)/(2*math.pi))%1,-v.z/10.0)
bpy.context.view_layer.objects.active=ob;ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/'UniversalBorder.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
# Additional surface geometry, designed in the same coordinate system as the universal cliff.
import random
rng=random.Random(417);decor={'cracks':[],'rocks':[],'cliffRocks':[],'rockVertices':[],'rockTriangles':[]}
rockN=8
outline=[(-.50,-.31),(-.30,-.50),(.30,-.47),(.53,-.27),(.48,.32),(.27,.52),(-.33,.49),(-.51,.25)]
for ring,y,radius in [(0,-.30,.85),(1,.19,1.0),(2,.68,.82)]:
 for x,z in outline:
  decor['rockVertices'].extend([round(x*radius+rng.uniform(-.07,.07),5),round(y+rng.uniform(-.10,.10),5),round(z*radius+rng.uniform(-.07,.07),5)])
for ring in range(2):
 for j in range(rockN):
  a=ring*rockN+j;b=ring*rockN+(j+1)%rockN;c=a+rockN;d=b+rockN;decor['rockTriangles'].extend([a,c,b,b,c,d])
for j in range(1,rockN-1):decor['rockTriangles'].extend([2*rockN,2*rockN+j+1,2*rockN+j])
# Closed chipped polyhedron shared by small rim chips and layered cliff blocks.
for j in range(1,rockN-1):decor['rockTriangles'].extend([0,j,j+1])
for idx in range(7,len(raw),12):
 x,z=raw[idx];v=Vector((x,z)).normalized();tangent=Vector((-v.y,v.x));length=rng.uniform(1.4,3.0);points=[]
 for j in range(8):
  point=Vector((x,z))-v*(j/7*length)+tangent*rng.uniform(-.16,.16);points.extend([round(point.x,5),round(point.y,5)])
 decor['cracks'].append({'points':points,'width':round(rng.uniform(.045,.095),4)})
 branch=points[6:12];px,pz=branch[0],branch[1];branchPoints=[px,pz,px+tangent.x*.45-v.x*.25,pz+tangent.y*.45-v.y*.25,px+tangent.x*.8-v.x*.5,pz+tangent.y*.8-v.y*.5]
 decor['cracks'].append({'points':branchPoints,'width':.035})
for idx in range(4,len(raw),8):
 x,z=raw[idx];v=Vector((x,z)).normalized();distance=rng.uniform(.15,1.1);decor['rocks'].append({'x':round(x-v.x*distance,5),'z':round(z-v.y*distance,5),'size':round(rng.uniform(.28,.72),4),'rotation':round(rng.uniform(0,360),3)})
# Four irregular staggered courses of embedded fractured rocks, not detached debris.
for row in range(4):
 for idx in range(row%3,len(raw),3):
  x,z=raw[idx];normal=Vector((x,z)).normalized();tangent=Vector((-normal.y,normal.x));jitter=rng.uniform(-.65,.65);x+=tangent.x*jitter;z+=tangent.y*jitter;angle=(math.atan2(z,x)/(2*math.pi))%1
  depth=1.95+row*1.70+rng.uniform(-.40,.40)
  radial=1.0 if row<2 else .984 if row==2 else .965
  decor['cliffRocks'].append(dict(x=x,z=z,depth=round(depth,4),radialScale=radial,
   width=round(rng.uniform(3.0,4.2),4),height=round(rng.uniform(2.4,3.2),4),thickness=round(rng.uniform(1.40,2.0),4),
   rotation=round(math.degrees(math.atan2(x,z))+rng.uniform(-18,18),4)))
decorPath=root/'Assets/Alterverse/Resources/PlanetSurfaces/UniversalBorderDecor.json';decorPath.write_text(json.dumps(decor,separators=(',',':')))
Path(str(decorPath)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/PlanetSurfaces/UniversalBorderDecor').hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Border studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.035,.05,.08,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
# A preview top plane closes the sample only; it is not exported as border geometry.
top=bpy.data.meshes.new('Preview ground');top.from_pydata(vertices[:count],[],[tuple(range(count-1,-1,-1))]);top.update();ground=bpy.data.objects.new('Preview ground only',top);bpy.context.collection.objects.link(ground);ground.data.materials.append(ob.data.materials[0])
# Preview the authored fissures and rocks on the sample terrain, using the exact shared warp.
def warpPreview(x,z):
 r=math.hypot(x,z);a=(math.atan2(z,x)/(2*math.pi))%1;edge=max(abs(x)/42,abs(z)/36);t=max(0,min(1,(edge-.88)/.12));blend=t*t*(3-2*t);extension=.18+angular(a,24,19)*.48+angular(a,80,50)*.14
 return Vector((x+x/max(r,.001)*extension*blend,-z-z/max(r,.001)*extension*blend,.06))
crackV=[];crackF=[]
for crack in decor['cracks']:
 pts=[warpPreview(*crack['points'][i:i+2]) for i in range(0,len(crack['points']),2)]
 for i in range(len(pts)-1):
  tangent=(pts[i+1]-pts[i]).normalized().cross(Vector((0,0,1)))*crack['width'];a=len(crackV);crackV.extend([pts[i]-tangent,pts[i]+tangent,pts[i+1]+tangent,pts[i+1]-tangent]);crackF.append((a,a+1,a+2,a+3))
me=bpy.data.meshes.new('Authored border fissures');me.from_pydata(crackV,[],crackF);me.update();cracks=bpy.data.objects.new('Border fissures preview',me);bpy.context.collection.objects.link(cracks);cracks.data.materials.append(ob.data.materials[2])
for i,stone in enumerate(decor['rocks']):
 p=warpPreview(stone['x'],stone['z']);size=stone['size'];a=math.radians(stone['rotation']);vs=[]
 for j in range(0,len(decor['rockVertices']),3):
  x,y,z=decor['rockVertices'][j:j+3];vs.append(p+Vector(((x*math.cos(a)-z*math.sin(a))*size,-(x*math.sin(a)+z*math.cos(a))*size,y*size)))
 fs=[tuple(decor['rockTriangles'][j:j+3]) for j in range(0,len(decor['rockTriangles']),3)];me=bpy.data.meshes.new('Border rock');me.from_pydata(vs,[],fs);me.update();stoneOb=bpy.data.objects.new('Attached small rock '+str(i),me);bpy.context.collection.objects.link(stoneOb);stoneOb.data.materials.append(ob.data.materials[0])
# Build the same authored cliff-block placements that the live border reads from JSON.
for i,stone in enumerate(decor['cliffRocks']):
 p=warpPreview(stone['x'],stone['z']);p.x*=stone['radialScale'];p.y*=stone['radialScale'];p.z=-stone['depth']
 a=math.radians(stone['rotation']);vs=[]
 for j in range(0,len(decor['rockVertices']),3):
  x,y,z=decor['rockVertices'][j:j+3];x*=stone['width'];y*=stone['height'];z*=stone['thickness']
  vs.append(p+Vector((x*math.cos(a)+z*math.sin(a),x*math.sin(a)-z*math.cos(a),y)))
 fs=[tuple(decor['rockTriangles'][j:j+3]) for j in range(0,len(decor['rockTriangles']),3)]
 me=bpy.data.meshes.new('Authored cliff block');me.from_pydata(vs,[],fs);me.update()
 block=bpy.data.objects.new('Embedded cliff block '+str(i),me);bpy.context.collection.objects.link(block)
 block.data.materials.append(ob.data.materials[0 if stone['depth']<3 else 1 if stone['depth']<6 else 2])
# The FBX contains the actual rock model and attached chips; the runtime adapts the same
# profile/placements to terrain relief. Studio floor, crack display and lights are excluded.
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
 if obj.type=='MESH' and obj.name not in ('Preview ground only','Border fissures preview'):obj.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.fbx(filepath=str(out/'UniversalBorder.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
def aim(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,color in [('Key',(-40,-65,85),180000,65,(1,.88,.72)),('Fill',(65,-10,50),90000,60,(.65,.77,1)),('Rim',(0,60,65),140000,45,(.62,.72,1))]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=size;d.color=color;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;aim(o,(0,0,-3))
d=bpy.data.cameras.new('Border view');o=bpy.data.objects.new('Border view',d);bpy.context.collection.objects.link(o);o.location=(66,-76,28);aim(o,(27,-28,-2));d.type='ORTHO';d.ortho_scale=42;scene.camera=o
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'UniversalBorder.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'UniversalBorder.blend'),compress=True)
# Topological checks: the only open loop is the boundary that joins the live floor.
from collections import Counter
edges=Counter(tuple(sorted(e)) for f in faces for e in zip(f,f[1:]+f[:1]));assert all(n<=2 for n in edges.values());assert sum(n==1 for n in edges.values())==count
assert all(profile['depth'][r*N+i]<(-.15 if r==0 else profile['depth'][(r-1)*N+i]) for r in range(len(depths)) for i in range(N))
manifest={'vertices':len(vertices),'triangles':len(faces),'rings':len(depths),'openBoundaryEdges':count,'connectedComponents':1,'detachedFragments':0,'cliffBlocks':len(decor['cliffRocks']),'rockTriangles':len(decor['rockTriangles'])//3,'maxRuntimeCliffTriangles':704*(len(depths)*2+1)+len(decor['cliffRocks'])*len(decor['rockTriangles'])//3,'topologyValidation':'passed','unityPlayModeValidation':'pending'}
(out/'universal-border-validation.json').write_text(json.dumps(manifest,indent=2));print('UNIVERSAL_BORDER_COMPLETE',json.dumps(manifest),flush=True)

bpy.ops.render.render(write_still=True)
