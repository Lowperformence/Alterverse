"""Exact orthographic and exploded projections of the shipping mesh, no invented dimensions."""
from pathlib import Path
import gzip,json,math
import numpy as np
import cairosvg
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/BrakkFists'
d=json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/BrakkFists/Model.bytes').read_bytes()))
fit=json.loads((ART/'fit-contract.json').read_text())
h=next(h for h in d['hands'] if h['side']=='R')
sizeFactor=d.get('sizeRelativeToInitialV2',1.0)
v=np.array(h['vertices']).reshape(-1,3)
# Convert Unity model axes back to native Blender, then to the actual wrist frame.
C=np.array([[1,0,0],[0,0,-1],[0,1,0]])
B=np.array(fit['handBindMatrices']['R'])
v=(np.c_[v@C.T,np.ones(len(v))]@np.linalg.inv(B).T)[:,:3]
groups={};vertexGroup={}
for part in h['parts']:
    indices=np.arange(part['firstVertex'],part['firstVertex']+part['vertexCount'])
    groups.setdefault(part['group'],[]).extend(indices.tolist())
    for i in indices:vertexGroup[i]=part['group']
shifts={'core':(0,0,0),'wrist':(0,-.14,0),'dorsal':(0,0,.115),'knuckles':(0,.115,0),
        'fingers':(0,0,-.09),'thumb':(-.125,0,0),'rails':(.125,0,0),'hardware':(0,0,.20)}
labels={'core':'01  Graphit-Grundkoerper','wrist':'02  Offene Manschette + Futter','dorsal':'03  Handrueckenplatte + Einlagen',
        'knuckles':'04  Vier Knoechelpanzer + Einsaetze','fingers':'05  Vier Fingersegmente + Schutzplatten',
        'thumb':'06  Daumenschale + Gelenk','rails':'07  Seitliche Halteschienen','hardware':'08  Vier versenkte Schrauben'}
colors=[(34,40,45),(202,154,70),(72,86,96),(255,145,24),(18,24,30)]
out=['<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1160" viewBox="0 0 1600 1160">',
     '<rect width="1600" height="1160" fill="#f4f0e7"/>',
     '<style>text{font-family:DejaVu Sans,Arial,sans-serif;fill:#222d36}.title{font-size:38px;font-weight:700}.sub{font-size:18px}.label{font-size:17px}.small{font-size:14px}.section{font-size:21px;font-weight:600}.dim{stroke:#536678;fill:none;stroke-width:1.5}</style>',
     '<text class="title" x="48" y="62">BRAKK ORUN / FORGE GRIP V2</text>',
     '<text class="sub" x="49" y="93">Sprengzeichnung aus der tatsaechlich exportierten Modellgeometrie - rechte Faust</text>',
     '<line x1="48" y1="116" x2="1552" y2="116" stroke="#b7c0c5"/>',
     '<text class="section" x="50" y="155">BAUGRUPPEN UND MONTAGE</text>']
def text(x,y,s,cls='label'):out.append(f'<text class="{cls}" x="{x}" y="{y}">{s}</text>')
def project(points,axes):return points@np.array(axes).T
iso=np.array([[.82,-.57,0],[.28,.40,-.87],[-.50,-.71,-.49]])
def draw(points,axes,center,scale):
    projected=project(points,axes);faces=[]
    for material,sub in enumerate(h['submeshes']):
        for triangle in np.array(sub['triangles']).reshape(-1,3):
            p=projected[triangle]
            n=np.cross(p[1]-p[0],p[2]-p[0])
            if n[2]>=-1e-10:continue
            shade=.68+.32*abs(n[2])/max(np.linalg.norm(n),1e-20)
            color=tuple(int(c*shade) for c in colors[material]);faces.append((p[:,2].mean(),p,color))
    for depth,p,color in sorted(faces,key=lambda f:-f[0]):
        q=p[:,:2]*scale+center
        poly=' '.join(f'{x:.2f},{y:.2f}' for x,y in q)
        out.append(f'<polygon points="{poly}" fill="rgb{color}" stroke="rgb{color}" stroke-width=".35"/>')
    return projected
exploded=v.copy()
for group,indices in groups.items():exploded[indices]+=np.array(shifts[group])*sizeFactor
projected=project(exploded,iso)[:,:2]
span=projected.max(0)-projected.min(0)
explScale=min(900/span[0],455/span[1])
explCenter=np.array([515,412.5])-(projected.max(0)+projected.min(0))/2*explScale
draw(exploded,iso,explCenter,explScale)
for i,group in enumerate(['hardware','dorsal','knuckles','wrist','core','thumb','rails','fingers']):
    x,y=project(exploded[groups[group]],iso)[:,:2].mean(axis=0)*explScale+explCenter
    yy=195+i*57
    out.append(f'<path d="M{x:.1f},{y:.1f} L1040,{yy-6} L1090,{yy-6}" fill="none" stroke="#6a7982" stroke-width="1.2"/>')
    out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="3" fill="#465762"/>')
    text(1104,yy,labels[group])
text(50,690,'Explosionsabstaende zeigen nur die Montagefolge; alle Bauteile stammen aus Model.bytes.','small')
out.append('<line x1="48" y1="720" x2="1552" y2="720" stroke="#b7c0c5"/>')
text(50,757,'ORTHOGRAFISCHE ANSICHTEN / BLENDER-MASSE','section')
views=[('HANDRUECKEN',np.array([[1,0,0],[0,-1,0],[0,0,-1]]),(270,944)),
       ('SEITENANSICHT',np.array([[0,1,0],[0,0,-1],[-1,0,0]]),(760,944)),
       ('FAUSTFRONT',np.array([[-1,0,0],[0,0,-1],[0,-1,0]]),(1250,944))]
for name,axes,center in views:
    p=project(v,axes);mid=(p[:,:2].min(0)+p[:,:2].max(0))/2
    viewScale=850/sizeFactor
    adjusted=(center[0]-mid[0]*viewScale,center[1]-mid[1]*viewScale)
    draw(v,axes,adjusted,viewScale)
    text(center[0]-120,803,name,'section')
    low=p[:,:2].min(0)*viewScale+adjusted;high=p[:,:2].max(0)*viewScale+adjusted
    yy=high[1]+28
    out.append(f'<path class="dim" d="M{low[0]},{high[1]+7} V{yy+8} M{high[0]},{high[1]+7} V{yy+8} M{low[0]},{yy} H{high[0]}"/>')
    text((low[0]+high[0])/2-35,yy-5,f'{(p[:,0].max()-p[:,0].min())*1000:.1f} mm','small')
text(50,1130,'Einheit: Meter (Blender Unit Scale 1). Laufzeit: vorhandener Handfaktor 0,8. Links ist geometrisch gespiegelt.','small')
out.append('</svg>')
path=ART/'BrakkFists-Sprengzeichnung.svg';path.write_text('\n'.join(out))
cairosvg.svg2png(url=str(path),write_to=str(ART/'BrakkFists-Sprengzeichnung.png'))
print('Exact blueprint saved',path)
