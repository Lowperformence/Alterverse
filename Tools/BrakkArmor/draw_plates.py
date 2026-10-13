"""Measured per-plate exploded projections from the actual Blender-exported faces."""
import json,math,html
from pathlib import Path
import numpy as np
import cairosvg
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/BrakkArmor';OUT=ART/'plates';OUT.mkdir(exist_ok=True)
entries=json.loads((ART/'diagram-geometry.json').read_text())
colors=[(.13,.17,.20),(.82,.56,.18),(.30,.36,.40),(.045,.065,.078),(1,.30,.02)]
P=np.array([[.866,0,-.5],[-.25,-.866,-.433],[.433,-.25,.866]])
light=np.array([.4,.65,1.]);light/=np.linalg.norm(light)

def text(x,y,body,size=20,fill='#14242c',weight='400',anchor='start'):
 return f'<text x="{x}" y="{y}" font-family="DejaVu Sans,sans-serif" font-size="{size}" fill="{fill}" font-weight="{weight}" text-anchor="{anchor}">{html.escape(str(body))}</text>'
def project(parts,exploded):
 faces=[];all2=[]
 for part in parts:
  v=np.array(part['vertices']);v=v+np.array([0,0,part['layer']*.14 if exploded else 0]);projected=v@P.T
  all2.extend(projected[:,:2])
  for ids in part['faces']:
   pos=v[ids];normal=np.cross(pos[1]-pos[0],pos[2]-pos[0]);length=np.linalg.norm(normal)
   if length<1e-12:continue
   normal/=length
   shade=.65+.35*abs(np.dot(normal,light));rgb=tuple(int(min(255,255*c*shade)) for c in colors[part['material']])
   faces.append((float(projected[ids,2].mean()),projected[ids,:2],f'rgb{rgb}'))
 return faces,np.array(all2)
def draw_view(parts,exploded,box):
 faces,pts=project(parts,exploded);lo,hi=pts.min(0),pts.max(0);size=hi-lo
 x,y,w,h=box;scale=min(w/size[0],h/size[1]);shift=np.array([x+w/2,y+h/2])-(hi+lo)*scale/2
 svg=[]
 for _,polygon,fill in sorted(faces,key=lambda e:e[0]):
  pp=polygon*scale+shift;coords=' '.join(f'{a:.2f},{b:.2f}' for a,b in pp)
  svg.append(f'<polygon points="{coords}" fill="{fill}" stroke="#263039" stroke-width=".65" stroke-linejoin="round"/>')
 if exploded:
  targets=[]
  for layer in range(5):
   group=[p for p in parts if p['layer']==layer];v=np.vstack([p['vertices'] for p in group]);v[:,2]+=layer*.14
   pp=v@P[:2].T;edge=np.array([pp[:,0].max(),pp[:,1].mean()])*scale+shift
   targets.append((layer,edge))
  previous=-1e9
  for layer,edge in sorted(targets,key=lambda a:a[1][1]):
   tx=x+w+18;ty=max(edge[1],previous+45);previous=ty
   svg.append(f'<path d="M {edge[0]:.1f},{edge[1]:.1f} L {tx-28:.1f},{ty:.1f} L {tx-13:.1f},{ty:.1f}" stroke="#52636a" fill="none"/>')
   svg.append(f'<circle cx="{tx}" cy="{ty}" r="16" fill="#fff" stroke="#b47c28" stroke-width="2"/>')
   svg.append(text(tx,ty+6,layer+1,17,anchor='middle',weight='700'))
 return ''.join(svg)
for entry in entries:
 plate,parts=entry['plate'],entry['parts'];pid=plate['id']
 allv=np.vstack([p['vertices'] for p in parts]);dimensions=(allv.max(0)-allv.min(0))*1000
 svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1100" viewBox="0 0 1600 1100">',
      '<rect width="1600" height="1100" fill="#fafbf9"/>',
      '<path d="M 56 115 H 1544" stroke="#b47c28" stroke-width="3"/>',
      text(56,51,'BRAKK ORUN / ARMOR STUDY 01',18,fill='#52636a',weight='700'),
      text(56,92,pid+'  '+plate['name'],33,weight='700'),
      text(1544,91,'BINDUNG: '+plate['bone'],18,anchor='end'),
      text(56,156,'ZUSAMMENGEBAUT',20,weight='700'),text(750,156,'SPRENGZEICHNUNG',20,weight='700')]
 svg.append(draw_view(parts,False,(100,225,535,540)))
 svg.append(draw_view(parts,True,(790,200,660,620)))
 for i,(label,value) in enumerate(zip(['BREITE','HOEHE','TIEFE'],dimensions)):
  x=65+i*192
  svg.append(text(x,845,label,15,fill='#52636a'));svg.append(text(x,879,f'{value:.1f} mm',24,weight='700'))
 svg.append(text(750,880,'Abstaende der Explosion dienen nur der Darstellung.',17,fill='#52636a'))
 svg.append('<path d="M 56 919 H 1544" stroke="#d1d7d5"/>')
 names=['Halterplatte','Daempfungseinlage','Goldschale','Deckplatte','3 Schrauben']
 for i,name in enumerate(names):
  x=70+i*300
  svg.append(text(x,959,f'{i+1:02d}  {name}',18,weight='700'))
  note=['Starrer Anschluss','Separate Einlage','Geschlossene Schale','Graphit, abgeschraegt','Sechskant, versenkt'][i]
  svg.append(text(x,989,note,15,fill='#52636a'))
 svg.extend([text(56,1055,'Geometrie: '+pid+'_01 bis '+pid+'_05 | Masse aus den exportierten Blender-Flaechen.',16,fill='#52636a'),
             text(1544,1055,'Meter / Unit Scale 1',16,fill='#52636a',anchor='end'),'</svg>'])
 content=''.join(svg);(OUT/(pid+'-Sprengzeichnung.svg')).write_text(content)
 cairosvg.svg2png(bytestring=content.encode(),write_to=str(OUT/(pid+'-Sprengzeichnung.png')))
print('PLATE_DRAWINGS_VALIDATED',len(entries), 'separate SVG/PNG pages')
