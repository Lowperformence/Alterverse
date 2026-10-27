"""Author final Station Kit meshes with Blender 4.2; export compact Unity mesh data and a preview.
Run: blender -b --factory-startup --python Tools/StationKit/author_station.py -- --render
Source design: approved Station Kit 02, integrated trench tower, near-flush healing modules.
"""
import bpy,math,json,sys,gzip
from pathlib import Path
from mathutils import Vector,Euler

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/StationKit';OUT.mkdir(parents=True,exist_ok=True)
ART=ROOT/'ArtSource/StationKit';ART.mkdir(parents=True,exist_ok=True)
spec=json.loads((Path(__file__).with_name('design.json')).read_text())
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
colors=spec['materials'];materials={}
for name,color in colors.items():
 rgb=[int(color[i:i+2],16)/255 for i in (1,3,5)]
 rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
 m=bpy.data.materials.new('SK_'+name);m.diffuse_color=(*rgb,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1)
 p.inputs['Metallic'].default_value=.65 if name in ('steel','dark') else .15;p.inputs['Roughness'].default_value=.42
 if name in ('team','heal'):p.inputs['Emission Color'].default_value=(*rgb,1);p.inputs['Emission Strength'].default_value=1.5
 materials[name]=m

def primitive(kind,d):
 n=16
 if kind=='diamond':
  x,y,z=d;v=[(0,0,z/2),(0,0,-z/2),(x/2,0,0),(0,y/2,0),(-x/2,0,0),(0,-y/2,0)]
  return v,[(0,2,3),(0,3,4),(0,4,5),(0,5,2),(1,3,2),(1,4,3),(1,5,4),(1,2,5)]
 if kind=='ring':
  r,w,h=d;n=24;pts=[]
  for z in [-h/2,h/2]:
   for radius in [r-w/2,r+w/2]:pts.extend((radius*math.cos(i*math.tau/n),radius*math.sin(i*math.tau/n),z) for i in range(n))
  faces=[]
  for i in range(n):
   j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
  return pts,faces
 if kind=='cylinder':r,_,h=d;outline=[(r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n)) for i in range(n)]
 else:
  w,depth,h=d;w/=2;depth/=2;b=min(w,depth)*.22;n=8
  outline=[(-w+b,-depth),(w-b,-depth),(w,-depth+b),(w,depth-b),(w-b,depth),(-w+b,depth),(-w,depth-b),(-w,-depth+b)]
 verts=[(x,y,z) for z in [-h/2,h/2] for x,y in outline]
 return verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]

models={};origins={};objects=[]
def begin(name,origin):models[name]=[];origins[name]=Vector(origin)
def part(model,name,kind,d,pos,mat='armor',rotation=(0,0,0),bevel=.025,mount='static',taper=1):
 verts,faces=primitive(kind,d)
 if taper!=1:
  verts=[(x*(taper if z>0 else 1),y*(taper if z>0 else 1),z) for x,y,z in verts]
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
 obj=bpy.data.objects.new(model+'_'+name,data);scene.collection.objects.link(obj)
 obj.location=origins[model]+Vector(pos);obj.rotation_euler=[math.radians(v) for v in rotation]
 data.materials.append(materials[mat]);obj['material']=mat;obj['mount']=mount
 if bevel>0:
  mod=obj.modifiers.new('Edge chamfer','BEVEL');mod.width=min(bevel,min(d)*.15);mod.segments=1
  mod.affect='EDGES';mod.limit_method='ANGLE'
 models[model].append(obj);objects.append(obj);return obj
def box(model,name,d,pos,mat='armor',rot=(0,0,0),bevel=.025,mount='static',taper=1):return part(model,name,'box',d,pos,mat,rot,bevel,mount,taper)
def cyl(model,name,r,h,pos,mat='steel',rot=(0,0,0),bevel=.015,mount='static'):return part(model,name,'cylinder',[r,r,h],pos,mat,rot,bevel,mount)
def ring(model,name,r,w,h,z,mat='team',xy=(0,0)):return part(model,name,'ring',[r,w,h],[*xy,z],mat,bevel=0)
def sector(model,name,inner,outer,a0,a1,z,height,mat='armor',steps=3):
 pts=[]
 for zz in [z-height/2,z+height/2]:
  for r in [inner,outer]:pts.extend((r*math.cos(a0+(a1-a0)*i/steps),r*math.sin(a0+(a1-a0)*i/steps),zz) for i in range(steps+1))
 k=steps+1;faces=[]
 for i in range(steps):faces.extend([(i,i+1,k+i+1,k+i),(2*k+i,3*k+i,3*k+i+1,2*k+i+1),(i,2*k+i,2*k+i+1,i+1),(k+i,k+i+1,3*k+i+1,3*k+i)])
 faces.extend([(0,k,3*k,2*k),(k-1,3*k-1,4*k-1,2*k-1)])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(pts,[],faces);mesh.update();o=bpy.data.objects.new(model+'_'+name,mesh);scene.collection.objects.link(o)
 o.location=origins[model];mesh.materials.append(materials[mat]);o['material']=mat;o['mount']='static';models[model].append(o);objects.append(o)

begin('deck',(0,0,0));cyl('deck','Deck',4.75,.08,(0,0,.04),'dark',bevel=.005)
for i in range(12):
 a=i*math.tau/12;b=(i+1)*math.tau/12
 sector('deck','RadialDeck'+str(i),1.72,4.10,a+.018,b-.018,.085,.014,'steel' if i%3 else 'dark',2)
 sector('deck','OuterArmor'+str(i),4.14,4.66,a+.025,b-.025,.10,.12,'armor',2)
 sector('deck','OuterTrace'+str(i),4.31,4.37,a+.05,b-.05,.165,.009,'team',2)
 for r in [4.20,4.57]:
  angle=a+.08;cyl('deck','Fastener',.035,.01,(r*math.cos(angle),r*math.sin(angle),.166),'dark',bevel=0)
ring('deck','InnerTrace',1.74,.05,.012,.10)
begin('core',(0,0,0));cyl('core','Foot',1.48,.30,(0,0,.28),'dark');cyl('core','Collar',1.23,.17,(0,0,.49),'armor')
ring('core','LowerTrace',1.09,.06,.055,.60)
part('core','Crystal','diamond',[.78,.78,1.66],[0,0,1.57],'team',bevel=0)
for i in range(4):
 angle=45+i*90;t=math.radians(angle)
 o=box('core','Cage',(.34,.42,1.94),(1.04*math.cos(t),1.04*math.sin(t),1.53),'dark',(0,0,angle),.035)
 o=box('core','CageArmor',(.26,.44,1.52),(1.10*math.cos(t),1.10*math.sin(t),1.55),'armor',(0,0,angle),.035)
 o=box('core','CageTrace',(.06,.46,.70),(1.14*math.cos(t),1.14*math.sin(t),1.55),'team',(0,0,angle),.005)
ring('core','Crown',1.17,.48,.23,2.68,'steel');ring('core','CrownTrace',1.16,.055,.035,2.815)
for i in range(8):
 a=i*math.tau/8;sector('core','CrownPanel',.96,1.44,a+.04,a+math.pi/4-.04,2.85,.10,'armor',1)

begin('bunker',(13,0,0))
box('bunker','Foot',(6.2,4.9,.25),(0,0,.125),'dark',bevel=.04)
box('bunker','SlopingHull',(5.9,4.6,1.55),(0,0,1.02),'steel',bevel=.05,taper=.88)
box('bunker','Roof',(5.90,4.50,.25),(0,0,1.93),'armor',bevel=.04)
for side in [-1,1]:
 box('bunker','SideArmor',(.38,3.70,1.30),(side*2.72,0,1.08),'armor',bevel=.045,taper=.91)
 box('bunker','SideBand',(.06,2.65,.085),(side*2.94,0,1.10),'team',bevel=.005)
 for j in range(3):box('bunker','SideInset',(.025,.50,.38),(side*2.925,-1+j*.95,.60),'dark',bevel=.004)
box('bunker','FaceRecess',(4.60,.15,1.20),(0,2.22,1.03),'dark',bevel=.025)
box('bunker','FacePlate',(4.15,.12,.92),(0,2.34,1.10),'armor',rot=(-8,0,0),bevel=.035)
box('bunker','FaceTrace',(2.05,.035,.085),(0,2.425,1.44),'team',bevel=.005)
for i in range(4):box('bunker','RoofPanel',(.92,1.10,.055),(-1.75+i*1.15,-1.16,2.09),'steel',bevel=.012)
cyl('bunker','TurretBase',.84,.25,(0,.22,2.20),'dark')
ring('bunker','TurretCircuit',.70,.045,.06,2.34,'team',xy=(0,.22))
# Preserve existing DefenseBunker pivot and muzzle EXACTLY. Local Unity (0,2.38,.22).
turret_origin=Vector((0,.22,2.38))
box('bunker','TurretHead',(1.52,1.10,.43),tuple(turret_origin+Vector((0,.05,.17))),'armor',bevel=.035,mount='turret')
box('bunker','TurretFace',(.96,.075,.20),tuple(turret_origin+Vector((0,.64,.17))),'dark',bevel=.01,mount='turret')
for side in [-1,1]:
 cyl('bunker','Barrel',.075,.66,tuple(turret_origin+Vector((side*.22,.75,.17))),'dark',rot=(90,0,0),bevel=.008,mount='turret')
 cyl('bunker','MuzzleRim',.095,.08,tuple(turret_origin+Vector((side*.22,1.08,.17))),'steel',rot=(90,0,0),bevel=.006,mount='turret')

begin('trench',(26,0,0));box('trench','Floor',(7,4.8,.16),(0,0,.08),'dark',bevel=.02)
box('trench','FrontWall',(6.75,.85,.90),(0,1.85,.62),'armor',bevel=.04,taper=.92)
for side in [-1,1]:
 box('trench','SideWall',(.75,3.85,.90),(side*2.97,-.10,.62),'armor',bevel=.04,taper=.92)
 box('trench','SideCap',(.88,3.85,.16),(side*2.97,-.10,1.14),'steel',bevel=.025)
 for j in range(3):box('trench','SidePanel',(.05,.62,.42),(side*3.34,-1.2+j*1.15,.61),'dark',bevel=.01)
box('trench','FrontCap',(6.7,.95,.16),(0,1.85,1.14),'steel',bevel=.025)
box('trench','FrontTrace',(4.4,.035,.075),(0,2.31,.81),'team',bevel=.005)
for i in range(4):box('trench','FloorInset',(3.65,.50,.012),(0,-1.55+i*.69,.17),'steel',bevel=.002)
box('trench','TowerFoot',(1.90,1.80,.23),(0,-.60,.29),'armor',bevel=.035)
box('trench','TowerShaft',(.72,.75,1.65),(0,-.60,1.29),'dark',bevel=.035)
box('trench','TowerFace',(.44,.055,1.12),(0,-.19,1.34),'armor',bevel=.015)
box('trench','TowerTrace',(.085,.025,.78),(0,-.149,1.34),'team',bevel=.004)
for side in [-1,1]:box('trench','TowerProng',(.40,.85,1.08),(side*.52,-.60,2.59),'armor',bevel=.035,taper=.82)
cyl('trench','LensMount',.28,.22,(0,-.25,2.59),'dark',rot=(90,0,0))
cyl('trench','Lens',.20,.028,(0,-.12,2.59),'team',rot=(90,0,0),bevel=.004)

for model,origin,radius,height in [('healpad',(0,13,0),2.795,.06),('healnode',(13,13,0),.85,.04)]:
 begin(model,origin);baseh=.018 if model=='healnode' else .025
 cyl(model,'FlushBase',radius,baseh,(0,0,baseh/2),'dark',bevel=0)
 # All detail lies below the hard height cap; no standing parts and no model colliders.
 sectors=8 if model=='healnode' else 6
 for i in range(sectors):
  a=i*math.tau/sectors;b=(i+1)*math.tau/sectors
  sector(model,'DeckSegment',radius*.20,radius*.76,a+.018,b-.018,height*.50,.010,'steel',2)
  sector(model,'PerimeterArmor',radius*.80,radius*.98,a+.025,b-.025,height*.43,.014,'armor',2)
  sector(model,'HealingTrace',radius*.76,radius*.79,a+.03,b-.03,height*.74,.004,'heal',2)
 cross=.53 if model=='healnode' else .85;thick=.004
 box(model,'CrossA',(.15,cross,thick),(0,0,height*.82),'heal',bevel=0,mount='available' if model=='healnode' else 'static')
 box(model,'CrossB',(cross,.15,thick),(0,0,height*.82),'heal',bevel=0,mount='available' if model=='healnode' else 'static')
 if model=='healnode':
  for obj in models[model]:
   if obj['material']=='heal':obj['mount']='available'

# Evaluate actual Blender bevels, triangulate, then combine by material/mount for few draw calls.
bpy.context.view_layer.update();depsgraph=bpy.context.evaluated_depsgraph_get();export=[];stats={}
for model,objs in models.items():
 groups={};bounds=[]
 for obj in objs:
  evaluated=obj.evaluated_get(depsgraph);mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
  group=groups.setdefault((obj['material'],obj['mount']),dict(material=obj['material'],mount=obj['mount'],vertices=[],normals=[],uv=[],triangles=[]))
  for tri in mesh.loop_triangles:
   corners=[]
   for i in tri.vertices:
    v=obj.matrix_world@mesh.vertices[i].co-origins[model];bounds.append(tuple(v));normal=obj.matrix_world.to_3x3()@tri.normal;normal.normalize()
    if obj['mount']=='turret':v-=turret_origin
    uv=(v.x,v.y) if abs(normal.z)>.6 else ((v.y,v.z) if abs(normal.x)>.6 else (v.x,v.z))
    corners.append(len(group['vertices'])//3)
    group['vertices'] += [round(v.x,6),round(v.z,6),round(v.y,6)]
    group['normals'] += [round(normal.x,6),round(normal.z,6),round(normal.y,6)]
    group['uv'] += [round(uv[0]*.35,6),round(uv[1]*.35,6)]
   group['triangles'] += [corners[0],corners[2],corners[1]]
  evaluated.to_mesh_clear()
 count=sum(len(g['triangles'])//3 for g in groups.values());stats[model]={'triangles':count,'drawGroups':len(groups),'bounds':[[min(v[i] for v in bounds),max(v[i] for v in bounds)] for i in range(3)]}
 export.append({'name':model,'groups':list(groups.values())})
 if model in ('healpad','healnode'):assert stats[model]['bounds'][2][1]<= (.06 if model=='healpad' else .04)
payload=json.dumps({'version':1,'models':export},separators=(',',':')).encode()
(OUT/'StationBuildings.bytes').write_bytes(gzip.compress(payload,compresslevel=9,mtime=0))
(ART/'StationBuildings.json').write_bytes(payload)
(ART/'geometry-report.json').write_text(json.dumps(stats,indent=2))

# Actual model preview, separate from generated concept art.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=900;scene.render.resolution_y=650;scene.render.resolution_percentage=100
scene.world.color=(.25,.25,.25)
for loc,power,size in [((0,-10,18),2100,12),((12,8,12),1800,10)]:
 data=bpy.data.lights.new('Studio','AREA');data.energy=power;data.shape='DISK';data.size=size
 o=bpy.data.objects.new('Studio',data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((10,5,0))-o.location).to_track_quat('-Z','Y').to_euler()
camera_data=bpy.data.cameras.new('Preview');camera=bpy.data.objects.new('Preview',camera_data);scene.collection.objects.link(camera);camera_data.type='ORTHO';scene.camera=camera
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
for model in ['core','bunker','trench','healpad','healnode']:
 visible=[model,'deck'] if model=='core' else [model]
 for name,objs in models.items():
  for o in objs:o.hide_render=name not in visible
 target=origins[model]+Vector((0,0,.95 if model not in ['healpad','healnode'] else 0))
 camera.location=target+Vector((3,14,12));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
 for light,offset in zip([o for o in scene.objects if o.type=='LIGHT'],[(-4,6,10),(6,-3,8)]):
  light.location=target+Vector(offset);light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
 camera_data.ortho_scale={'core':11,'bunker':8.5,'trench':9.4,'healpad':7,'healnode':2.4}[model]
 if '--render' in sys.argv:
  scene.render.filepath=str(ART/(model+'.png'));bpy.ops.render.render(write_still=True)
for o in objects:o.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'StationKit.blend'),compress=True)
print(json.dumps(stats))
