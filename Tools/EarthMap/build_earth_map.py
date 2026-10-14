"""Deterministic Earth scenery kit. Run with Python; import/edit with author_in_blender.py.

Coordinates are Unity metres (X/Y/Z, Y up). Only presentation assets are emitted.
Every asset has a ground pivot and material-separated mesh parts for regional batching.
"""
import json
import math
import random
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/Alterverse/Resources/EarthMap/Dressing.json'


def add(a, b): return tuple(x + y for x, y in zip(a, b))
def mul(a, s): return tuple(x * s for x in a)
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(a): return mul(a, 1 / max(math.sqrt(sum(x*x for x in a)), 1e-8))


class Part:
    def __init__(self, material):
        self.material, self.vertices, self.triangles, self.uv = material, [], [], []

    def face(self, points, uv=None):
        start = len(self.vertices)//3
        for i, p in enumerate(points):
            self.vertices.extend(round(x, 5) for x in p)
            self.uv.extend(uv[i] if uv else (0, 0))
        for i in range(1, len(points)-1):
            self.triangles.extend((start, start+i, start+i+1))

    def box(self, center, size):
        x, y, z = center; w, h, d = (s/2 for s in size)
        v = [(x-w,y-h,z-d),(x+w,y-h,z-d),(x+w,y+h,z-d),(x-w,y+h,z-d),
             (x-w,y-h,z+d),(x+w,y-h,z+d),(x+w,y+h,z+d),(x-w,y+h,z+d)]
        for ids in ((0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)):
            self.face([v[i] for i in ids])

    def tube(self, points, radii, sides=7):
        rings = []
        for i, p in enumerate(points):
            delta = tuple(b-a for a,b in zip(points[max(0,i-1)], points[min(len(points)-1,i+1)]))
            direction = norm(delta)
            side = norm(cross(direction, (0,0,1) if abs(direction[2]) < .9 else (1,0,0)))
            up = cross(side, direction)
            rings.append([add(p, add(mul(side, math.cos(j*math.tau/sides)*radii[i]),
                                      mul(up, math.sin(j*math.tau/sides)*radii[i]))) for j in range(sides)])
        for a, b in zip(rings, rings[1:]):
            for j in range(sides):
                k = (j+1)%sides
                self.face([a[j], b[j], b[k], a[k]])
        self.face(rings[0])
        self.face(list(reversed(rings[-1])))

    def leaf(self, center, length, width, angle, droop=.14):
        # Closed, bent leaf volume: no alpha sorting or invisible back faces.
        a = math.radians(angle); forward=(math.sin(a),0,math.cos(a)); side=(math.cos(a),0,-math.sin(a))
        tip=add(center,mul(forward,length)); tip=add(tip,(0,-droop,0))
        mid=add(center,mul(forward,length*.44))
        left=add(mid,mul(side,width)); right=add(mid,mul(side,-width))
        ridge=add(mid,(0,.07,0)); underside=add(mid,(0,-.025,0))
        start=len(self.vertices)//3
        for p in (center,left,tip,right,ridge,underside):
            self.vertices.extend(round(x,5) for x in p);self.uv.extend((0,0))
        for ids in ((0,4,1),(1,4,2),(2,4,3),(3,4,0),(1,5,0),(2,5,1),(3,5,2),(0,5,3)):
            self.triangles.extend(start+i for i in ids)

    def data(self):
        return dict(material=self.material, vertices=self.vertices, triangles=self.triangles, uv=self.uv)

    def cluster(self, center, size, seed):
        # Small irregular leaf masses with shared normals, rather than stock spherical crowns.
        rng=random.Random(seed);start=len(self.vertices)//3;count=6
        pts=[]
        for y,radius in ((-.65,.48),(0,1),(.68,.46)):
            for j in range(count):
                a=j*math.tau/count;wobble=rng.uniform(.82,1.18)
                pts.append(add(center,(math.cos(a)*radius*size[0]*wobble,y*size[1],math.sin(a)*radius*size[2]*wobble)))
        pts.extend([add(center,(0,-size[1],0)),add(center,(0,size[1],0))])
        for p in pts:self.vertices.extend(round(x,5) for x in p);self.uv.extend((0,0))
        for row in range(2):
            for j in range(count):
                k=(j+1)%count;a=row*count+j;b=row*count+k;c=a+count;d=b+count
                self.triangles.extend(start+i for i in (a,c,b,b,c,d))
        for j in range(count):
            k=(j+1)%count
            self.triangles.extend(start+i for i in (18,j,k,19,12+k,12+j))


class Asset:
    def __init__(self, name): self.name, self.parts = name, {}
    def part(self, material): return self.parts.setdefault(material, Part(material))
    def data(self): return dict(name=self.name, parts=[p.data() for p in self.parts.values() if p.vertices])


def tree(species, seed):
    rng=random.Random(seed); a=Asset(species+str(seed%2)); bark=a.part(1 if species=='Birch' else 0)
    h=3.9 if species=='Pine' else 3.3 if species=='Birch' else 3.15
    bark.tube([(0,0,0),(.04,.9,0),(-.07,h*.64,.05),(.07,h,0)], [.15,.115,.073,.018])
    for i in range(4):
        angle=i*math.tau/4+.3; end=(math.sin(angle)*.44,.025,math.cos(angle)*.44)
        bark.tube([(0,.22,0),end],[.10,.027],5)
    branch_count=14 if species=='Pine' else 9
    for i in range(branch_count):
        angle=i*137.5+seed*23; rad=math.radians(angle)
        t=i/max(branch_count-1,1)
        y=.9+t*(h-.95) if species=='Pine' else 1.3+t*1.65
        reach=(1.15*(1-t)+.16) if species=='Pine' else rng.uniform(.55,1.05)*(1-.3*t)
        base=(0,y,0); tip=(math.sin(rad)*reach,y+(.08 if species=='Pine' else .36),math.cos(rad)*reach)
        bark.tube([base,add(mul(base,.35),mul(tip,.65)),tip],[.046,.028,.009],5)
        foliage=a.part(4 if species=='Pine' else 2+(i%3==0))
        cluster_pos=add(mul(base,.18),mul(tip,.82))
        cluster_size=(.38*(1-t*.55),.20,.40*(1-t*.55)) if species=='Pine' else (.34,.53,.36) if species=='Birch' else (.57,.43,.50)
        foliage.cluster(cluster_pos,cluster_size,seed*100+i)
        for j in range(3 if species=='Pine' else 4):
            f=.28+j*.095 if species=='Pine' else rng.uniform(.45,1.15)
            c=add(mul(base,1-f),mul(tip,f))
            c=add(c,(rng.uniform(-.25,.25),rng.uniform(-.07,.23),rng.uniform(-.25,.25)))
            foliage.leaf(c,.45 if species=='Pine' else .42,.11 if species=='Pine' else .21,
                         angle+(j%2*2-1)*rng.uniform(35,110),.10)
    return a


def shrub(name, seed, fern=False):
    a=Asset(name); rng=random.Random(seed)
    for i in range(5):
        angle=i*137.5; rad=math.radians(angle)
        end=(math.sin(rad)*.45,.34+rng.uniform(0,.16),math.cos(rad)*.45)
        a.part(0).tube([(0,0,0),end],[.016,.005],4)
        for j in range(5 if fern else 4):
            f=.3+j*.12; c=mul(end,f)
            a.part(3 if fern else 2).leaf(c,.20 if fern else .28,.055 if fern else .12,angle+(1 if j%2 else -1)*65,.08)
    return a


def reeds():
    a=Asset('Reeds')
    for i in range(8):
        angle=i*137.5; rad=math.radians(angle); x=math.sin(rad)*.22; z=math.cos(rad)*.22; h=.55+(i%3)*.12
        a.part(3).tube([(x,0,z),(x+.03,h,z)],[.012,.007],4)
        a.part(0).tube([(x+.03,h-.08,z),(x+.03,h+.09,z)],[.035,.025],5)
        a.part(3).leaf((x,h*.45,z),.35,.04,angle,.15)
    return a


def cottage(name, shed=False, slate=False):
    a=Asset(name); w=2.4 if shed else 3.0 if slate else 3.4; d=2 if shed else 2.9 if slate else 2.65; h=1.35 if shed else 1.85
    wall=a.part(0 if shed else 5); wall.box((0,h/2+.12,0),(w,h,d))
    a.part(9).box((0,.10,0),(w+.14,.20,d+.14))
    ridge=h+(.70 if slate else .92); eave=h+.16
    # Gable ends and two pitched roof slabs with actual thickness/overhang.
    wall.face([(-w/2,h+.12,-d/2),(0,ridge,-d/2),(w/2,h+.12,-d/2)])
    wall.face([(w/2,h+.12,d/2),(0,ridge,d/2),(-w/2,h+.12,d/2)])
    roof=a.part(9 if slate else 6)
    for side in (-1,1):
        x=side*(w/2+.24); z=d/2+.22
        top=[(0,ridge+.08,-z),(x,eave,-z),(x,eave,z),(0,ridge+.08,z)]
        if side>0: top.reverse()
        roof.face(top); roof.face(list(reversed([(px,py-.10,pz) for px,py,pz in top])))
        for p,q in zip(top,top[1:]+top[:1]): roof.face([p,q,add(q,(0,-.10,0)),add(p,(0,-.10,0))])
        # Tile courses read from above without hundreds of separate tile objects.
        for i in range(1,5):
            f=i/5; tx=x*f; ty=(ridge+.08)*(1-f)+eave*f+.02
            roof.box((tx,ty,0),(.045,.035,d+.45))
        a.part(7).box((x,eave-.075,0),(.07,.07,d+.40))
    a.part(7).box((0,ridge+.1,0),(.14,.12,d+.50))
    # Front is -Z. Door has a frame, lintel, step and small handle.
    front=-d/2-.018
    a.part(0).box((0,.68,front),(.54,1.12,.04))
    for x in (-.32,.32): a.part(7).box((x,.73,front-.04),(.07,1.25,.07))
    a.part(7).box((0,1.35,front-.04),(.70,.08,.07))
    a.part(9).box((0,.12,front-.20),(.85,.17,.44))
    a.part(7).box((.17,.70,front-.045),(.025,.05,.022))
    if not shed:
        for x in (-1.02,1.02):
            a.part(8).box((x,1.12,front-.01),(.58,.58,.04))
            for dx in (-.33,.33): a.part(7).box((x+dx,1.12,front-.04),(.055,.69,.06))
            for dy in (-.33,.33): a.part(7).box((x,1.12+dy,front-.04),(.69,.055,.06))
            a.part(7).box((x,1.12,front-.05),(.045,.60,.06))
            a.part(9).box((x,.78,front-.10),(.78,.06,.22))
        # Side windows and downpipes are visible from either gameplay camera.
        for side in (-1,1):
            a.part(8).box((side*(w/2+.02),1.12,.12),(.045,.57,.64))
            a.part(7).box((side*(w/2+.07),1.12,.12),(.04,.60,.035))
            a.part(7).box((side*(w/2+.20),.92,d/2),(.055,1.65,.055))
        a.part(9).box((.72,h+.92,.44),(.35,.75,.38))
        a.part(7).box((.72,h+1.30,.44),(.44,.08,.46))
    else:
        for x in range(11): wall.box((-w/2+x*w/10,h/2+.12,front-.015),(.028,h,.026))
    return a


def fence():
    a=Asset('Fence')
    for x in (-.9,0,.9): a.part(0).box((x,.38,0),(.09,.76,.09))
    for y in (.25,.58): a.part(7).box((0,y,0),(1.92,.07,.06))
    return a


def stone():
    a=Asset('Stone'); p=a.part(9); n=7
    lower=[(math.sin(i*math.tau/n)*.4,0,math.cos(i*math.tau/n)*.34) for i in range(n)]
    upper=[(math.sin(i*math.tau/n)*.27,.32+(i%3)*.035,math.cos(i*math.tau/n)*.22) for i in range(n)]
    for i in range(n): j=(i+1)%n; p.face([lower[j],upper[j],upper[i],lower[i]])
    p.face(upper); p.face(list(reversed(lower)))
    return a


def ribbon(name, points, widths, material, lift, water=False):
    p=Part(material)
    # Smooth centreline and width changes; endpoints remain exactly connected to the lake/roads.
    smooth=[];smooth_width=[]
    for i in range(len(points)-1):
        a=points[max(0,i-1)];b=points[i];c=points[i+1];d=points[min(len(points)-1,i+2)]
        for step in range(6):
            t=step/6
            smooth.append(tuple(.5*((2*b[k])+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-d[k])*t*t+(-a[k]+3*b[k]-3*c[k]+d[k])*t*t*t) for k in range(2)))
            smooth_width.append(widths[i]*(1-t)+widths[i+1]*t)
    points=smooth+[points[-1]];widths=smooth_width+[widths[-1]]
    # Separate shallow/deep strips supply an authored bank coordinate to the water shader.
    fractions=[-1,-.72,0,.72,1]
    rows=[]
    for i, (x,z) in enumerate(points):
        prev=points[max(0,i-1)]; nxt=points[min(len(points)-1,i+1)]
        dx,dz=nxt[0]-prev[0],nxt[1]-prev[1]; length=max(math.hypot(dx,dz),1e-5)
        rows.append([(x-dz/length*widths[i]*f,lift,z+dx/length*widths[i]*f) for f in fractions])
    for row in range(len(rows)-1):
        for j in range(len(fractions)-1):
            pts=[rows[row][j],rows[row+1][j],rows[row+1][j+1],rows[row][j+1]]
            uv=[(abs(fractions[j]),0),(abs(fractions[j]),0),(abs(fractions[j+1]),0),(abs(fractions[j+1]),0)]
            # Make every quad face up regardless of the direction of its centreline.
            if cross(tuple(b-a for a,b in zip(pts[0],pts[1])), tuple(b-a for a,b in zip(pts[0],pts[2])))[1]<0:
                pts.reverse(); uv.reverse()
            p.face(pts,uv)
    return dict(name=name,water=water,parts=[p.data()])


def pond(name, x, z, rx, rz, material, lift, water=False):
    p=Part(material); rings=[]; count=64
    for radius in (0,.48,.80,1):
        ring=[]
        for i in range(count):
            a=i*math.tau/count; irregular=1+.11*math.sin(3*a+.4)+.055*math.sin(7*a)
            ring.append((x+math.cos(a)*rx*radius*irregular,lift,z+math.sin(a)*rz*radius*irregular))
        rings.append(ring)
    for r in range(3):
        for i in range(count):
            j=(i+1)%count
            if r==0:
                p.face([rings[0][i],rings[1][j],rings[1][i]],[(0,0),(.48,0),(.48,0)])
            else:
                radii=(0,.48,.80,1)
                p.face([rings[r][i],rings[r][j],rings[r+1][j],rings[r+1][i]],
                       [(radii[r],0),(radii[r],0),(radii[r+1],0),(radii[r+1],0)])
    return dict(name=name,water=water,parts=[p.data()])


def generate():
    palette=[('Bark',(.23,.16,.095), 'Bark'),('BirchBark',(.67,.64,.55),'Bark'),
             ('Leaf',(.17,.29,.11),'Leaf'),('LeafTips',(.29,.39,.16),'Leaf'),('Pine',(.09,.22,.15),'Leaf'),
             ('LimePlaster',(.61,.56,.44),'Concrete'),('Terracotta',(.35,.16,.10),'Stone'),
             ('WeatheredTimber',(.38,.32,.23),'Bark'),('Window',(.12,.21,.24),'Metal'),
             ('Fieldstone',(.35,.34,.29),'Stone'),('Road',(.25,.25,.22),'Stone'),
             ('Gravel',(.45,.40,.29),'Soil'),('Bank',(.25,.27,.16),'Soil'),('Water',(.025,.19,.24),'WetStone')]
    assets=[tree(species,seed) for species in ('Oak','Birch','Pine') for seed in (8,9)]
    assets += [shrub('Shrub',16),shrub('Fern',21,True),reeds(),cottage('Cottage'),cottage('CottageSlate',slate=True),cottage('Shed',True),fence(),stone()]
    placements=[]; surfaces=[]
    def place(asset,x,z,yaw=0,scale=1):
        placements.append(dict(asset=asset,x=round(x,4),z=round(z,4),yaw=round(yaw,3),scale=round(scale,4)))
        placements.append(dict(asset=asset,x=round(-x,4),z=round(-z,4),yaw=round((yaw+180)%360,3),scale=round(scale,4)))
    stream=[(-9,34.8),(-9.6,33),(-10.4,31.3),(-11.2,29.9),(-12.3,28.3),(-13.3,26.6),(-13.5,24.8),(-12.5,22.7),(-12,20.4)]
    road=[(-6,25.2),(-3,25.4),(0,25.8),(4,26),(8,26.2),(12,26.4),(16,26.9),(20,27.4)]
    def paired(s):
        surfaces.append(s)
        other=json.loads(json.dumps(s)); other['name']+='South'
        for part in other['parts']:
            for i in range(0,len(part['vertices']),3): part['vertices'][i]*=-1; part['vertices'][i+2]*=-1
        surfaces.append(other)
    paired(ribbon('RiverBank',stream,[.96,.94,1,.9,.92,.94,1.02,1.18,1.35],12,.035))
    paired(ribbon('River',stream,[.48,.50,.57,.49,.53,.55,.64,.81,1.05],13,.059,True))
    paired(pond('LakeBank',-12,19,4.15,2.72,12,.035))
    paired(pond('Lake',-12,19,3.62,2.20,13,.059,True))
    paired(ribbon('RoadShoulder',road,[.83]*len(road),11,.038))
    paired(ribbon('VillageRoad',road,[.57]*len(road),10,.052))
    for x,z in ((4,29),(10,29.4),(16,30.1)):
        place('CottageSlate' if x==10 else 'Cottage',x,z,0,.80 if x==10 else .90)
        driveway=[(x,z-1.25),(x,z-2),(x,26.2+(x-8)*.08)]
        paired(ribbon('CottagePath'+str(x),driveway,[.35]*3,11,.063))
        for side in (-1,1):
            place('Fence',x+side*1.85,z,90,.8)
            place('Shrub',x+side*1.25,z+1.7,side*25,.55)
        place('Fence',x,z+1.8,0,1.15)
    place('Shed',20,32.3,0,.75)
    paired(ribbon('ShedPath',[(20,31.3),(19,29.5),(18,27.2)],[.3]*3,11,.063))
    paired(ribbon('FootpathToLake',[(-6,25.2),(-6.8,23.6),(-7.4,21.8),(-8.1,20.3)],[.3,.28,.3,.26],11,.049))
    rng=random.Random(2601014)
    # Irregular woodland edges and clearings, never a row/grid of cloned crowns.
    centers=[(-37,29),(-38,14),(-38,-17),(-36,-29),(-24,31),(-22,-31),(-17,8),(-15,-14),(2,33),(28,33)]
    for c,(cx,cz) in enumerate(centers):
        for j in range(7):
            angle=rng.uniform(0,math.tau); radius=rng.uniform(.4,3.5)
            x=cx+math.cos(angle)*radius; z=cz+math.sin(angle)*radius
            if abs(x)>40.5 or abs(z)>34.5 or abs(z)<8 or math.hypot(x,z)<13: continue
            if -17<x<-7 and z>15: continue
            species=('Oak','Pine','Birch','Oak','Pine')[(c+j)%5]
            place(species+str(j%2),x,z,rng.uniform(0,360),rng.uniform(.66,1.04))
            for k in range(2):
                sx=x+rng.uniform(-.9,.9); sz=z+rng.uniform(-.9,.9)
                place('Fern' if k else 'Shrub',sx,sz,rng.uniform(0,360),rng.uniform(.5,.9))
    # Shore vegetation follows the authored water contour, with gaps and varied sizes.
    for i in range(18):
        angle=i*math.tau/18; radius=1+.11*math.sin(3*angle+.4)+.055*math.sin(7*angle)
        x=-12+math.cos(angle)*3.96*radius; z=19+math.sin(angle)*2.56*radius
        place('Reeds' if i%3 else 'Stone',x,z,i*31,.6+(i%4)*.14)
    for i in range(1,len(stream)-1):
        x,z=stream[i]; place('Reeds',x+.92,z,i*51,.73)
        if i%2: place('Stone',x-.9,z+.2,i*61,.85)
    result=dict(version=1,materials=[dict(name=n,color=list(c)+[1],finish=f) for n,c,f in palette],
                assets=[a.data() for a in assets],placements=placements,surfaces=surfaces)
    OUTPUT.parent.mkdir(parents=True,exist_ok=True)
    OUTPUT.write_text(json.dumps(result,separators=(',',':'))+'\n')
    meta(Path(str(OUTPUT)+'.meta'),'TextScriptImporter','Dressing.json')
    meta(OUTPUT.parent.with_suffix('.meta'), 'DefaultImporter', 'folder', True)
    return result


def meta(path, importer, name, folder=False):
    path.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'alterverse/EarthMap/'+name).hex+
                    ('\nfolderAsset: yes' if folder else '')+'\n'+importer+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


if __name__=='__main__':
    result=generate()
    print(f"Earth kit: {len(result['assets'])} assets, {len(result['placements'])} placements, {len(result['surfaces'])} surface strips")
