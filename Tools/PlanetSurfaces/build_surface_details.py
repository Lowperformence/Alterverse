"""Reproducible seamless linear surface-data authoring and Blender material study.
Run: blender -b --python-exit-code 1 --python Tools/PlanetSurfaces/build_surface_details.py
RG = signed slope encoded around .5; B = roughness; A = neutral-centered height/value.
No albedo is baked: the game's established planet palette remains authoritative.
"""
import bpy, numpy as np, math, json, struct, zlib, uuid
from pathlib import Path
from mathutils import Vector
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'Assets/Alterverse').is_dir())
OUT=ROOT/'Assets/Alterverse/Resources/PlanetSurfaces';ART=ROOT/'ArtSource/PlanetSurfaces';OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
SIZE=512
THEMES=['Moon','Mars','GreenPlanet','Noxara','Kryon','Earth','Pyraxis','Neptune','Dark00','DiamondCore','GJ504b']
COLORS=[(.19,.21,.24),(.43,.19,.10),(.19,.29,.12),(.08,.12,.30),(.50,.72,.80),(.27,.28,.13),(.22,.11,.07),(.13,.30,.38),(.09,.09,.12),(.38,.46,.57),(.46,.15,.32)]
y,x=np.mgrid[:SIZE,:SIZE];u=x/SIZE;v=y/SIZE

def noise(cells,seed):
 rng=np.random.default_rng(seed);g=rng.random((cells,cells))
 px=u*cells;py=v*cells;ix=np.floor(px).astype(int);iy=np.floor(py).astype(int);fx=px-ix;fy=py-iy
 fx=fx*fx*fx*(fx*(fx*6-15)+10);fy=fy*fy*fy*(fy*(fy*6-15)+10)
 a=g[iy%cells,ix%cells]*(1-fx)+g[iy%cells,(ix+1)%cells]*fx
 b=g[(iy+1)%cells,ix%cells]*(1-fx)+g[(iy+1)%cells,(ix+1)%cells]*fx
 return (a*(1-fy)+b*fy)-.5

def cells(count,seed):
 rng=np.random.default_rng(seed);jitter=rng.uniform(.12,.88,(count,count,2));px=u*count;py=v*count;ix=np.floor(px).astype(int);iy=np.floor(py).astype(int)
 first=np.full_like(u,100.);second=first.copy()
 for oy in [-1,0,1]:
  for ox in [-1,0,1]:
   j=jitter[(iy+oy)%count,(ix+ox)%count];d=(ix+ox+j[:,:,0]-px)**2+(iy+oy+j[:,:,1]-py)**2
   second=np.minimum(second,np.maximum(first,d));first=np.minimum(first,d)
 return np.sqrt(first),np.sqrt(second)-np.sqrt(first)

def field(theme,rock,seed):
 broad=noise(4,seed);mid=noise(13,seed+1);fine=noise(47,seed+2);grain=noise(113,seed+3)
 f,edge=cells(12 if rock else 17,seed+9)
 cracks=np.exp(-edge*65);pores=np.exp(-(f/.14)**2)
 ripple=np.sin(2*np.pi*(u*11+v*3)+broad*10+mid*1.7)
 h=broad*.08+mid*.04+fine*.016+grain*.009
 rough=.84
 if theme in ['Mars','GJ504b']:
  h+=ripple*(.020 if theme=='Mars' else .029)+np.sin(2*np.pi*(u*23+v*6)+broad*20)*.005
  if rock:h+=mid*.065-cracks*.018
  rough=.91 if not rock else .80
 elif theme in ['Moon','Dark00']:
  h+=mid*.03-pores*(.024 if theme=='Moon' else .019)-cracks*(.020 if rock else .004)
  rough=.94 if not rock else .86
 elif theme in ['Earth','GreenPlanet']:
  h+=np.maximum(0,.28-f)*(.12 if rock else .065)+fine*.022+mid*.024
  if theme=='GreenPlanet':h+=np.maximum(mid,0)*.065
  if rock:h-=cracks*.017
  rough=.86 if theme=='Earth' else .92
 elif theme=='Kryon':
  h=mid*.032+fine*.012+grain*.006-cracks*(.037 if rock else .021)+edge*.022
  rough=.36 if rock else .62
 elif theme=='Noxara':
  h+=edge*.075-cracks*.028+np.abs(mid)*.07
  rough=.58 if rock else .72
 elif theme=='Pyraxis':
  h+=mid*.060-cracks*.032-pores*.036+fine*.025
  rough=.91
 elif theme=='Neptune':
  h+=mid*.045+np.sin(2*np.pi*(u*4+v*13)+broad*7)*.013-pores*.020
  rough=.40 if rock else .65
 elif theme=='DiamondCore':
  h=edge*.11-cracks*.020+mid*.016+fine*.004
  rough=.27 if rock else .49
 if rock:h+=broad*.035+mid*.035
 h-=h.mean()
 dx=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*SIZE*.5
 dz=(np.roll(h,-1,axis=0)-np.roll(h,1,axis=0))*SIZE*.5
 scale=.045 if rock else .065
 data=np.stack([.5+np.clip(dx*scale,-.48,.48),.5+np.clip(dz*scale,-.48,.48),np.clip(rough+mid*.15+cracks*.10-pores*.05,0,1),np.clip(.5+h*2.0,.12,.88)],axis=-1)
 return h,np.round(data*255).astype(np.uint8)

def png(path,data):
 def chunk(kind,value):return struct.pack('>I',len(value))+kind+value+struct.pack('>I',zlib.crc32(kind+value)&0xffffffff)
 raw=b''.join(b'\0'+row.tobytes() for row in data[::-1])
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',SIZE,SIZE,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))

def meta(path):
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/planet-surfaces/'+str(path.relative_to(ROOT))).hex
 path.with_name(path.name+'.meta').write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
  isReadable: 0
  streamingMipmaps: 1
  streamingMipmapsPriority: 0
  textureFormat: 4
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  compressionQuality: 100
  alphaSource: 1
  alphaIsTransparency: 0
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
  userData: Linear numerical RG slope B roughness A height; never import as normal map or transparency.
  assetBundleName:
  assetBundleVariant:
''')

bpy.ops.wm.read_factory_settings(use_empty=True);manifest=[]
for index,theme in enumerate(THEMES):
 folder=OUT/theme;folder.mkdir(exist_ok=True)
 for rock in [False,True]:
  name='Stone' if rock else 'Ground';h,data=field(theme,rock,1703+index*101+(37 if rock else 0));path=folder/(name+'.png');png(path,data);meta(path)
  # Validate periodic edge jumps against interior differences; the boundary is a sampled interval, not duplicated pixels.
  edges=max(float(np.abs(h[:,-1]-h[:,0]).mean()),float(np.abs(h[-1,:]-h[0,:]).mean()))
  interior=max(float(np.abs(np.diff(h,axis=0)).mean()),float(np.abs(np.diff(h,axis=1)).mean()))
  assert edges<interior*3+1e-5,(theme,name,'seam')
  manifest.append({'theme':theme,'surface':name,'size':SIZE,'heightMean':float(h.mean()),'seamMean':edges,'interiorStepMean':interior,'roughnessMean':float(data[:,:,2].mean()/255),'path':str(path.relative_to(ROOT))})
  # A material study with real displaced geometry, UVs and the packed roughness channel.
  n=97;verts=[];faces=[];offset=(index%4)*5.0;row=(index//4)*5.0
  for j in range(n):
   for i in range(n):verts.append((offset+(i/(n-1)-.5)*2.1+(-1.14 if not rock else 1.14),row+(j/(n-1)-.5)*3.6,float(h[int(j/(n-1)*(SIZE-1)),int(i/(n-1)*(SIZE-1))])*.65))
  for j in range(n-1):
   for i in range(n-1):a=j*n+i;faces.append((a,a+1,a+n+1,a+n))
  mesh=bpy.data.meshes.new(theme+name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(theme+' '+name,mesh);bpy.context.collection.objects.link(obj)
  uv=mesh.uv_layers.new(name='UVMap')
  for poly in mesh.polygons:
   poly.use_smooth=True
   for loop in poly.loop_indices:
    vertex=mesh.loops[loop].vertex_index;uv.data[loop].uv=((vertex%n)/(n-1),(vertex//n)/(n-1))
  mat=bpy.data.materials.new(theme+' '+name);mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*COLORS[index],1)
  tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(path));tex.image.colorspace_settings.name='Non-Color';tex.image.alpha_mode='CHANNEL_PACKED';tex.image.pack()
  separate=nodes.new('ShaderNodeSeparateColor');links.new(tex.outputs['Color'],separate.inputs[0]);links.new(separate.outputs['Blue'],bs.inputs['Roughness'])
  bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.35;bump.inputs['Distance'].default_value=.06;links.new(tex.outputs['Alpha'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal']);obj.data.materials.append(mat)
 print('SURFACE_AUTHORED',theme,flush=True)
 # Label the pair in the saved material study.
 curve=bpy.data.curves.new(theme+' label','FONT');curve.body=theme+'   |   ground / stone';curve.size=.24;obj=bpy.data.objects.new(theme+' label',curve);bpy.context.collection.objects.link(obj);obj.location=((index%4)*5-2.2,(index//4)*5-2.15,.05)

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=False
scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.15,.20,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
for name,loc,power,size in [('Key',(2,-3,9),3200,7),('Fill',(15,8,9),1800,9)]:
 light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;obj=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(obj);obj.location=loc;obj.rotation_euler=(Vector((7,5,0))-obj.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.cameras.new('Surface study');obj=bpy.data.objects.new('Surface study',cam);bpy.context.collection.objects.link(obj);obj.location=(7.5,-7,21);obj.rotation_euler=(Vector((7.5,5,0))-obj.location).to_track_quat('-Z','Y').to_euler();cam.type='ORTHO';cam.ortho_scale=21;scene.camera=obj
scene.render.resolution_x=1800;scene.render.resolution_y=1400;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ART/'SurfaceStudy.png');scene.view_settings.view_transform='AgX'
(ART/'manifest.json').write_text(json.dumps(manifest,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ART/'SurfaceStudy.blend'),compress=True);bpy.ops.render.render(write_still=True)
print('SURFACE_COMPLETE',len(manifest),flush=True)
