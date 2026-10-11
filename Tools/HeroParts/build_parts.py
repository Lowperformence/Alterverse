"""Four authored hero part replacements; +Y up, +Z forward, metres.

The shipping geometry is amended rather than regenerated. Non-target vertices,
normals, skin weights, weapon groups and gameplay markers remain unchanged.
NumPy-only head generation can be checked locally; Blender LTS writes source
scenes, Tahla's animated FBX and renders the exact exported triangles.
"""
from pathlib import Path
import argparse, copy, gzip, hashlib, json, math, subprocess
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/PartsRedesign'
RES = ROOT / 'Assets/Alterverse/Resources'
REVISION = 'hero-parts-20261011-v1'
BASE_COMMIT = 'bd7756321a7ac0ac9e32275c67bc03461547d00e'
PALETTES = {
    'CyberRanger':[(.028,.041,.044),(.080,.115,.120),(.46,.57,.57),(.025,.65,.36),(.007,.018,.016)],
    'UmbralMage':[(.018,.019,.026),(.055,.050,.075),(.84,.86,.89),(.57,.065,.95),(.015,.72,.78),(1,1,1)],
    'CyberShark':[(.035,.022,.014),(.58,.61,.62),(.22,.12,.065),(.04,.78,1),(.48,.29,.12),(.93,.94,.91)],
    'Tahla':[(.65,.39,.10),(.92,.86,.69),(.04,.31,.13),(.18,.82,.37)]
}

def baseline(path):
    """Rebuild from the reviewed original, including after generated assets are committed."""
    relative=path.relative_to(ROOT).as_posix()
    if (ROOT/'.git').exists():
        return subprocess.run(['git','show',BASE_COMMIT+':'+relative],cwd=ROOT,check=True,capture_output=True).stdout
    original=path.with_suffix(path.suffix+'.original')
    if original.exists():return original.read_bytes()
    raw=path.read_bytes();original.write_bytes(raw)
    return raw

def unit(v):
    v = np.asarray(v, dtype=float)
    return v / max(float(np.linalg.norm(v)), 1e-12)

class Parts:
    def __init__(self):
        self.v=[]; self.f=[]; self.m=[]; self.b=[]; self.b2=[]; self.w2=[]; self.parts=[]

    def add(self, name, vertices, faces, material, bone, secondary=None, weights=None):
        vertices=np.asarray(vertices,float); faces=np.asarray(faces,int).reshape(-1,3)
        cross=np.cross(vertices[faces[:,1]]-vertices[faces[:,0]],vertices[faces[:,2]]-vertices[faces[:,0]])
        faces=faces[np.linalg.norm(cross,axis=1)>1e-12]
        first=len(self.v); self.v.extend(vertices.tolist()); self.f.extend((faces+first).tolist())
        self.m.extend([material]*len(faces)); self.b.extend([bone]*len(vertices))
        self.b2.extend([secondary if secondary is not None else bone]*len(vertices))
        self.w2.extend(list(weights) if weights is not None else [0.0]*len(vertices))
        self.parts.append({'name':name,'first':first,'vertices':len(vertices),'triangles':len(faces),'bone':bone,'material':material})

    def ellipsoid(self,name,c,r,mat,bone,segments=32,rings=16):
        c=np.asarray(c); r=np.asarray(r); vertices=[c+[0,r[1],0]]; faces=[]
        for i in range(1,rings):
            a=math.pi*i/rings
            for j in range(segments):
                b=math.tau*j/segments
                vertices.append(c+r*np.array([math.sin(a)*math.sin(b),math.cos(a),math.sin(a)*math.cos(b)]))
        vertices.append(c-[0,r[1],0]); last=len(vertices)-1
        for j in range(segments):faces.append([0,1+j,1+(j+1)%segments])
        for i in range(rings-2):
            for j in range(segments):
                a=1+i*segments+j; b=1+i*segments+(j+1)%segments
                faces.extend([[a,a+segments,b],[b,a+segments,b+segments]])
        for j in range(segments): faces.append([last,last-1-j,last-1-(j+1)%segments])
        vertices=np.asarray(vertices)
        for face in faces:
            a,b,d=vertices[face]
            if np.dot(np.cross(b-a,d-a),(a+b+d)/3-c)<0:face.reverse()
        self.add(name,vertices,faces,mat,bone)

    def tube(self,name,points,radius,mat,bone,sides=10):
        points=np.asarray(points,float); radii=np.broadcast_to(radius,(len(points),)); vertices=[];faces=[];previous=None
        for i,p in enumerate(points):
            tangent=unit(points[min(i+1,len(points)-1)]-points[max(i-1,0)])
            u=np.cross(tangent,[0,0,1])
            if np.linalg.norm(u)<.2:u=np.cross(tangent,[1,0,0])
            u=unit(u)
            if previous is not None and np.dot(u,previous)<0:u=-u
            previous=u;v=unit(np.cross(tangent,u))
            for j in range(sides):vertices.append(p+radii[i]*(math.cos(math.tau*j/sides)*u+math.sin(math.tau*j/sides)*v))
        for i in range(len(points)-1):
            for j in range(sides):
                a=i*sides+j;b=i*sides+(j+1)%sides
                faces.extend([[a,b,b+sides],[a,b+sides,a+sides]])
        if np.linalg.norm(points[0]-points[-1])>1e-8:
            for j in range(1,sides-1):faces.extend([[0,j+1,j],[(len(points)-1)*sides,(len(points)-1)*sides+j,(len(points)-1)*sides+j+1]])
        self.add(name,vertices,faces,mat,bone)

    def ring(self,name,c,r,t,mat,bone,normal=(0,1,0),segments=40):
        n=unit(normal);u=np.cross(n,[0,1,0])
        if np.linalg.norm(u)<.1:u=np.array([1,0,0])
        u=unit(u);v=unit(np.cross(n,u));c=np.asarray(c)
        points=[c+r*(math.cos(a)*u+math.sin(a)*v) for a in np.linspace(0,math.tau,segments+1)]
        self.tube(name,points,t,mat,bone,8)

    def plate(self,name,outline,z,depth,mat,bone):
        xy=np.asarray(outline,float)
        signed=np.sum(xy[:,0]*np.roll(xy[:,1],-1)-xy[:,1]*np.roll(xy[:,0],-1))
        if signed<0:xy=xy[::-1]
        center=xy.mean(0);vertices=[];faces=[];n=len(xy)
        for scale,d in [(1,-depth*.45),(1,depth*.05),(.86,depth*.75)]:
            for x,y in center+(xy-center)*scale:vertices.append([x,y,z+d])
        vertices.append([*center,z+depth*.88]);top=len(vertices)-1
        for i in range(2):
            for j in range(n):
                a=i*n+j;b=i*n+(j+1)%n
                faces.extend([[a,b,b+n],[a,b+n,a+n]])
        for j in range(n):faces.append([2*n+j,2*n+(j+1)%n,top])
        for j in range(1,n-1):faces.append([0,j+1,j])
        self.add(name,vertices,faces,mat,bone)

    def patch(self,name,c,r,a_range,b_range,mat,bone,rows=6,columns=6):
        c=np.asarray(c);r=np.asarray(r);vertices=[];faces=[]
        for a in np.linspace(*a_range,rows+1):
            for b in np.linspace(*b_range,columns+1):
                vertices.append(c+r*np.array([math.sin(a)*math.sin(b),math.cos(a),math.sin(a)*math.cos(b)]))
        for i in range(rows):
            for j in range(columns):
                a=i*(columns+1)+j;b=a+1;d=a+columns+1;e=d+1
                faces.extend([[a,b,e],[a,e,d]])
        vertices=np.asarray(vertices)
        for face in faces:
            a,b,d=vertices[face]
            if np.dot(np.cross(b-a,d-a),(a+b+d)/3-c)<0:face.reverse()
        self.add(name,vertices,faces,mat,bone)

    def normals(self):
        v=np.asarray(self.v);f=np.asarray(self.f,int);n=np.zeros_like(v)
        cross=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]])
        for j in range(3):np.add.at(n,f[:,j],cross)
        lengths=np.linalg.norm(n,axis=1); n[lengths>1e-12]/=lengths[lengths>1e-12,None]
        n[lengths<=1e-12]=[0,1,0]
        return n

def build_head(data,kind):
    p=Parts();head=data['boneNames'].index('Head');anchor=np.array(data['anchors'][head]['head'])
    mage=kind=='UmbralMage';cy=float(anchor[1])+( .205 if mage else .2126 )
    radius=np.array([.102,.119,.095]) if mage else np.array([.108,.145,.103])
    center=np.array([0,cy,-.005]);metal=2;dark=1;black=0 if mage else 4;energy=3
    low=float(anchor[1])-.024;neck_top=cy-radius[1]+.028
    p.tube('articulated neck chassis',[[0,low,0],[0,neck_top,0]],[.041,.047],black,head,24)
    for y in np.linspace(low+.008,neck_top-.006,5):
        p.ring('machined neck vertebra',[0,y,0],.046,.0038,metal if not mage else dark,head)
    p.ellipsoid('protected cranial chassis',center,radius,dark,head,48,26)
    # Curved shell plates and open panel seams follow the skull rather than boxy primitives.
    for side in [-1,1]:
        for j,(a,b) in enumerate([(.10,1.05),(1.085,1.88),(1.915,2.54)]):
            for k in range(4):
                b0=side*(.54+k*.63); b1=side*(.54+(k+1)*.63-.035)
                p.patch('ceramic crown panel' if mage else 'graphite crown panel',center,radius+np.array([.003,.003,.003]),(a,b),(min(b0,b1),max(b0,b1)),metal if mage or k==0 else dark,head,6,5)
        p.patch('layered brow shell',center,radius+np.array([.005,.003,.005]),(.17,1.03),(min(side*.045,side*.50),max(side*.045,side*.50)),metal if mage else dark,head,8,5)
        # Recessed optics with separate armored brows and sculpted cheek/jaw pieces.
        def outline(points):return [(side*x,cy+y) for x,y in points]
        p.plate('dark optical aperture',outline([(.013,.044),(.084,.040),(.087,.011),(.040,-.008),(.012,.005)]),.095,.011,black,head)
        p.plate('angled optical emitter',outline([(.022,.025),(.072,.030),(.070,.017),(.037,.003),(.023,.009)]),.108,.005,energy,head)
        p.plate('armored eyebrow',outline([(.010,.062),(.052,.084),(.092,.052),(.094,.035),(.071,.040),(.026,.048)]),.086,.024,metal,head)
        p.plate('sculpted cheek armor',outline([(.088,.028),(.098,-.008),(.079,-.074),(.045,-.108),(.017,-.103),(.032,-.049),(.068,-.019)]),.081,.024,metal,head)
        p.plate('lower jaw lamella',outline([(.073,-.070),(.060,-.111),(.029,-.133),(.013,-.101),(.041,-.075)]),.068,.018,metal,head)
        p.tube('cheek recessed light channel',[[side*.078,cy-.017,.110],[side*.059,cy-.061,.108],[side*.040,cy-.084,.096]],.0019,energy,head,7)
        for x,y,z in [(.083,.050,.106),(.083,-.046,.106),(.051,-.091,.100)]:
            p.ellipsoid('recessed cheek fastener',[side*x,cy+y,z],[.0036,.0036,.0014],dark,head,10,6)
        temple=[side*.108,cy+.017,-.008]
        p.ellipsoid('temple mechanism housing',temple,[.017,.044,.041],metal,head,32,16)
        p.ring('temple graphite inset',[side*.125,cy+.018,-.008],.031,.005,dark,head,(side,0,0))
        p.ring('flush optical temple ring',[side*.130,cy+.018,-.008],.024,.0028,energy,head,(side,0,0))
        p.ellipsoid('temple optical core',[side*.132,cy+.018,-.008],[.0018,.018,.018],black,head,24,12)
    p.plate('central nasal bridge',[(-.016,cy+.055),(0,cy+.073),(.016,cy+.055),(.022,cy-.039),(0,cy-.062),(-.022,cy-.039)],.099,.023,metal,head)
    p.plate('lower central faceplate',[(-.027,cy-.048),(0,cy-.031),(.027,cy-.048),(.022,cy-.105),(0,cy-.137),(-.022,cy-.105)],.087,.024,metal,head)
    for x in [-.014,0,.014]:p.tube('lower face recessed vent',[[x,cy-.063,.113],[x*.85,cy-.086,.109]],.0016,black,head,6)
    if mage:
        p.plate('violet forehead diamond',[(0,cy+.110),(.013,cy+.081),(0,cy+.057),(-.013,cy+.081)],.091,.008,energy,head)
        angles=np.linspace(.015*math.pi,.985*math.pi,45)
        points=[[.129*math.cos(a),cy+.043+.129*math.sin(a),-.043] for a in angles]
        p.tube('segmented orbital crown',points,.0085,metal,head,12)
        p.tube('crown violet inset',[[v[0],v[1],v[2]+.008] for v in points],.0031,energy,head,8)
        for a in [math.pi*.18,math.pi*.50,math.pi*.82]:
            x=.129*math.cos(a);y=cy+.043+.129*math.sin(a)
            p.plate('orbital crown retaining clip',[(x-.008,y-.010),(x+.008,y-.010),(x+.008,y+.010),(x-.008,y+.010)],-.040,.018,metal,head)
        for side in [-1,1]:
            p.plate('short swept crown vane',[(side*.104,cy+.013),(side*.149,cy+.077),(side*.138,cy+.112),(side*.115,cy+.074)],-.003,.017,metal,head)
            p.tube('orbital mechanical support',[[side*.108,cy+.034,-.008],[side*.128,cy+.051,-.043]],.006,dark,head,10)
    else:
        p.tube('axial cyan sensor',[[0,cy+.105,.077],[0,cy+.080,.098]],.0025,energy,head,8)
        for side in [-1,1]:
            p.tube('cranial titanium rail',[[side*.019,cy+.139,.030],[side*.040,cy+.109,.070],[side*.065,cy+.085,.080]],.0038,metal,head,8)
            for j in range(3):
                p.plate('temple machined vent',[(side*.077,cy+.093-j*.012),(side*.095,cy+.090-j*.012),(side*.093,cy+.084-j*.012),(side*.078,cy+.088-j*.012)],.060,.002,black,head)
    return p

def read_model(kind):return json.loads(gzip.decompress(baseline(RES/kind/'Model.bytes')))

def save_model(kind,data):
    data['designRevision']=REVISION
    (RES/kind/'Model.bytes').write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))

def amend(data,remove,p,clothing=False):
    result=copy.deepcopy(data);count=len(data['boneIndices']);remove=np.asarray(remove,bool)
    assert remove.shape==(count,)
    keep=~remove;remap=np.full(count,-1,int);remap[keep]=np.arange(keep.sum());first=int(keep.sum())
    new_n=p.normals()
    for key,new in [('vertices',p.v),('normals',new_n.tolist())]:
        result[key]=np.asarray(data[key]).reshape(-1,3)[keep].ravel().tolist()+np.asarray(new).ravel().tolist()
    result['boneIndices']=np.asarray(data['boneIndices'])[keep].tolist()+p.b
    for key,addition,default in [('groups',[0]*len(p.v),[0]*count),('boneIndices2',p.b2,data['boneIndices']),('boneWeights2',p.w2,[0.0]*count)]:
        if key in data or clothing:result[key]=np.asarray(data.get(key,default))[keep].tolist()+addition
    faces=np.asarray(p.f,int);mats=np.asarray(p.m)
    for m,sub in enumerate(data['submeshes']):
        old=np.asarray(sub['triangles'],int).reshape(-1,3); selected=old[~remove[old].any(axis=1)]
        result['submeshes'][m]['triangles']=remap[selected].ravel().tolist()+(faces[mats==m]+first).ravel().tolist()
    for key in ['tailVertices','mouthVertices','clothingVertices']:
        if key in data:result[key]=[int(remap[i]) for i in data[key] if remap[i]>=0]
    if clothing:result['clothingVertices']=list(range(first,first+len(p.v)))
    for key in ['boneNames','anchors','markers','tailPivot','hookGrip']:
        if key in data:assert result[key]==data[key],key
    # Exact body preservation, including exported normal values and skin bindings.
    for key in ['vertices','normals']:
        assert np.array_equal(np.asarray(result[key]).reshape(-1,3)[:first],np.asarray(data[key]).reshape(-1,3)[keep]),key
    report={'preservedVertices':first,'removedVertices':int(remove.sum()),'newVertices':len(p.v),'newTriangles':len(p.f),'newParts':len(p.parts),'rigAndMarkersPreserved':True,'bodyPositionsAndNormalsExact':True}
    report['preservedVertexDigest']=hashlib.sha256(np.asarray(data['vertices']).reshape(-1,3)[keep].tobytes()).hexdigest()
    validate(result)
    return result,report

def validate(data):
    v=np.asarray(data['vertices']).reshape(-1,3);n=np.asarray(data['normals']).reshape(-1,3);bi=np.asarray(data['boneIndices'])
    assert np.isfinite(v).all() and np.isfinite(n).all()
    assert len(v)==len(n)==len(bi) and bi.min()>=0 and bi.max()<len(data['boneNames'])
    assert np.allclose(np.linalg.norm(n,axis=1),1,atol=.015)
    groups=np.asarray(data.get('groups',[0]*len(v)))
    for sub in data['submeshes']:
        t=np.asarray(sub['triangles'],int).reshape(-1,3)
        if not len(t):continue
        assert t.min()>=0 and t.max()<len(v)
        assert (groups[t].max(1)==groups[t].min(1)).all()
    if 'clothingVertices' in data:
        tagged=np.asarray(data['clothingVertices']);assert (tagged>=0).all() and (tagged<len(v)).all()
        assert (groups[tagged]==0).all() and np.all(np.diff(tagged)>0)

def source_naval_parts():
    path=ROOT/'Tools/CyberShark/build_cyber_shark.py'
    code=path.read_text().split("bpy.ops.object.select_all(action='SELECT')")[0]
    code=code.replace('import bpy\n','').replace('from mathutils import Vector\n','')
    ns={'__file__':str(path),'__name__':'hero_parts_reference'}
    exec(compile(code,str(path),'exec'),ns)
    return ns

def old_clothes(data):
    ns=source_naval_parts();v=np.asarray(ns['V']);f=np.asarray(ns['F'],int)
    names=('pirate trouser thigh','open naval vest','vest brass lapel','vest gold braid','split coat tail','coat tail piping','leather cross-body baldric','baldric stud')
    wanted=set();first=0;part_names=[]
    def signature(points):return tuple(sorted(tuple(p) for p in np.round(points,5)))
    for part in ns['PARTS']:
        length=part['triangles']
        if part['name'].startswith(names):
            part_names.append(part['name'])
            wanted.update(signature(v[face]) for face in f[first:first+length])
        first+=length
    current=np.asarray(data['vertices']).reshape(-1,3);remove=np.zeros(len(current),bool);matched=0
    for sub in data['submeshes']:
        for tri in np.asarray(sub['triangles'],int).reshape(-1,3):
            if signature(current[tri]) in wanted:remove[tri]=True;matched+=1
    # Blender's runtime resource uses independent corners: target islands must not share with body.
    assert remove.sum()>1000 and matched>450,('Expected original garment parts',matched,int(remove.sum()))
    for sub in data['submeshes']:
        for tri in np.asarray(sub['triangles'],int).reshape(-1,3):
            assert not remove[tri].any() or remove[tri].all(),'Garment selection would cut a body triangle'
    assert not remove[np.asarray(data.get('mouthVertices',[]),int)].any()
    assert not remove[np.asarray(data.get('tailVertices',[]),int)].any()
    return remove,part_names,matched

def build_clothes(data):
    p=Parts();names=data['boneNames'];idx=lambda n:names.index(n)
    # Contoured open-front panels follow the measured broad shark torso and clear its fins.
    rows=[(1.067,.194,.149),(1.14,.216,.161),(1.25,.253,.183),(1.34,.284,.202),(1.43,.315,.211),(1.505,.255,.175),(1.555,.181,.144)]
    for side in [-1,1]:
        vertices=[];faces=[];columns=18
        for y,rx,rz in rows:
            for a in np.linspace(-1.36,.99,columns+1):vertices.append([side*rx*math.cos(a),y,rz*math.sin(a)])
        for i in range(len(rows)-1):
            for j in range(columns):
                a=i*(columns+1)+j;b=a+1;d=a+columns+1;e=d+1
                tri=[[a,d,e],[a,e,b]]
                if side<0:tri=[face[::-1] for face in tri]
                faces.extend(tri)
        p.add('tailored naval jacket panel',vertices,faces,2,idx('Chest'))
        front=[[side*rx*math.cos(.99),y,rz*math.sin(.99)+.006] for y,rx,rz in rows]
        rear=[[side*rx*math.cos(-1.36),y,rz*math.sin(-1.36)-.004] for y,rx,rz in rows]
        p.tube('rolled leather front welt',front,.0055,0,idx('Chest'),10)
        p.tube('rear fin opening binding',rear,.0042,0,idx('Chest'),8)
        # Flat flax seams are small geometric stitches, not painted-on embellishments.
        for a,b in zip(front[:-1],front[1:]):
            a=np.array(a);b=np.array(b)
            for t in np.arange(.07,.95,.12):p.tube('jacket saddle stitch',[a+(b-a)*t+[side*.005,0,.003],a+(b-a)*(t+.048)+[side*.005,0,.003]],.0008,4,idx('Chest'),5)
        for j in range(4):
            p.ellipsoid('naval jacket brass closure',[side*(.118-j*.006),1.43-j*.079,.178-j*.008],[.006,.006,.0024],4,idx('Chest'),12,8)
        p.plate('folded high lapel',[(side*.101,1.53),(side*.181,1.551),(side*.174,1.476),(side*.123,1.409),(side*.112,1.475)],.141,.018,0,idx('Chest'))
        p.plate('reinforced front pocket',[(side*.146,1.271),(side*.216,1.279),(side*.202,1.171),(side*.132,1.162)],.153,.012,2,idx('Chest'))
        p.tube('pocket flap welt',[[side*.143,1.268,.173],[side*.214,1.276,.173]],.0028,0,idx('Chest'),8)
        for z,frontward in [(.085,True),(-.130,False)]:
            p.plate('articulated split jacket skirt',[(side*.083,1.064),(side*.212,1.066),(side*.281,.787),(side*.236,.707),(side*.133,.774)],z,.013 if frontward else -.013,2,idx('ThighL' if side<0 else 'ThighR'))
            p.tube('coat hem leather binding',[[side*.134,.778,z+( .012 if frontward else -.012)],[side*.236,.710,z+( .012 if frontward else -.012)],[side*.278,.790,z+( .012 if frontward else -.012)]],.0032,0,idx('ThighL' if side<0 else 'ThighR'),8)
    for side,sign in [('L',-1),('R',1)]:
        upper=idx('UpperArm'+side);a=np.array(data['anchors'][upper]['head']);e=np.array(data['anchors'][idx('Forearm'+side)]['head'])
        ts=np.linspace(.18,.64,9);points=[a+(e-a)*t for t in ts];radii=np.linspace(.124,.103,len(ts))
        p.tube('jacket short articulated sleeve '+side,points,radii,2,upper,32)
        axis=unit(e-a);p.ring('turned leather sleeve hem '+side,points[-1],radii[-1],.0032,0,upper,axis,40)
        thigh=idx('Thigh'+side);h=np.array(data['anchors'][thigh]['head']);k=np.array(data['anchors'][idx('Shin'+side)]['head'])
        ts=np.linspace(0,1,13);radii=[.079+.031*math.sin(math.pi*(.10+.85*t)) for t in ts]
        p.tube('shaped reinforced trousers '+side,[h+(k-h)*t for t in ts],radii,2,thigh,36)
        center=(h+k)*.5
        p.plate('trouser cargo pocket '+side,[(center[0]-.049,center[1]+.077),(center[0]+.049,center[1]+.077),(center[0]+.045,center[1]-.063),(center[0]-.043,center[1]-.063)],.104,.010,2,thigh)
        p.tube('cargo pocket flap '+side,[[center[0]-.048,center[1]+.052,.120],[center[0]+.048,center[1]+.052,.120]],.003,0,thigh,8)
        for x in [-.030,.030]:p.ellipsoid('cargo pocket snap '+side,[center[0]+x,center[1]+.049,.124],[.0038,.0038,.0013],4,thigh,10,6)
        # Long outer seams follow the leg, including the original authored leg angle.
        for offset in [-.004,.004]:
            line=[h+(k-h)*t+[sign*(r+.002),0,offset] for t,r in zip(ts,radii)]
            p.tube('reinforced trouser double seam '+side,line,.0012,0,thigh,6)
    # Two rear hip panels leave a genuine central opening around the moving tail root.
    for sign in [-1,1]:
        verts=[];faces=[];cols=18;levels=[(.872,.181,.125),(.932,.205,.148),(1.022,.190,.137)]
        for y,rx,rz in levels:
            for a in np.linspace(-1.27,1.42,cols+1):verts.append([sign*rx*math.cos(a),y,rz*math.sin(a)])
        for row in range(2):
            for j in range(cols):
                a=row*(cols+1)+j;b=a+1;d=a+cols+1;e=d+1
                ts=[[a,d,e],[a,e,b]];faces.extend(ts if sign>0 else [f[::-1] for f in ts])
        p.add('tail-socket trouser yoke',verts,faces,2,idx('Hips'))
    return p

def obj_source(path,p,material_names,colors):
    lines=['# '+REVISION,'# +Y up / +Z forward / metres','mtllib '+path.with_suffix('.mtl').name]
    for v in p.v:lines.append('v '+' '.join(f'{x:.8f}' for x in v))
    for n in p.normals():lines.append('vn '+' '.join(f'{x:.8f}' for x in n))
    previous=None
    for face,mat in zip(p.f,p.m):
        if mat!=previous:lines.append('usemtl '+material_names[mat].replace(' ','_'));previous=mat
        lines.append('f '+' '.join(f'{i+1}//{i+1}' for i in face))
    path.write_text('\n'.join(lines)+'\n')
    materials=[]
    for name,color in zip(material_names,colors):
        materials.extend(['newmtl '+name.replace(' ','_'),'Kd '+' '.join(map(str,color)),'Ks 0.25 0.25 0.25','Ns 90',''])
    path.with_suffix('.mtl').write_text('\n'.join(materials)+'\n')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--heads-only',action='store_true');args=parser.parse_args()
    ART.mkdir(parents=True,exist_ok=True);report={'revision':REVISION,'baseCommit':BASE_COMMIT,'models':{}}
    results={}
    for kind,label in [('CyberRanger','Sivra'),('UmbralMage','Ione')]:
        data=read_model(kind);p=build_head(data,kind)
        remove=np.asarray(data['boneIndices'])==data['boneNames'].index('Head')
        changed,checks=amend(data,remove,p);save_model(kind,changed)
        report['models'][label]=checks;results[kind]=(changed,p)
        obj_source(ART/(label+'-head.obj'),p,[f'Surface_{i}' for i in range(len(data['submeshes']))],PALETTES[kind])
        print('HEAD_COMPLETE',label,json.dumps(checks),flush=True)
    if not args.heads_only:
        data=read_model('CyberShark');remove,names,matched=old_clothes(data);p=build_clothes(data)
        changed,checks=amend(data,remove,p,True);save_model('CyberShark',changed)
        checks.update({'replacedParts':names,'matchedOriginalTriangles':matched,'mouthTailAnchorAndHookExact':True})
        report['models']['Nerok']=checks;results['CyberShark']=(changed,p)
        obj_source(ART/'Nerok-clothes.obj',p,[f'Surface_{i}' for i in range(len(data['submeshes']))],PALETTES['CyberShark'])
        import blender_export
        blender_export.export_all(ROOT,results,report)
    (ART/'validation-report.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PARTS_COMPLETE',json.dumps(report),flush=True)

if __name__=='__main__':main()
