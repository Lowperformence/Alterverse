#!/usr/bin/env python3
"""Build exact tileable RG slope / B roughness / A value data for structures.

Numerical inputs for the existing triplanar shader, not colour/normal-map images.
NumPy/Pillow only. --check verifies the generated PNG bytes and import settings.
"""
import argparse,hashlib,io,uuid
from pathlib import Path
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/StructureFinishes'
SIZE=256
v,u=np.mgrid[:SIZE,:SIZE]/SIZE

def noise(cells,seed):
    rng=np.random.default_rng(seed);grid=rng.uniform(-.5,.5,(cells,cells))
    x=u*cells;y=v*cells;ix=np.floor(x).astype(int);iy=np.floor(y).astype(int)
    a=x-ix;b=y-iy;a=a*a*(3-2*a);b=b*b*(3-2*b)
    return (grid[iy%cells,ix%cells]*(1-a)+grid[iy%cells,(ix+1)%cells]*a)*(1-b)+(grid[(iy+1)%cells,ix%cells]*(1-a)+grid[(iy+1)%cells,(ix+1)%cells]*a)*b

def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)

def field(name):
    fine=noise(59,101);coarse=noise(9,103)
    px=np.mod(u*2,1);py=np.mod(v*2,1)
    edge=np.minimum(np.minimum(px,1-px),np.minimum(py,1-py))
    seams=1-smooth(.009,.030,edge)
    if name=='Concrete':
        pores=np.maximum(0,-noise(37,107)-.24)**2*2.2
        height=.51+coarse*.13+fine*.025-seams*.18-pores
        value=.53+coarse*.34+fine*.065-seams*.40-pores*1.7
        roughness=.88+coarse*.055+seams*.045
    elif name=='Deck':
        diagonal=np.mod((u+v)*12,1)
        ribs=(1-smooth(.035,.13,np.minimum(diagonal,1-diagonal)))*(1-seams)
        fasteners=np.zeros_like(u)
        for x in (.045,.455,.545,.955):
            for y in (.045,.455,.545,.955):
                dx=np.minimum(abs(u-x),1-abs(u-x));dy=np.minimum(abs(v-y),1-abs(v-y))
                fasteners=np.maximum(fasteners,1-smooth(.007,.016,np.sqrt(dx*dx+dy*dy)))
        height=.52-seams*.19+ribs*.022-fasteners*.055+fine*.009
        value=.54-seams*.39+ribs*.070-fasteners*.19+coarse*.13
        roughness=.75+seams*.15-ribs*.05+coarse*.08
    elif name=='Metal':
        brush=np.sin(2*np.pi*(u*47+noise(3,109)*.12))
        cut=np.maximum(0,np.sin(2*np.pi*(u*7+v*3))-.92)*(1+coarse)
        height=.52+brush*.010+coarse*.050-cut*.15
        value=.54+brush*.050+coarse*.23-cut*.50
        roughness=.78+coarse*.10+fine*.025+cut*.12
    else:raise ValueError(name)
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*SIZE*.5
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*SIZE*.5
    limit=max(float(np.quantile(np.abs(np.stack([dx,dy])),.99)),.001)
    packed=np.stack([.5+np.clip(dx/limit,-1,1)*.46,.5+np.clip(dy/limit,-1,1)*.46,np.clip(roughness,.08,.98),np.clip(value,.06,.94)],axis=-1)
    assert np.isfinite(packed).all() and packed.min()>=0 and packed.max()<=1
    return np.rint(packed*255).astype(np.uint8)

def meta(name):
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/structure-finishes/'+name).hex
    # Preserve the project's established linear data importer contract.
    template=(ROOT/'Assets/Alterverse/Resources/SceneryFinishes/Metal.png.meta').read_text()
    import re
    template=re.sub(r'^guid: .*$', 'guid: '+guid,template,flags=re.M)
    return re.sub(r'^  userData: .*$', '  userData: Linear structure RG slope B roughness A value. Mipmapped repeat data.',template,flags=re.M)

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');args=p.parse_args()
    if not args.check:OUT.mkdir(parents=True,exist_ok=True)
    for name in ('Concrete','Deck','Metal'):
        buffer=io.BytesIO();Image.fromarray(field(name)).save(buffer,format='PNG',optimize=True)
        data=buffer.getvalue();path=OUT/(name+'.png');metadata=path.with_suffix('.png.meta')
        if args.check:
            assert path.read_bytes()==data,name+' data differs from generator'
            assert metadata.read_text()==meta(name),name+' import settings differ'
        else:path.write_bytes(data);metadata.write_text(meta(name))
        print(name+': '+hashlib.sha256(data).hexdigest()[:16])
if __name__=='__main__':main()
