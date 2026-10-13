"""Create the illustrated catalog and exact vector plate pages from the Blender geometry.
python build_catalogue.py --output /absolute/path/Brakk_Ruestung_Sprengzeichnungen.pdf
"""
import argparse,io,json,gzip
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4,landscape
from reportlab.lib.colors import HexColor,Color,white
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.utils import ImageReader
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from pypdf import PdfReader,PdfWriter,Transformation
import cairosvg
import numpy as np

ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/BrakkArmor'
p=argparse.ArgumentParser();p.add_argument('--output',required=True);args=p.parse_args()
output=Path(args.output).resolve();output.parent.mkdir(parents=True,exist_ok=True)
W,H=landscape(A4);INK=HexColor('#14242c');GREY=HexColor('#52636a');GOLD=HexColor('#b47c28')
fontRoot=Path('/usr/share/fonts/truetype/dejavu')
pdfmetrics.registerFont(TTFont('BrakkSans',str(fontRoot/'DejaVuSans.ttf')))
pdfmetrics.registerFont(TTFont('BrakkBold',str(fontRoot/'DejaVuSans-Bold.ttf')))
style=ParagraphStyle('Brakk',fontName='BrakkSans',fontSize=10,leading=14,textColor=INK)
meta=json.loads((ART/'catalogue.json').read_text());plates=meta['plates']
data=json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/BrakkUtilityBelt/Model.bytes').read_bytes()))
diagrams={x['plate']['id']:x for x in json.loads((ART/'diagram-geometry.json').read_text())}

buf=io.BytesIO();c=canvas.Canvas(buf,pagesize=(W,H))
c.setTitle('Brakk Orun - Rüstungskonzept und 18 Sprengzeichnungen')
c.setAuthor('Alterverse - Brakk-Designauftrag')
def text(x,y,t,size=10,color=INK,bold=False):
 c.setFillColor(color);c.setFont('BrakkBold' if bold else 'BrakkSans',size);c.drawString(x,y,t)
def para(x,top,width,t):
 obj=Paragraph(t,style);_,height=obj.wrap(width,H);obj.drawOn(c,x,top-height);return top-height

def title(t,sub):
 c.setFillColor(HexColor('#fafbf9'));c.rect(0,0,W,H,fill=1,stroke=0)
 text(36,H-45,t,24,bold=True);text(36,H-66,sub,10,GREY)
 c.setStrokeColor(GOLD);c.setLineWidth(1.5);c.line(36,H-80,W-36,H-80)
def image(path,x,y,w,h):
 c.drawImage(str(path),x,y,width=w,height=h,preserveAspectRatio=True,anchor='c',mask='auto')
def footer(n,body='BRAKK ORUN / MODULAR ARMOR / KONZEPT 01'):
 text(36,17,body,8,GREY);text(W-65,17,f'{n:02d} / 22',8,GREY)

# 1: artistic concept; geometry in the following pages is authoritative.
c.setFillColor(HexColor('#161c22'));c.rect(0,0,W,H,fill=1,stroke=0)
image(ART/'BrakkArmor-artistic-concept.png',18,37,W-36,H-56)
text(24,19,'Künstlerischer Entwurf | Stand 13.10.2026 | Technische Geometrie auf den folgenden Seiten',8.3,HexColor('#c4c9cd'))
text(W-62,19,'01 / 22',8.3,HexColor('#c4c9cd'));c.showPage()

# 2: source-native prototype, fitted against the inherited reference anatomy.
title('Das umsetzbare Blender-Modell','18 Plattenmodule, 126 Rüstungsteile; bestehendes Rig und bestätigte Faustgeometrie als Referenz.')
image(ART/'BrakkArmor-front.png',36,87,(W-90)/2,405)
image(ART/'BrakkArmor-back.png',W/2+9,87,(W-90)/2,405)
text(36,70,'VORDERSEITE',10,bold=True);text(W/2+9,70,'RÜCKSEITE',10,bold=True)
para(36,52,W-72,'Die Abbildungen zeigen die tatsächliche Blender-Geometrie am Referenzkörper. Der bestehende Spielkopf bleibt erhalten. Die künstlerische Titelseite veranschaulicht Materialwirkung und Erscheinungsbild; Maße und Bauteile richten sich nach dem Blender-Modell.')
footer(2);c.showPage()

# 3: production belt; all 34 parts are represented in the editable source model.
title('Utensiliengürtel','Separates Zubehör mit 34 geschlossenen Bauteilen und 5 Materialgruppen.')
image(ART/'BrakkUtilityBelt.png',28,150,470,340)
x=518;y=H-118
items=[('U01-04','Band, Schließe und Statusfeld'),('U05-09','Zwei Vordertaschen mit Deckeln und Schlaufen'),('U10-12','Hintere Reparaturbox mit Verriegelung'),('U13-14','Lampe mit zwei Halteclips'),('U15-16','Maulschlüssel mit Werkzeugclips'),('U17-19','Zwei austauschbare Kapseln mit Haltern')]
for code,name in items:
 text(x,y,code,10,GOLD,True);y=para(x,y-8,W-x-36,name)-25
vertices=np.array(data['vertices']).reshape(-1,3);dims=(vertices.max(0)-vertices.min(0))*1000
text(36,131,'GESAMTABMESSUNGEN DER AUSGESTATTETEN QUELLGEOMETRIE',9,GREY,True)
text(36,110,' × '.join(f'{v:.1f} mm' for v in dims)+'   (Breite × Höhe × Tiefe)',12,bold=True)
para(36,82,W-72,'Der Gürtel ist als Spielzubehör integriert und folgt dem vorhandenen Hüftknochen. Taschen und Utensilien sind kosmetische Ausrüstung; sie fügen keine neuen Fähigkeiten oder Kollisionskörper hinzu. Die neuen Panzerplatten bleiben in diesem Stand ein separates Konzept.')
footer(3);c.showPage()

# 4: measured catalog and practical construction workflow.
title('Plattenkatalog und Aufbau','Jedes Modul besitzt eine eigene Sprengzeichnung und feste Bauteilkennungen.')
xs=[36,95,277,390,485,579,674];heads=['ID','Bereich','Rig-Knochen','Breite','Höhe','Tiefe','Bauteile']
y=H-108
for x,h in zip(xs,heads):text(x,y,h,9,GREY,True)
y-=23
for plate in plates:
 pid=plate['id'];allv=np.vstack([p['vertices'] for p in diagrams[pid]['parts']]);size=(allv.max(0)-allv.min(0))*1000
 row=[pid,plate['name'].replace('Ruecken','Rücken').replace('Brustbruecke','Brustbrücke'),plate['bone'],*[f'{v:.1f} mm' for v in size],str(len(plate['parts']))]
 for x,t in zip(xs,row):text(x,y,t,9,INK,pid.startswith('C'))
 c.setStrokeColor(HexColor('#e0e4e1'));c.setLineWidth(.35);c.line(36,y-6,W-36,y-6);y-=18
para(36,94,W-72,'<b>Blender:</b> Meter und Unit Scale 1. Jede Platte besteht aus Halterplatte, Dämpfungseinlage, Goldschale, Graphit-Deckplatte und drei Sechskantschrauben. Geschlossene Polygonprismen, kleine Bevels und facettierte Zylinder bilden alle Details; es gibt keine Sculpting- oder Boolean-Abhängigkeit. Die Explosion zeigt getrennte Bauteile mit vergrößerten Ansichtsabständen.')
footer(4);c.showPage();c.save();buf.seek(0)

writer=PdfWriter();reader=PdfReader(buf)
for page in reader.pages:writer.add_page(page)
for i,plate in enumerate(plates,5):
 svg=(ART/'plates'/(plate['id']+'-Sprengzeichnung.svg')).read_bytes()
 drawing=PdfReader(io.BytesIO(cairosvg.svg2pdf(bytestring=svg))).pages[0]
 target=writer.add_blank_page(width=W,height=H)
 pw,ph=float(drawing.mediabox.width),float(drawing.mediabox.height)
 scale=min((W-36)/pw,(H-34)/ph)
 target.merge_transformed_page(drawing,Transformation().scale(scale).translate((W-pw*scale)/2,(H-ph*scale)/2))
 overlay=io.BytesIO();oc=canvas.Canvas(overlay,pagesize=(W,H));oc.setFillColor(GREY);oc.setFont('BrakkSans',8)
 oc.drawString(36,12,'BRAKK ORUN / TECHNISCHE GEOMETRIE / '+plate['id']);oc.drawRightString(W-36,12,f'{i:02d} / 22');oc.save();overlay.seek(0)
 target.merge_page(PdfReader(overlay).pages[0])
assert len(writer.pages)==22,len(writer.pages)
writer.add_metadata({'/Title':'Brakk Orun - Rüstungskonzept und 18 Sprengzeichnungen','/Author':'Alterverse - Brakk-Designauftrag','/Subject':'Blender-Bauteile, maßhaltige Plattenzeichnungen und Utensiliengürtel'})
with output.open('wb') as stream:writer.write(stream)
print('BRAKK_CATALOGUE_VALIDATED',output,'pages',len(writer.pages))
