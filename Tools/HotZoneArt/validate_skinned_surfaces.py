"""Validate Blender UV seams against the exact original monster topology and skin weights."""
from pathlib import Path
import gzip, hashlib, json, struct
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
reports=json.loads((ROOT/'ArtSource/HotZoneMonsters/skinned-surfaces-report.json').read_text())
assert len(reports)==7
for row in reports:
    folder=ROOT/'Assets/Alterverse/Resources'/row['resource']
    source=(folder/'Model.bytes').read_bytes();model=json.loads(gzip.decompress(source))
    payload=gzip.decompress((folder/'SkinUV.bytes').read_bytes());offset=0
    def take(fmt):
        global offset
        value=struct.unpack_from(fmt,payload,offset);offset+=struct.calcsize(fmt);return value
    magic,version,original,count,slots=take('<4sIIII')
    assert magic==b'DFUV' and version==1 and original==len(model['boneIndices']) and slots==len(model['submeshes'])
    assert payload[offset:offset+32]==hashlib.sha256(source).digest();offset+=32
    mapping=np.empty(count,dtype=int);uv=np.empty((count,2))
    for i in range(count):mapping[i],uv[i,0],uv[i,1]=take('<Iff')
    assert mapping.min()>=0 and mapping.max()<original and np.isfinite(uv).all()
    assert uv.min()>=-.001 and uv.max()<=1.001
    # This mobile pass adds seams, never new triangles or animation bones.
    assert count<65536 and count<=original*2.1
    for sub in model['submeshes']:
        length,=take('<I');assert length==len(sub['triangles'])
        indices=np.array(take('<'+'I'*length),dtype=int)
        assert length==0 or (indices.min()>=0 and indices.max()<count)
        assert np.array_equal(mapping[indices],sub['triangles'])
    assert offset==len(payload)
    for kind in ['Surface','Normal']:
        assert (ROOT/'Assets/Alterverse/Resources/AuthoredHotZone'/f"{row['family']}{kind}.png").exists()
    print(row['resource'],': UVs, exact triangle order, skin bindings and source hash passed;',count,'vertices')
print('All seven skinned monsters passed; original Model.bytes files remain unchanged.')
