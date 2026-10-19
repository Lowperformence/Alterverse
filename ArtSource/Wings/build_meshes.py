"""Bake approved part geometry to the runtime mesh asset. Python stdlib only.
The source uses Blender meters (X horizontal, Z up, -Y rear).
The runtime uses Unity X horizontal, Y up, -Z rear.
"""
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]

def triangles(vertices, face):
    if len(face)==3:return [face]
    # Ear clipping keeps closed rings and concave trim contours intact.
    a,b,c=[vertices[i] for i in face[:3]]
    u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
    normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
    drop=max(range(3),key=lambda i:abs(normal[i]))
    coords={i:[x for j,x in enumerate(vertices[i]) if j!=drop] for i in face}
    def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    area=sum(coords[face[i]][0]*coords[face[(i+1)%len(face)]][1]-coords[face[(i+1)%len(face)]][0]*coords[face[i]][1] for i in range(len(face)))
    sign=1 if area>0 else -1
    ids=list(face);out=[]
    while len(ids)>3:
        for j,b in enumerate(ids):
            a,c=ids[j-1],ids[(j+1)%len(ids)]
            if sign*cross(coords[a],coords[b],coords[c])<=1e-12:continue
            inside=any(all(sign*cross(coords[u],coords[v],coords[k])>1e-12 for u,v in [(a,b),(b,c),(c,a)]) for k in ids if k not in [a,b,c])
            if inside:continue
            out.append([a,b,c]);ids.pop(j);break
        else:raise ValueError('Non-simple or degenerate polygon')
    out.append(ids);return out

def bake(parts, materials):
    mesh=dict(positions=[],colors=[],body=[],facet=[],energy=[])
    for part in parts:
        for f,face in enumerate(part['faces']):
            name=part.get('faceMaterials',[part['material']]*len(part['faces']))[f]
            color,metal,rough,emission=materials[name]
            slot='energy' if emission else 'facet' if name in ['Ivory','PaleGold','Silver','Gunmetal'] else 'body'
            if name.startswith('Spectrum_') and 'Channel' not in part['name'] and 'Bridge' not in part['name']:
                slot='facet'
            for tri in triangles(part['verts'],face):
                # Swap Y/Z to Unity. Duplicate reverse faces: mirrored transform stays visible.
                for indices in [tri[::-1],tri]:
                    for i in indices:
                        x,y,z=part['verts'][i]
                        mesh[slot].append(len(mesh['positions'])//3)
                        mesh['positions'] += [round(x,6),round(z,6),round(y,6)]
                        mesh['colors'] += [round(c,5) for c in part.get('vertexColors',[color]*len(part['verts']))[i]]
    return mesh

def build():
    source=json.loads((HERE/'PremiumWingParts.json').read_text())
    library={}
    for label,key in [('angel','Angel'),('rgb','RGB')]:
        parts=source['models'][key]
        library[label]=dict(wing=bake([p for p in parts if p['side']==1],source['materials']),back=bake([p for p in parts if p['side']==0],source['materials']))
    target=ROOT/'Assets/Alterverse/Resources/Wings/PremiumWingMeshes.json'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(library,separators=(',',':'))+'\n')
    print(target)
    return library

if __name__=='__main__':build()
