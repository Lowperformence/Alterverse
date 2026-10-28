"""Exact vector assembly sheet generated from P01 geometry."""
from build_brakk import parts,materials
from pathlib import Path
from html import escape
ROOT=Path(__file__).resolve().parent
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1500" height="1100" viewBox="0 0 1500 1100">',
'<rect width="1500" height="1100" fill="#f4f0e7"/>',
'<style>text{font-family:Arial,sans-serif;fill:#202d35} .h{font-size:30px;font-weight:bold}.t{font-size:18px}.s{font-size:15px}</style>',
'<text x="40" y="50" class="h">BRAKK P01 | Modularer Bauplan</text>',
'<text x="40" y="80" class="t">Blockout in Metern · Front -Y · Z oben · Teile-IDs nach README, unabhängig von Bildlabels</text>']
def draw(cx,base,scale,exploded=False,side=False):
    shifts={'head':(0,.40),'torso':(0,.18),'arms':(.30,.18),'gauntlets':(.55,0),'pelvis':(0,0),'legs':(.12,-.08)}
    for p in sorted(parts,key=lambda p:sum(v[1] for v in p['vertices'])/len(p['vertices']),reverse=True):
        dx,dz=shifts[p['group']] if exploded else (0,0)
        if sum(v[0] for v in p['vertices'])<0: dx=-dx
        color='#'+''.join(f'{round(c*255):02x}' for c in materials[p['material']])
        for face in p['faces']:
            pts=[]
            for i in face:
                x,y,z=p['vertices'][i]; horizontal=-y if side else x+dx
                pts.append(f'{cx+scale*horizontal:.1f},{base-scale*(z+dz):.1f}')
            svg.append(f'<polygon points="{" ".join(pts)}" fill="{color}" stroke="#42515a" stroke-width=".7"/>')
draw(250,735,310)
draw(670,735,280,True)
draw(1120,735,310,side=True)
for x,label in [(80,'Zusammengebaut / Front'),(480,'Baugruppen getrennt'),(1010,'Seitenprofil')]:
    svg.append(f'<text x="{x}" y="780" class="t">{label}</text>')
labels=[('01 / 02 Kopf',125),('03–06 Torso',335),('08 / 09 Arme',435),('10 / 11 Fäuste',510),('07 / 12 Hüfte',560),('13–16 Beine',700)]
for label,y in labels: svg.append(f'<text x="1280" y="{y}" class="s">{label}</text>')
svg.append('<path d="M40 820H1460" stroke="#8a969b"/><text x="40" y="860" class="h">Faustmodul | Montagefolge von innen nach außen</text>')
blocks=[('Innenhandschuh','10'),('Handgelenkring','10'),('Träger / Palmplatte','09 / 10'),('Rückenschale','10'),('2 Dämpfer','11'),('3 Schlagsegmente','11')]
for i,(name,ids) in enumerate(blocks):
    x=40+i*242
    svg.append(f'<rect x="{x}" y="890" width="213" height="100" rx="12" fill="#d8dde0" stroke="#42515a"/>')
    svg.append(f'<text x="{x+12}" y="923" class="t">{escape(name)}</text><text x="{x+12}" y="956" class="s">Teile-ID {ids}</text>')
    if i<5: svg.append(f'<path d="M{x+216} 940h20l-6 -5m6 5l-6 5" fill="none" stroke="#42515a" stroke-width="2"/>')
svg.append('<text x="40" y="1040" class="t">59 separate Meshes · Detailmodellierung, UVs, Rig und Animation stehen noch aus.</text>')
svg.append('<text x="40" y="1070" class="s">Konzept-Sprengzeichnung; keine technische Fertigungs- oder Belastungsfreigabe.</text></svg>')
(ROOT/'brakk_bauplan.svg').write_text('\n'.join(svg))
