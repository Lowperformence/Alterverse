#!/usr/bin/env python3
"""Own deterministic, periodic material microdata; no third-party assets.

RG signed world-UV slopes, B roughness, A neutral height/value. Linear RGBA8,
repeat/trilinear/mips, including Android. Existing geological maps stay intact.
Run with NumPy/Pillow; --check verifies committed bytes without rewriting them.
"""
from pathlib import Path
import argparse, hashlib, io, json, uuid
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
SIZE=256
v,u=np.mgrid[:SIZE,:SIZE]/SIZE

def noise(cells,seed):
    rng=np.random.default_rng(seed);grid=rng.uniform(-.5,.5,(cells,cells))
    px=u*cells;py=v*cells;ix=np.floor(px).astype(int);iy=np.floor(py).astype(int)
    fx=px-ix;fy=py-iy
    fx=fx**3*(fx*(fx*6-15)+10);fy=fy**3*(fy*(fy*6-15)+10)
    a=grid[iy%cells,ix%cells]*(1-fx)+grid[iy%cells,(ix+1)%cells]*fx
    b=grid[(iy+1)%cells,ix%cells]*(1-fx)+grid[(iy+1)%cells,(ix+1)%cells]*fx
    return a*(1-fy)+b*fy

def cells(count,seed):
    rng=np.random.default_rng(seed);j=rng.uniform(.12,.88,(count,count,2))
    px=u*count;py=v*count;ix=np.floor(px).astype(int);iy=np.floor(py).astype(int)
    nearest=np.full_like(u,100.)
    for oy in (-1,0,1):
        for ox in (-1,0,1):
            offset=j[(iy+oy)%count,(ix+ox)%count]
            d=(ix+ox+offset[:,:,0]-px)**2+(iy+oy+offset[:,:,1]-py)**2
            nearest=np.minimum(nearest,d)
    return np.sqrt(nearest)

def fields():
    fine=noise(53,41);coarse=noise(11,43);grain=cells(31,47)
    # Rounded, randomly placed grains: no second miniature copy of the dune waves.
    sand=.5+np.exp(-(grain/.30)**2)*.085+fine*.020+coarse*.012
    # Irregular aggregates with interstitial pores and compacted patches.
    clods=noise(15,61);pores=np.exp(-(cells(19,67)/.11)**2)
    soil=.5+clods*.17+noise(37,69)*.055-pores*.07
    # Fine cement paste, exposed aggregate and sparse pinholes, without rock cracks.
    aggregate=np.exp(-(cells(12,79)/.24)**2);holes=np.exp(-(cells(23,83)/.075)**2)
    concrete=.5+noise(7,89)*.025+aggregate*.040+noise(57,97)*.026-holes*.045
    # Fine mineral facets/crystal grains subordinate to the authored large fractures.
    stone=.5+noise(17,101)*.12+noise(43,103)*.030+noise(7,107)*.018
    return {'Sand':(sand,.91+coarse*.045),
            'Soil':(soil,.87+clods*.12+pores*.04),
            'Concrete':(concrete,.83+aggregate*.06+holes*.04+noise(19,109)*.07),
            'Stone':(stone,.78+noise(17,113)*.14)}

def meta(name):
    # Same import contract as existing scenery data; stable GUID, owned by this asset.
    template=(ROOT/'Tools/PlanetSurfaces/build_scenery_finishes.py').read_text().split("META='''",1)[1].split("'''",1)[0]
    return template.format(guid=uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/surface-micro/'+name).hex)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    out=ROOT/'Assets/Alterverse/Resources/SurfaceMicro';out.mkdir(parents=True,exist_ok=True)
    report={}
    for name,(height,rough) in fields().items():
        height=height-height.mean()+.5
        dx=(np.roll(height,-1,1)-np.roll(height,1,1))*SIZE*.5
        dy=(np.roll(height,-1,0)-np.roll(height,1,0))*SIZE*.5
        bound=max(float(np.quantile(abs(np.stack([dx,dy])),.995)),.001)
        # Value is neutral-centred and normalized to a readable range, independent
        # of physical relief. Sparse concrete pores should not disappear in grey.
        value=.5+(height-.5)*min(12.,.11/max(float(height.std()),1e-5))
        packed=np.stack([.5+np.clip(dx/bound,-1,1)*.47,.5+np.clip(dy/bound,-1,1)*.47,
                         np.clip(rough,.03,.97),np.clip(value,.03,.97)],-1)
        assert np.isfinite(packed).all()
        data=np.rint(packed*255).astype(np.uint8)
        # PNG rows are top-down; Unity UV.y is bottom-up. Encode gradients accordingly.
        stream=io.BytesIO();Image.fromarray(data[::-1]).save(stream,format='PNG',optimize=True)
        content=stream.getvalue();path=out/(name+'.png');importer=Path(str(path)+'.meta')
        if args.check:
            assert path.read_bytes()==content,(name,'non-reproducible texture')
            assert importer.read_text()==meta(name),(name,'import settings drift')
        else:path.write_bytes(content);importer.write_text(meta(name))
        # Boundary sample intervals should behave like interior intervals, never duplicate edges.
        seams=[np.abs(height[:,-1]-height[:,0]).mean(),np.abs(height[-1,:]-height[0,:]).mean()]
        interior=[np.abs(np.diff(height,axis=1)).mean(),np.abs(np.diff(height,axis=0)).mean()]
        assert max(seams)<max(interior)*2,(name,'visible repeat seam')
        report[name]={'path':str(path.relative_to(ROOT)),'sha256':hashlib.sha256(content).hexdigest(),
                      'size':[SIZE,SIZE],'heightMean':float(height.mean()),
                      'roughnessMean':float(data[:,:,2].mean()/255),'valueDeviation':float(data[:,:,3].std()/255),
                      'boundaryStep':list(map(float,seams)),'interiorStep':list(map(float,interior))}
    if not args.check:
        (ROOT/'ArtSource/PlanetSurfaces/2026-10-09/micro-finishes.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({'materials':len(report),'reproducible':args.check,'rgba8MipMemoryMiB':4*SIZE*SIZE*4*4/3/1024**2}))

if __name__=='__main__':main()
