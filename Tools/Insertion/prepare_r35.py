"""Build separate game assets from the owner's R35 export; leave Assets/R35 untouched.
Usage: python Tools/Insertion/prepare_r35.py R35_komplett.unitypackage
Requires Pillow. The licensed source package is intentionally not redistributed here.
"""
import argparse, hashlib, io, json, re, tarfile
from pathlib import Path
from PIL import Image, ImageOps, ImageChops

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Alterverse/Art/InsertionR35'
RESOURCE=ROOT/'Assets/Alterverse/Resources/InsertionTransport'

def guid(path):
 return hashlib.md5(('dimensions-front/r35/'+path).encode()).hexdigest()

def write(path, data):
 path.parent.mkdir(parents=True,exist_ok=True)
 path.write_bytes(data if isinstance(data,bytes) else data.encode())

def meta(path, importer='DefaultImporter', extra=''):
 rel=path.relative_to(ROOT).as_posix()
 write(Path(str(path)+'.meta'),f'fileFormatVersion: 2\nguid: {guid(rel)}\n{importer}:\n  externalObjects: {{}}\n{extra}  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 return guid(rel)

def folders(path):
 for p in reversed([path,*path.parents]):
  if p==ROOT or ROOT not in p.parents:continue
  if not Path(str(p)+'.meta').exists():
   p.mkdir(parents=True,exist_ok=True)
   write(Path(str(p)+'.meta'),f'fileFormatVersion: 2\nguid: {guid(p.relative_to(ROOT).as_posix())}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

def texture_meta(path, normal=False, linear=False):
 rel=path.relative_to(ROOT).as_posix()
 write(Path(str(path)+'.meta'),f'''fileFormatVersion: 2
guid: {guid(rel)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {0 if linear or normal else 1}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 2
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  compressionQuality: 70
  alphaUsage: 1
  alphaIsTransparency: 0
  textureType: {1 if normal else 0}
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 70
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: 50
    textureCompression: 1
    compressionQuality: 70
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
  userData: R35 game copy; original source untouched
  assetBundleName: 
  assetBundleVariant: 
''')
 return guid(rel)

def material(name,tint,energy,maps):
 def tex(key,value):
  return f'    - {key}:\n        m_Texture: {{fileID: 2800000, guid: {value}, type: 3}}\n        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n'
 def color(c):return '{r: '+str(c[0])+', g: '+str(c[1])+', b: '+str(c[2])+', a: 1}'
 text=f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}}
  m_ValidKeywords:
  - _EMISSION
  - _METALLICSPECGLOSSMAP
  - _NORMALMAP
  m_InvalidKeywords: []
  m_LightmapFlags: 2
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
'''
 for key,value in [('_BaseMap',maps['Albedo']),('_MainTex',maps['Albedo']),('_BumpMap',maps['Normal']),('_EmissionMap',maps['Emission']),('_MetallicGlossMap',maps['MetallicSmoothness'])]:text+=tex(key,value)
 text+='''    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _BumpScale: 0.85
    - _Cull: 2
    - _DstBlend: 0
    - _EnvironmentReflections: 1
    - _Metallic: 1
    - _OcclusionStrength: 1
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: 0.62
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
'''
 for key,c in [('_BaseColor',tint),('_Color',tint),('_EmissionColor',energy)]:text+=f'    - {key}: {color(c)}\n'
 path=RESOURCE/(name+'.mat');write(path,text);return meta(path,'NativeFormatImporter','  mainObjectFileID: 2100000\n')

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('package',type=Path);a=p.parse_args()
 folders(ART);folders(RESOURCE)
 with tarfile.open(a.package,'r:gz') as t:
  bypath={t.extractfile(m).read().decode('utf-8-sig').strip():m.name.split('/')[0] for m in t.getmembers() if m.isfile() and m.name.endswith('/pathname')}
  def source(path):return t.extractfile(bypath['Assets/R35/'+path]+'/asset').read()
  def image(name,size=2048):return Image.open(io.BytesIO(source('Textures/R35_'+name+'.tif'))).convert('RGB').resize((size,size),Image.Resampling.LANCZOS)
  maps={}
  glow=image('Glow',512);channels=glow.split();neutral=ImageChops.lighter(ImageChops.lighter(channels[0],channels[1]),channels[2]);glow=Image.merge('RGB',(neutral,neutral,neutral))
  for name,im in [('Albedo',image('Diffuse5')),('Normal',image('Normal')),('Emission',glow)]:
   path=ART/(name+'.png');im.save(path,optimize=True);maps[name]=texture_meta(path,normal=name=='Normal')
  metal=image('Metallic',1024).getchannel('R');metallic=Image.merge('RGBA',(metal,metal,metal,ImageOps.invert(image('Roughness',1024).convert('L'))))
  path=ART/'MetallicSmoothness.png';metallic.save(path,optimize=True);maps['MetallicSmoothness']=texture_meta(path,linear=True)
  zero=material('R35_Zero',(.79,.82,.92),(1.60,.44,2.55),maps)
  material('R35_Lumen',(.91,.94,.98),(.35,2.1,2.6),maps)
  prefab=source('Dropship_R35.prefab').decode('utf-8-sig')
  # Upgrade the legacy flat prefab; keep the exact mesh fileIDs and artist-authored transforms.
  prefab=re.sub(r'--- !u!1001 .*?(?=--- !u!)','',prefab,flags=re.S)
  prefab=prefab.replace('m_PrefabParentObject:','m_CorrespondingSourceObject:')
  prefab=re.sub(r'  m_PrefabInternal: \{fileID: \d+\}', '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}',prefab)
  prefab=re.sub(r'(m_Materials:\n  - )\{fileID: 2100000, guid: [0-9a-f]{32}, type: [23]\}',r'\g<1>{fileID: 2100000, guid: '+zero+', type: 2}',prefab)
  # Transport presentation has no guns or deployed landing legs.
  for block in re.findall(r'--- !u!1 .*?(?=--- !u!|\Z)',prefab,flags=re.S):
   if re.search(r'm_Name: Dropship_R35-(Laser|Landing)',block):prefab=prefab.replace(block,block.replace('m_IsActive: 1','m_IsActive: 0'))
  path=RESOURCE/'R35.prefab';write(path,prefab);meta(path,'PrefabImporter')
  report={'source':'CGPitbull DropShip R35, Unity Asset Store 121438; owner-supplied export','source_package_sha256':hashlib.sha256(a.package.read_bytes()).hexdigest(),'albedo_variant':'Diffuse5','texture_sizes':{'Albedo':2048,'Normal':2048,'Emission':512,'MetallicSmoothness':1024},'material_pipeline':'URP/Lit','android_texture_format':'ASTC 6x6','original_assets_modified':False,'maps':maps,'runtime_prefab':str(path.relative_to(ROOT))}
  write(ROOT/'Tools/Insertion/r35-report.json',json.dumps(report,indent=2)+'\n')
  print(json.dumps({str(p.relative_to(ROOT)):p.stat().st_size for p in [*ART.glob('*.png'),RESOURCE/'R35.prefab']},indent=2))
if __name__=='__main__':main()
