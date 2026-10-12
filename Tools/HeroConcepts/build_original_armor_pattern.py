"""Original deterministic pigment pattern; no image input, photograph or stock texture.

This replacement retains the existing WoodlandCamo resource/GUID and the four
input pigment tones consumed by the current Auren shader. New shapes come only
from the explicit seeded periodic synthesis below.
"""
from pathlib import Path
import hashlib, json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
SEED=20261012
def build():
    size=1024;rng=np.random.default_rng(SEED)
    y,x=np.mgrid[0:size,0:size]/(size-1)
    field=np.zeros((size,size));fine=np.zeros_like(field)
    for i in range(28):
        a,b=rng.integers(-9,10,size=2);phase=rng.uniform(0,2*np.pi)
        if a==0 and b==0:a=1
        field+=np.sin(2*np.pi*(a*x+b*y)+phase)/(1+a*a+b*b)**.7
    for i in range(12):
        a,b=rng.integers(20,70,size=2);phase=rng.uniform(0,2*np.pi)
        fine+=np.sin(2*np.pi*(a*x+b*y)+phase)/120
    field+=fine
    thresholds=np.quantile(field,[.12,.32,.60]);index=np.digitize(field,thresholds)
    # Shader's four canonical source pigments: graphite, beige, brown, olive.
    palette=np.array([[.2572,.2502,.2345],[.6665,.6228,.5205],[.3807,.3276,.2710],[.3994,.4233,.2523]])
    grain=1+fine[...,None]*.12
    pixels=np.uint8(np.clip(palette[index]*grain,0,1)*255+.5)
    pixels[-1]=pixels[0];pixels[:,-1]=pixels[:,0]
    path=ROOT/'Assets/Alterverse/Resources/HeroArt/WoodlandCamo.png';path.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray(pixels,'RGB').save(path,optimize=True)
    # A preview of the exact current shader's color remapping, in sRGB.
    source=pixels.astype(float)/255
    pigments=np.array([[.3994,.4233,.2523],[.3807,.3276,.2710],[.6665,.6228,.5205],[.2572,.2502,.2345]])
    target=np.array([[.48,.30,.65],[.23,.40,.65],[.6665,.6228,.5205],[.16,.19,.27]])
    distance=np.sum((source[:,:,None,:]-pigments)**2,axis=-1)
    weights=1/np.maximum(distance,.0001)**2;weights/=weights.sum(axis=-1,keepdims=True)
    color=weights@target;lum=np.array([.299,.587,.114]);reference=(weights@pigments)@lum
    color*=np.clip((source@lum)/np.maximum(reference,.001),.6,1.4)[...,None]
    preview=ROOT/'ArtSource/Heroes/ConceptRebuild/Auren-original-coating.png';preview.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray(np.uint8(np.clip(color,0,1)*255+.5),'RGB').save(preview,optimize=True)
    record={'revision':'auren-own-coating-20261012-v1','createdOn':'2026-10-12','generation':'deterministic NumPy periodic synthesis; no external bitmap/reference input','seed':SEED,'size':[size,size],'resource':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'existingMetaGuidPreserved':True,'existingShaderColorwayPreserved':True,'seamPixelsIdentical':bool(np.array_equal(pixels[0],pixels[-1]) and np.array_equal(pixels[:,0],pixels[:,-1]))}
    out=ROOT/'Documentation/Provenance/OriginalAurenCoating.json';out.write_text(json.dumps(record,indent=2)+'\n')
    print(json.dumps(record));return preview
if __name__=='__main__':build()
