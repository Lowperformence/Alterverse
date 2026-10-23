"""Validate headwear exports without Unity, Blender, credentials or third-party packages."""
import json, math, struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'Assets/Alterverse/Resources/Headwear'
PROTOTYPES=('WizardHat','PropellerCap','MushroomHat')
def check_mesh(name):
    data=json.loads((RES/(name+'.json')).read_text())
    vertices=data['vertices'];indices=data['triangles'];sections=data['submeshes'];materials=data['materials']
    assert 0<len(vertices)<=18000 and len(vertices)%3==0,name+' vertex budget'
    assert 0<len(indices)<=18000 and len(indices)%3==0,name+' triangle budget'
    assert len(sections)==len(indices)//3 and len(materials)==6,name+' material layout'
    assert all(math.isfinite(v) and abs(v)<=10 for v in vertices),name+' nonfinite vertex'
    assert all(type(i) is int and 0<=i<len(vertices)//3 for i in indices),name+' invalid index'
    assert all(type(s) is int and 0<=s<6 for s in sections),name+' invalid section'
    for m in materials:
        assert all(math.isfinite(m[k]) and 0<=m[k]<=1 for k in ('r','g','b','metallic','smoothness')),name+' material values'
        assert math.isfinite(m['emission']) and 0<=m['emission']<=32,name+' emission'
    if name in PROTOTYPES:
        for i in range(0,len(indices),3):
            a,b,c=[vertices[j*3:j*3+3] for j in indices[i:i+3]]
            ab=[b[n]-a[n] for n in range(3)];ac=[c[n]-a[n] for n in range(3)]
            cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
            assert sum(x*x for x in cross)>1e-18,name+' degenerate triangle'
        folder=ROOT/'ArtSource/Headwear'/name
        blend=(folder/(name+'.blend')).read_bytes()
        assert len(blend)>10000 and blend.startswith((b'BLENDER',b'\x28\xb5\x2f\xfd')),name+' editable Blender source missing'
        glb=(folder/(name+'.glb')).read_bytes()
        magic,version,size=struct.unpack('<4sII',glb[:12])
        assert magic==b'glTF' and version==2 and size==len(glb),name+' invalid GLB'
    return len(vertices)//3,len(indices)//3
for name in ('UfoTestHat',*PROTOTYPES):
    count=check_mesh(name);print(name+': %d vertices, %d triangles'%count)
for name in ('BetaCap','UfoTestHat',*PROTOTYPES):
    path=RES/(name+'Icon.png');data=path.read_bytes()
    assert data[:8]==b'\x89PNG\r\n\x1a\n'
    width,height,depth,color=struct.unpack('>IIBB',data[16:26])
    assert width>=512 and height>=512 and depth==8 and color==6,name+' preview must be high resolution RGBA'
    assert (path.with_name(path.name+'.meta')).is_file(),name+' preview import metadata missing'
print('PASS: all five high-resolution headwear previews and all four serialized meshes.')

