"""Zhaev Null: fresh mathematical geometry; Python standard library only.
Coordinates: metres, X right, Y forward, Z up. No input meshes or images.
"""
import json, math, hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2]; O=R/'ArtSource/Heroes/ZhaevOriginal';O.mkdir(parents=True,exist_ok=True)
P=[]
COLORS={'fabric':'#17202b','metal':'#404d60','edge':'#8090a5','energy':'#ad56ff','skin':'#927567','black':'#090e18'}
def part(name,cat,bone,v,f,mat):
 P.append(dict(id=f'Z{len(P)+1:03}',name=name,category=cat,bone=bone,vertices=v,faces=f,material=mat))
def orb(name,cat,bone,c,r,mat,n=16,k=10):
 v=[(c[0],c[1],c[2]+r[2])];f=[]
 for j in range(1,k):
  a=math.pi*j/k
  for i in range(n):
   t=2*math.pi*i/n;v.append((c[0]+r[0]*math.sin(a)*math.cos(t),c[1]+r[1]*math.sin(a)*math.sin(t),c[2]+r[2]*math.cos(a)))
 v.append((c[0],c[1],c[2]-r[2]));bottom=len(v)-1
 for i in range(n):f.append((0,1+i,1+(i+1)%n))
 for j in range(k-2):
  a=1+j*n;b=a+n
  for i in range(n):d=(i+1)%n;f.extend([(a+i,b+i,b+d),(a+i,b+d,a+d)])
 for i in range(n):f.append((bottom,1+(k-2)*n+(i+1)%n,1+(k-2)*n+i))
 part(name,cat,bone,v,f,mat)
def prism(name,cat,bone,outline,y,depth,mat):
 # Convex outline in X/Z, bevel silhouette is explicitly authored.
 n=len(outline);v=[(x,y+d,z) for d in [-depth/2,depth/2] for x,z in outline];f=[]
 for i in range(1,n-1):f.extend([(0,i+1,i),(n,n+i,n+i+1)])
 for i in range(n):j=(i+1)%n;f.extend([(i,j,n+j),(i,n+j,n+i)])
 part(name,cat,bone,v,f,mat)
def plate(name,cat,bone,x,z,w,h,y,d,mat):
 prism(name,cat,bone,[(x-w*.36,z-h/2),(x+w*.36,z-h/2),(x+w/2,z-h*.28),(x+w/2,z+h*.28),(x+w*.36,z+h/2),(x-w*.36,z+h/2),(x-w/2,z+h*.28),(x-w/2,z-h*.28)],y,d,mat)
# Own skeleton dimensions compatible in naming with the project's runtime contract.
B=[['Root',[0,0,0],[0,0,.15],None],['Hips',[0,0,.90],[0,0,1.03],'Root'],['Spine',[0,0,1.03],[0,0,1.20],'Hips'],['Chest',[0,0,1.20],[0,0,1.42],'Spine'],['Head',[0,0,1.42],[0,0,1.75],'Chest']]
orb('Pelvic undersuit','clothes','Hips',(0,0,.91),(.175,.105,.15),'fabric')
orb('Waist flex core','clothes','Spine',(0,0,1.08),(.135,.09,.16),'fabric')
orb('Rib undersuit','clothes','Chest',(0,0,1.28),(.215,.12,.20),'fabric')
orb('Neck seal','clothes','Head',(0,0,1.48),(.07,.068,.085),'black')
orb('Original head cranium','head','Head',(0,0,1.63),(.088,.081,.128),'skin',20,12)
plate('Face brow','head','Head',0,1.667,.148,.033,.074,.02,'skin')
plate('Face jaw','head','Head',0,1.575,.12,.072,.069,.032,'skin')
prism('Face nose','head','Head',[(-.018,1.608),(.018,1.608),(.014,1.664),(-.014,1.664)],.096,.028,'skin')
for s in [-1,1]:orb('Eye '+str(s),'head','Head',(s*.035,.082,1.65),(.018,.008,.008),'black',12,6)
# Fully sealed helmet shells cover the independently editable head.
orb('Crown outer shell','helmet','Head',(0,-.015,1.66),(.11,.091,.135),'metal',20,12)
plate('Helmet visor plate','helmet','Head',0,1.65,.19,.074,.090,.020,'black')
plate('Chin respirator','helmet','Head',0,1.563,.15,.075,.09,.036,'metal')
for s in [-1,1]:
 plate('Violet eye aperture '+str(s),'helmet','Head',s*.044,1.655,.067,.011,.105,.004,'energy')
 plate('Helmet cheek flank '+str(s),'helmet','Head',s*.08,1.596,.055,.096,.072,.035,'edge')
 plate('Temple service latch '+str(s),'helmet','Head',s*.10,1.674,.024,.034,.020,.03,'edge')
plate('Crown sensor rail','helmet','Head',0,1.75,.017,.072,.072,.006,'energy')
# Torso split plates: centre negative space, raised protective overlapping edges.
for s in [-1,1]:
 prism('Split cuirass '+str(s),'armor','Chest',[(s*.017,1.16),(s*.14,1.18),(s*.204,1.35),(s*.18,1.43),(s*.025,1.405)],.116,.027,'metal')
 plate('Cuirass edge rail '+str(s),'armor','Chest',s*.105,1.39,.12,.012,.135,.009,'edge')
 plate('Scapular plate '+str(s),'armor','Chest',s*.104,1.31,.17,.205,-.116,.022,'metal')
 for j in range(3):plate('Abdominal lamella '+str(s)+' '+str(j),'armor','Spine',s*.069,1.15-j*.051,.118,.037,.091,.02,'metal')
plate('Sternum violet key','armor','Chest',0,1.313,.018,.14,.14,.007,'energy')
for j in range(4):plate('Back spine cell '+str(j),'armor','Chest',0,1.21+j*.044,.033,.022,-.137,.009,'energy')
plate('Belt buckle','clothes','Hips',0,.974,.074,.05,.12,.025,'edge')
for s,k in [(-1,'L'),(1,'R')]:
 shoulder=[s*.24,0,1.39];elbow=[s*.34,0,1.10];wrist=[s*.39,0,.88];tip=[s*.4,0,.79]
 for n,h,t,p in [('UpperArm',shoulder,elbow,'Chest'),('Forearm',elbow,wrist,'UpperArm'+k),('Hand',wrist,tip,'Forearm'+k),('WeaponGrip',wrist,tip,'Hand'+k),('Thigh',[s*.13,0,.9],[s*.14,0,.49],'Hips'),('Shin',[s*.14,0,.49],[s*.18,0,.12],'Thigh'+k),('Foot',[s*.18,0,.12],[s*.18,.15,.06],'Shin'+k)]:B.append([n+k,h,t,p])
 orb('Upper sleeve '+k,'clothes','UpperArm'+k,(s*.288,0,1.255),(.067,.07,.17),'fabric')
 orb('Elbow flex '+k,'clothes','Forearm'+k,(s*.34,0,1.105),(.055,.058,.067),'black')
 orb('Lower sleeve '+k,'clothes','Forearm'+k,(s*.365,0,.99),(.056,.06,.13),'fabric')
 orb('Glove palm '+k,'clothes','Hand'+k,(s*.39,0,.839),(.048,.041,.055),'fabric')
 for j in range(4):orb('Glove finger '+k+str(j),'clothes','Hand'+k,(s*(.359+j*.019),.032,.80),(.009,.018,.031),'fabric',10,6)
 orb('Glove thumb '+k,'clothes','Hand'+k,(s*.35,.015,.847),(.013,.025,.027),'fabric',10,6)
 plate('Shoulder shell '+k,'armor','UpperArm'+k,s*.257,1.39,.145,.12,.006,.16,'metal')
 plate('Shoulder tracer '+k,'armor','UpperArm'+k,s*.257,1.425,.103,.009,.091,.005,'energy')
 plate('Biceps guard '+k,'armor','UpperArm'+k,s*.292,1.25,.092,.16,.058,.019,'metal')
 plate('Vambrace '+k,'armor','Forearm'+k,s*.367,.995,.093,.17,.057,.025,'metal')
 for j in range(3):plate('Vambrace vent '+k+str(j),'armor','Forearm'+k,s*.367,1.02-j*.028,.062,.007,.074,.005,'edge')
 plate('Glove dorsal plate '+k,'armor','Hand'+k,s*.39,.852,.073,.047,.044,.012,'metal')
 orb('Trouser thigh '+k,'clothes','Thigh'+k,(s*.137,0,.716),(.087,.086,.215),'fabric')
 orb('Knee joint '+k,'clothes','Shin'+k,(s*.14,0,.49),(.066,.069,.068),'black')
 orb('Trouser calf '+k,'clothes','Shin'+k,(s*.16,0,.317),(.068,.067,.173),'fabric')
 plate('Thigh floating plate '+k,'armor','Thigh'+k,s*.139,.73,.126,.22,.071,.021,'metal')
 plate('Knee cap '+k,'armor','Shin'+k,s*.14,.49,.109,.088,.068,.027,'edge')
 plate('Shin guard '+k,'armor','Shin'+k,s*.16,.31,.093,.23,.057,.022,'metal')
 plate('Shin tracer '+k,'armor','Shin'+k,s*.16,.32,.011,.15,.071,.004,'energy')
 orb('Boot shell '+k,'clothes','Foot'+k,(s*.18,.067,.092),(.078,.14,.074),'metal')
 plate('Boot sole '+k,'clothes','Foot'+k,s*.18,.026,.159,.04,.066,.272,'black')
 plate('Hip skirt panel '+k,'clothes','Hips',s*.19,.872,.067,.175,.004,.10,'fabric')
 # Blade extends out/down from hand. Own asymmetric spear/crescent hybrid profile.
 x=s*.39
 plate('Null blade grip '+k,'weapon','WeaponGrip'+k,x,.84,.036,.14,.020,.032,'black')
 plate('Null blade pommel '+k,'weapon','WeaponGrip'+k,x,.93,.055,.026,.020,.038,'edge')
 plate('Null blade guard '+k,'weapon','WeaponGrip'+k,x,.769,.115,.023,.020,.040,'metal')
 outline=[(x-s*.022,.762),(x+s*.026,.762),(x+s*.13,.60),(x+s*.15,.43),(x+s*.04,.51),(x-s*.036,.67)]
 prism('Null blade forged core '+k,'weapon','WeaponGrip'+k,outline,.02,.019,'metal')
 prism('Null blade cutting bevel '+k,'weapon','WeaponGrip'+k,[(x+s*.04,.51),(x+s*.15,.43),(x+s*.13,.60),(x+s*.113,.597)],.02,.024,'edge')
 prism('Null blade energy vein '+k,'weapon','WeaponGrip'+k,[(x+s*.002,.738),(x+s*.018,.736),(x+s*.102,.589),(x+s*.096,.559)],.033,.004,'energy')
# Orient every closed source part outward, including mirrored blade profiles.
for p in P:
 volume=0
 for f in p['faces']:
  a,b,c=[p['vertices'][i] for i in f]
  volume+=a[0]*(b[1]*c[2]-b[2]*c[1])+a[1]*(b[2]*c[0]-b[0]*c[2])+a[2]*(b[0]*c[1]-b[1]*c[0])
 assert abs(volume)>1e-10,p['name']
 if volume<0:p['faces']=[tuple(reversed(f)) for f in p['faces']]
# Serialize all authored parts; enough to reconstruct every output without an external asset.
spec={'revision':'Zhaev-original-v1','hero':'Veyr','units':'metres','axes':'X right Y forward Z up','colors':COLORS,'bones':B,'parts':P}
(O/'parts.json').write_text(json.dumps(spec,separators=(',',':')))
mtl='';obj=['mtllib Zhaev.mtl'];off=1
for m,c in COLORS.items():rgb=[int(c[i:i+2],16)/255 for i in (1,3,5)];mtl+=f'newmtl {m}\nKd '+ ' '.join(map(str,rgb))+'\n'
for p in P:
 obj += ['o '+p['id']+'_'+p['name'].replace(' ','_'),'usemtl '+p['material']]
 obj += ['v '+' '.join(f'{q:.6f}' for q in v) for v in p['vertices']]
 obj += ['f '+' '.join(str(i+off) for i in f) for f in p['faces']];off+=len(p['vertices'])
(O/'Zhaev.obj').write_text('\n'.join(obj)+'\n');(O/'Zhaev.mtl').write_text(mtl)
# Deterministic projections from exact geometry: front/back/side and exploded part sheets.
def polys(p,proj,dx=0,dz=0,scale=260,depth_axis=1,depth_sign=1):
 v=p['vertices'];items=[]
 for f in p['faces']:
  points=[proj(v[i]) for i in f];depth=depth_sign*sum(v[i][depth_axis] for i in f)/3
  pts=' '.join(f'{x*scale+dx:.2f},{-z*scale+dz:.2f}' for x,z in points)
  items.append((depth,f'<polygon points="{pts}" fill="{COLORS[p["material"]]}" stroke="#080e17" stroke-width=".45"/>'))
 return items
header='<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="1100" viewBox="0 0 1400 1100"><rect width="1400" height="1100" fill="#edf1f6"/><g font-family="sans-serif" fill="#1c283a">'
def title(text,sub):return f'<text x="45" y="50" font-size="30" font-weight="bold">{text}</text><text x="45" y="82" font-size="15">{sub}</text>'
svg=header+title('ZHAEV NULL / ORIGINAL V1','Geometry-derived orthographic plan | metres | 2026-10-12')
for label,dx,proj in [('FRONT',300,lambda v:(v[0],v[2])),('BACK',700,lambda v:(-v[0],v[2])),('SIDE',1120,lambda v:(v[1],v[2]))]:
 items=[q for p in P if p['category']!='head' for q in polys(p,proj,dx,920,420,0 if label=='SIDE' else 1,-1 if label=='BACK' else 1)]
 svg+=''.join(q[1] for q in sorted(items,key=lambda q:q[0]))+f'<text x="{dx-40}" y="990" font-size="20">{label}</text>'
svg+='</g></svg>';(O/'Orthographic.svg').write_text(svg)
for cat in ['armor','weapon','head','helmet','clothes']:
 parts=[p for p in P if p['category']==cat];rows=math.ceil(len(parts)/5);height=150+rows*205
 svg=header.replace('height="1100"','height="'+str(height)+'"').replace('1400 1100','1400 '+str(height))+title('ZHAEV / '+cat.upper()+' / EXPLODED PARTS','Each component shown separately from exact mesh data. IDs match parts.json; not to common scale.')
 for j,p in enumerate(parts):
  cx=150+(j%5)*275;cy=180+(j//5)*205
  xmin=min(v[0] for v in p['vertices']);xmax=max(v[0] for v in p['vertices']);zmin=min(v[2] for v in p['vertices']);zmax=max(v[2] for v in p['vertices']);scale=min(190/max(xmax-xmin,.01),135/max(zmax-zmin,.01))
  items=polys(p,lambda v:(v[0]-(xmin+xmax)/2,v[2]-(zmin+zmax)/2),cx,cy+12,scale)
  svg+=''.join(q[1] for q in sorted(items,key=lambda q:q[0]))
  svg+=f'<text x="{cx-105}" y="{cy+100}" font-size="13">{p["id"]} | {p["name"]}</text><text x="{cx-105}" y="{cy+118}" font-size="11">{(xmax-xmin)*1000:.0f} x {(zmax-zmin)*1000:.0f} mm | {p["bone"]}</text>'
 svg+='</g></svg>';(O/('Exploded-'+cat+'.svg')).write_text(svg)
# Mechanical assembly explosion is a scaled displacement of actual mesh components.
svg=header+title('ZHAEV / ASSEMBLY EXPLOSION','Helmet up / armor front-back / limbs outward | original geometry | component categories separated')
items=[]
for p in P:
 if p['category']=='head':continue
 def projection(v,p=p):
  x,y,z=v;side=-1 if x<0 else 1
  if p['category']=='helmet':z+=.32
  if p['category']=='armor':x+=side*.20;y+=.15 if y>0 else -.15
  if p['category']=='weapon':x+=side*.42
  return (x+.45*y,z+.28*y)
 items+=polys(p,projection,700,990,390)
svg+=''.join(q[1] for q in sorted(items,key=lambda q:q[0]))+'</g></svg>';(O/'Assembly-exploded.svg').write_text(svg)
# Structural checks genuinely cover manifold parts, finite coordinates and unique labels.
for p in P:
 assert all(math.isfinite(x) for v in p['vertices'] for x in v)
 edges={}
 for f in p['faces']:
  assert len(set(f))==3
  for a,b in zip(f,f[1:]+f[:1]):key=tuple(sorted((a,b)));edges[key]=edges.get(key,0)+1
 assert all(n==2 for n in edges.values()),p['name']
assert len({p['id'] for p in P})==len(P)
report={'parts':len(P),'vertices':sum(len(p['vertices']) for p in P),'triangles':sum(len(p['faces']) for p in P),'bones':len(B),'checks':['finite vertices','closed two-face edge incidence per part','unique part IDs'],'nativeBlender':'pending','UnityAndroid':'not executed','freshGeometry':True,'externalMeshInputs':[]}
(O/'geometry-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
