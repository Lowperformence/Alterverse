from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import json
HERE=Path(__file__).resolve().parent
W,H=2600,1860
im=Image.new('RGB',(W,H),'#0c111d');d=ImageDraw.Draw(im)
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf';bold='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
def text(x,y,s,size=24,c='#d5ddea',b=False):d.text((x,y),s,font=ImageFont.truetype(bold if b else font,size),fill=c)
def line(points,c='#6a4ca5',w=2):d.line(points,fill=c,width=w)
def panel(box):d.rounded_rectangle(box,18,fill='#141b2b',outline='#2c3550',width=2)
text(70,48,'DIMENSIONS FRONT / WEAPON ATELIER',20,'#a28ccf',True)
text(67,87,'VOID KNIGHT',57,'#f3f4fc',True);text(71,161,'RIFTKEEPER  /  SCHWERT V5',24,'#bca3ff',True)
text(1790,58,'KONSTRUKTION & MATERIALSTUDIE',22,'#e3def4',True)
text(1790,99,'Blender 5.0.1  |  reale Modellansichten',21)
text(1790,139,'1.633 mm Gesamtlänge  /  380 mm Griff',21)
line([(70,218),(2530,218)],'#485176')
panel((60,250,960,1630));panel((990,250,1390,1630));panel((1420,250,2540,1110));panel((1420,1140,2540,1630))
text(90,277,'01  VORDERANSICHT',23,'#c4b1ef',True);text(1020,277,'02  PROFIL',23,'#c4b1ef',True)
text(1450,277,'03  MATERIAL & LICHT',23,'#c4b1ef',True);text(1450,1166,'04  GRIFF / PARIERSTANGE',23,'#c4b1ef',True)
def paste(name,box):
 src=Image.open(HERE/(name+'.png')).convert('RGBA');src=src.crop(src.getbbox());src.thumbnail((box[2]-box[0],box[3]-box[1]),Image.Resampling.LANCZOS)
 x=box[0]+(box[2]-box[0]-src.width)//2;y=box[1]+(box[3]-box[1]-src.height)//2
 im.paste(src,(x,y),src);return (x,y,src.width,src.height)
x,y,w,h=paste('front',(215,350,795,1550));paste('side',(1090,350,1290,1550));paste('beauty',(1470,335,2490,1060));paste('hilt-detail',(1640,1220,2300,1580))
# True overall length dimension and guard width from authored construction units.
line([(145,y),(145,y+h)],'#8b99b8');line([(135,y),(155,y)],'#8b99b8');line([(135,y+h),(155,y+h)],'#8b99b8')
text(85,y+h//2-45,'1.633',18,'#b4c0da',True);text(91,y+h//2-18,'mm',18)
gy=y+int(h*.74);line([(x,gy+70),(x+w,gy+70)],'#8b99b8');text(x+max(0,w//2-45),gy+80,'494 mm',17)
# Numbered leaders correspond to material and construction notes below.
for label,pos,elbow,end in [('A',(x+w*.52,y+h*.18),(825,y+h*.18),(865,y+h*.18)),('B',(x+w*.50,y+h*.43),(825,y+h*.43),(865,y+h*.43)),('C',(x+w*.74,y+h*.70),(825,y+h*.70),(865,y+h*.70)),('D',(x+w*.50,y+h*.88),(825,y+h*.88),(865,y+h*.88))]:
 line([pos,elbow,end],'#b39bda',2);d.ellipse((pos[0]-4,pos[1]-4,pos[0]+4,pos[1]+4),fill='#cdb1ff');text(end[0]+8,end[1]-15,label,23,'#d5b7ff',True)
text(85,1670,'A  FACETTIERTE KLINGE',21,'#c4b1ef',True);text(85,1710,'Dunkle Flächen, helle Schneidfasen.',21);text(85,1742,'Echte Geometrie statt aufgemalter Kante.',21)
text(730,1670,'B  EINGELASSENER RIFTKANAL',21,'#c4b1ef',True);text(730,1710,'Beidseitiger violetter Energieeinsatz.',21);text(730,1742,'Unter den geschmiedeten Führungsrippen.',21)
text(1420,1670,'C  FACETTIERTER KERN',21,'#c4b1ef',True);text(1420,1710,'Eigene Fassung, dunkler Sitz und Kristall.',21);text(1420,1742,'Symmetrische, nach oben gezogene Parierstange.',21)
text(2090,1670,'D  ZWEIHANDGRIFF',21,'#c4b1ef',True);text(2090,1710,'Diagonale Wicklung.',21);text(2090,1742,'Metallzwingen, Gegengewicht.',21)
line([(70,1805),(2530,1805)],'#303952');text(70,1820,'53 editierbare Bauteile  |  2.208 Schwert-Dreiecke  |  5 gemeinsame Materialien',18,'#8c9ab7');text(1820,1820,'V5 / 28.10.2026 / Modellmaße in mm',18,'#8c9ab7')
im.save(HERE/'VoidKnight-Schwert-Zeichnung.png')
