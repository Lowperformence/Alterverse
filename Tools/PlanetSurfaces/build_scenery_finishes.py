"""Deterministic, seamless RG slope / B roughness / A height material data.

No colour paint or normal-map importer encoding; sampled as linear data in ScenerySurface.
NumPy/Pillow only. Run from any directory. Texture GUIDs remain stable across rebuilds.
"""
from pathlib import Path
import hashlib, json, uuid
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/SceneryFinishes'
OUT.mkdir(parents=True,exist_ok=True)
SIZE=256
v,u=np.mgrid[:SIZE,:SIZE]/SIZE
tau=2*np.pi

def wave(x,y,phase=0): return np.sin(tau*(u*x+v*y)+phase)
def noise(seed):
    # Integer Fourier modes are periodic in both axes, including the slope derivative.
    rng=np.random.default_rng(seed)
    result=np.zeros_like(u)
    for i in range(14):
        x,y=rng.integers(1,24,size=2)
        result+=wave(int(x),int(y),float(rng.uniform(0,tau)))/(5+i)
    return result

fields={}
# Brushed metal: directional hairlines with restrained machining/grain variation.
grain=noise(31)
brush=np.sin(tau*(u*61)+wave(2,3)*.35)
fields['Metal']=(.5+brush*.027+wave(3,1)*.026+grain*.035,
                 .32+grain*.09+brush*.025)
# Bark: broad warped ridges, narrow fissures and cross-grain breaks.
ridge=np.cos(tau*u*11+wave(1,2)*.7+wave(2,5)*.28)
crack=np.exp(-((ridge+.96)/.13)**2)
fields['Bark']=(.5+ridge*.12-crack*.14+noise(7)*.07,
                .83+crack*.10+noise(11)*.045)
# Leaf: a central vein family and paired diagonal secondary veins, kept tonal.
primary=np.exp(-(np.sin(tau*u*2)/.10)**2)
secondary=np.exp(-(np.sin(tau*(u*8+v*7)+wave(2,1)*.3)/.18)**2)
fields['Leaf']=(.5+primary*.08+secondary*.035+noise(19)*.018,
                .62-primary*.08-secondary*.04+noise(21)*.025)
# Ice: seamless Voronoi fracture cells with frost patches in the cut boundaries.
rng=np.random.default_rng(73)
dist=[]
for x,y in rng.uniform(0,1,(26,2)):
    dx=np.minimum(abs(u-x),1-abs(u-x));dy=np.minimum(abs(v-y),1-abs(v-y))
    dist.append(np.sqrt(dx*dx+dy*dy))
nearest=np.sort(np.stack(dist),axis=0)
fracture=np.exp(-((nearest[1]-nearest[0])/.008)**2)
frost=(.5+.5*wave(2,3))*fracture
fields['Ice']=(.5-fracture*.055+noise(47)*.017,
               .20+frost*.31+noise(49)*.025)

META='''fileFormatVersion: 2
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
  maxTextureSize: 256
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
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    textureFormat: 4
    textureCompression: 0
    overridden: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: 256
    textureFormat: 4
    textureCompression: 0
    overridden: 1
  userData: Linear RG slope B roughness A height. Mipmapped repeat data; not colour/normal/alpha transparency.
  assetBundleName:
  assetBundleVariant:
'''
report={}
for name,(height,roughness) in fields.items():
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*SIZE*.5
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*SIZE*.5
    slopes=np.stack([dx,dy],axis=-1)
    bound=max(float(np.quantile(abs(slopes),.985)),.001)
    packed=np.stack([.5+np.clip(dx/bound,-1,1)*.47,
                     .5+np.clip(dy/bound,-1,1)*.47,
                     np.clip(roughness,.03,.97),np.clip(height,.03,.97)],axis=-1)
    assert np.isfinite(packed).all() and packed.min()>=0 and packed.max()<=1
    file=OUT/(name+'.png')
    Image.fromarray(np.rint(packed*255).astype(np.uint8)).save(file,optimize=True)
    (OUT/(name+'.png.meta')).write_text(META.format(guid=uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/scenery-finishes/'+name).hex))
    report[name]={'size':[SIZE,SIZE],'sha256':hashlib.sha256(file.read_bytes()).hexdigest(),
                  'heightRange':[float(height.min()),float(height.max())],
                  'roughnessRange':[float(roughness.min()),float(roughness.max())]}
art=ROOT/'ArtSource/PlanetSurfaces';art.mkdir(parents=True,exist_ok=True)
(art/'scenery-finishes-report.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
