"""New Traveler prototype geometry. No external geometry or textures imported.
Python 3 + numpy; matplotlib only for exact-mesh preview renders.
Coordinates: metres, Y up, Z front. Separate parts, no skin/animation claim.
"""
from pathlib import Path
import math, json, base64, hashlib
import numpy as np
ROOT = Path(__file__).resolve().parent
PARTS=[]
MATS=[('PressureWeave',(0.018,0.018,0.018),0.0,.88),('BlackCeramic',(.035,.035,.035),.25,.34),('SealRubber',(.009,.009,.009),0,.95),('Obsidian',(.012,.012,.012),.65,.13),('BlackMetal',(.052,.052,.052),.8,.32)]
def add(name,v,f,mat,group,bone,explode=(0,0,0)):
    v=np.asarray(v,dtype=float);f=np.asarray(f,dtype=np.uint32)
    PARTS.append(dict(name=name,v=v,f=f,mat=mat,group=group,bone=bone,explode=np.array(explode,float)))
def ell(name,c,s,mat,group,bone,explode=(0,0,0),seg=24,rows=12):
    v=[[0,1,0]]
    for i in range(1,rows):
        a=math.pi*i/rows
        for j in range(seg):
            b=math.tau*j/seg;v.append([math.sin(a)*math.cos(b),math.cos(a),math.sin(a)*math.sin(b)])
    v.append([0,-1,0]);f=[]
    for j in range(seg):f.append([0,1+(j+1)%seg,1+j])
    for i in range(rows-2):
        for j in range(seg):
            a=1+i*seg+j;b=1+i*seg+(j+1)%seg;f.extend([[a,b,b+seg],[a,b+seg,a+seg]])
    for j in range(seg):f.append([1+(rows-2)*seg+j,1+(rows-2)*seg+(j+1)%seg,len(v)-1])
    v=np.array(v)*s+c
    for face in f:
        a,b,d=v[face]
        if np.dot(np.cross(b-a,d-a),(a+b+d)/3-c)<0:face.reverse()
    add(name,v,f,mat,group,bone,explode)
def tube(name,a,b,r1,r2,mat,group,bone,explode=(0,0,0),seg=16):
    a=np.array(a);b=np.array(b);n=b-a;n/=np.linalg.norm(n)
    u=np.cross(n,[0,0,1])
    if np.linalg.norm(u)<1e-6:u=np.cross(n,[1,0,0])
    u/=np.linalg.norm(u);w=np.cross(n,u);v=[];f=[]
    for c,r in [(a,r1),(b,r2)]:
        for j in range(seg):v.append(c+r*(math.cos(math.tau*j/seg)*u+math.sin(math.tau*j/seg)*w))
    for j in range(seg):
        k=(j+1)%seg;f.extend([[j,k,k+seg],[j,k+seg,j+seg]])
    v.extend([a,b])
    for j in range(seg):
        k=(j+1)%seg;f.extend([[2*seg,k,j],[2*seg+1,j+seg,k+seg]])
    add(name,v,f,mat,group,bone,explode)
def ring(name,c,r,t,mat,group,bone,explode=(0,0,0),normal=(0,1,0),seg=36):
    n=np.array(normal,float);n/=np.linalg.norm(n);u=np.cross(n,[0,0,1])
    if np.linalg.norm(u)<1e-6:u=np.array([1.,0,0])
    u/=np.linalg.norm(u);w=np.cross(n,u);v=[];f=[];sides=8
    for i in range(seg):
        d=math.cos(math.tau*i/seg)*u+math.sin(math.tau*i/seg)*w
        for j in range(sides):v.append(np.array(c)+(r+t*math.cos(math.tau*j/sides))*d+t*math.sin(math.tau*j/sides)*n)
    for i in range(seg):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;d=((i+1)%seg)*sides+j;e=((i+1)%seg)*sides+(j+1)%sides;f.extend([[a,b,e],[a,e,d]])
    # Orient faces by nearest centreline to avoid inward torus normals.
    for face in f:
        a,b,d=np.array(v)[face];q=(a+b+d)/3-np.array(c);qplane=q-n*np.dot(q,n);center=np.array(c)+qplane/np.linalg.norm(qplane)*r
        if np.dot(np.cross(b-a,d-a),(a+b+d)/3-center)<0:face.reverse()
    add(name,v,f,mat,group,bone,explode)
def plate(name,xy,z,depth,mat,group,bone,explode=(0,0,0)):
    xy=np.array(xy);center=xy.mean(axis=0);n=len(xy);v=[];f=[]
    # closed chamfered polygon slab with convex front and rear fans
    for scale,d in [(1,-depth/2),(1,depth*.15),(.86,depth*.5)]:
        for x,y in center+(xy-center)*scale:v.append([x,y,z+d])
    v.extend([[*center,z+depth*.6],[*center,z-depth/2]])
    area=sum(xy[j,0]*xy[(j+1)%n,1]-xy[(j+1)%n,0]*xy[j,1] for j in range(n))
    for row in range(2):
        for j in range(n):
            a=row*n+j;b=row*n+(j+1)%n;f.extend([[a,b,b+n],[a,b+n,a+n]])
    for j in range(n):f.extend([[3*n,2*n+j,2*n+(j+1)%n],[3*n+1,(j+1)%n,j]])
    if area<0:f=[list(reversed(face)) for face in f]
    add(name,v,f,mat,group,bone,explode)
def boxplate(name,c,w,h,d,mat,group,bone,explode=(0,0,0)):
    x,y,z=c;k=min(w,h)*.12
    xy=[(x-w/2+k,y-h/2),(x+w/2-k,y-h/2),(x+w/2,y-h/2+k),(x+w/2,y+h/2-k),(x+w/2-k,y+h/2),(x-w/2+k,y+h/2),(x-w/2,y+h/2-k),(x-w/2,y-h/2+k)]
    plate(name,xy,z,d,mat,group,bone,explode)
def build():
    ell('T02_PelvicPressureSuit',(0,.97,0),(.19,.18,.13),0,'Torso','Hips')
    ell('T02_AbdominalPressureSuit',(0,1.19,0),(.165,.24,.115),0,'Torso','Spine')
    ell('T02_ThoracicPressureSuit',(0,1.43,0),(.245,.23,.135),0,'Torso','Chest')
    tube('H03_NeckSeal',(0,1.60,0),(0,1.73,0),.082,.075,2,'Helmet','Head',(0,.17,0))
    ell('H01_HelmetShell',(0,1.835,0),(.145,.19,.143),1,'Helmet','Head',(0,.48,0),32,20)
    ell('H02_SmokedVisor',(0,1.835,.112),(.116,.144,.061),3,'Helmet','Head',(0,.48,.29),32,16)
    for i in range(2):ring('H03_CollarGasket_'+str(i),(0,1.656+i*.035,0),.109,.012,4,'Helmet','Head',(0,.14+i*.08,0))
    for side,sign in [('L',-1),('R',1)]:
        boxplate('H01_Temple_'+side,(sign*.14,1.85,.035),.04,.10,.04,4,'Helmet','Head',(sign*.22,.48,0))
        xy=[(sign*.009,1.99),(sign*.118,1.966),(sign*.137,1.919),(sign*.018,1.939)]
        plate('H01_OffsetBrow_'+side,xy,.115,.024,1,'Helmet','Head',(sign*.10,.58,.08))
    for i in range(3):
        y=1.57-i*.128;w=.218-i*.028
        plate('T01_SteppedBreastplate_'+str(i+1),[(-w,y-.018),(-w*.88,y+.07),(w*.88,y+.07),(w,y-.018),(w*.60,y-.092),(0,y-.114),(-w*.60,y-.092)],.147+i*.002,.034,1,'Torso','Chest' if i==0 else 'Spine',(0,0,.28+i*.12))
        for sign in [-1,1]:ell('T01_Fastener_'+str(i)+'_'+str(sign),(sign*w*.77,y+.03,.177),(.007,.007,.004),4,'Torso','Chest',(0,0,.28+i*.12),8,4)
    for i in range(7):ring('T02_AbdominalRib_'+str(i),(0,1.10+i*.037,0),.123,.005,2,'Torso','Spine')
    boxplate('T02_BackpackBase',(0,1.435,-.18),.31,.36,.10,1,'Backpack','Chest',(0,0,-.32))
    for sign in [-1,1]:
        boxplate('T02_CirculationHousing_'+str(sign),(sign*.105,1.43,-.252),.088,.30,.06,4,'Backpack','Chest',(sign*.14,0,-.46))
        for i in range(6):boxplate('T02_CoolingSlat_'+str(sign)+'_'+str(i),(sign*.105,1.38+i*.023,-.287),.062,.011,.012,2,'Backpack','Chest',(sign*.14,0,-.46))
    ring('T03_WaistSeal',(0,1.02,0),.163,.015,2,'Torso','Hips')
    boxplate('T03_BeltBuckle',(0,1.02,.142),.09,.055,.025,4,'Torso','Hips',(0,0,.24))
    for side,s in [('L',-1),('R',1)]:
        shoulder=np.array([s*.26,1.565,0]);elbow=np.array([s*.39,1.30,.012]);wrist=np.array([s*.465,1.055,.035]);hip=np.array([s*.113,.97,0]);knee=np.array([s*.14,.545,.02]);ankle=np.array([s*.155,.155,.012]);ex=(s*.43,0,0)
        tube('A01_UpperArm_'+side,shoulder,elbow,.077,.066,0,'Arm'+side,'UpperArm'+side,ex)
        tube('A01_Forearm_'+side,elbow,wrist,.065,.048,0,'Arm'+side,'Forearm'+side,ex)
        ell('A01_ShoulderShell_'+side,shoulder+[s*.015,.015,0],(.12 if side=='L' else .09,.097,.10),1,'Arm'+side,'UpperArm'+side,(s*.64,.10,0))
        if side=='L':
            for i in range(2):boxplate('A01_ShoulderLouver_'+str(i),(s*.31,1.56-i*.061,.088),.15,.078,.024,1,'Arm'+side,'UpperArm'+side,(s*.64,.10,.04+i*.04))
        for center,tag,bone in [(elbow,'Elbow','Forearm'+side),(wrist,'Wrist','Hand'+side),(knee,'Knee','Shin'+side)]:
            ell('Seal_'+tag+'_'+side,center,(.068,.057,.064),2,'Leg'+side if tag=='Knee' else 'Arm'+side,bone,ex)
            for i in range(5):ring('SealRib_'+tag+'_'+side+'_'+str(i),center+[0,(i-2)*.017,0],.064 if tag!='Wrist' else .047,.005,2,'Leg'+side if tag=='Knee' else 'Arm'+side,bone,ex)
        mid=(shoulder+elbow)/2
        boxplate('A02_BicepArmor_'+side,(mid[0],mid[1],.071),.12,.18,.034,1,'Arm'+side,'UpperArm'+side,(s*.43,0,.23))
        mid=(elbow+wrist)/2
        boxplate('A03_Gauntlet_'+side,(mid[0],mid[1],.075),.105,.18,.04,1,'Arm'+side,'Forearm'+side,(s*.43,0,.23))
        palm=wrist+[s*.012,-.065,.026]
        ell('A04_Glove_'+side,palm,(.046,.068,.038),0,'Arm'+side,'Hand'+side,ex)
        for i in range(4):
            x=palm[0]+(i-1.5)*.021
            tube('A04_Finger_'+side+'_'+str(i),(x,palm[1]-.022,palm[2]+.022),(x,palm[1]-.079,palm[2]+.043),.010,.008,2,'Arm'+side,'Hand'+side,ex,10)
            ell('A04_Knuckle_'+side+'_'+str(i),(x,palm[1]-.017,palm[2]+.033),(.012,.015,.008),1,'Arm'+side,'Hand'+side,ex,8,4)
        tube('A04_Thumb_'+side,palm+[-s*.036,.01,.01],palm+[-s*.052,-.04,.04],.014,.010,2,'Arm'+side,'Hand'+side,ex,10)
        tube('L01_ThighSuit_'+side,hip,knee,.098,.074,0,'Leg'+side,'Thigh'+side,ex)
        tube('L01_ShinSuit_'+side,knee,ankle,.068,.049,0,'Leg'+side,'Shin'+side,ex)
        boxplate('L02_ThighArmor_'+side,(s*.13,.79,.095),.15,.25,.04,1,'Leg'+side,'Thigh'+side,(s*.43,0,.25))
        boxplate('L03_KneeArmor_'+side,(s*.14,.545,.087),.14,.125,.034,1,'Leg'+side,'Shin'+side,(s*.43,0,.25))
        boxplate('L04_ShinArmor_'+side,(s*.15,.325,.073),.105,.24,.035,1,'Leg'+side,'Shin'+side,(s*.43,0,.25))
        boxplate('L05_Boot_'+side,(s*.155,.09,.085),.158,.14,.255,1,'Leg'+side,'Foot'+side,(s*.43,-.055,0))
        boxplate('L05_BootSole_'+side,(s*.155,.027,.085),.17,.045,.27,2,'Leg'+side,'Foot'+side,(s*.43,-.11,0))
        for i in range(3):boxplate('L05_ToeBand_'+side+'_'+str(i),(s*.155,.082,.16+i*.018),.13,.08,.01,4,'Leg'+side,'Foot'+side,(s*.43,-.055,0))
    orb=np.array([.69,1.24,.16])
    ell('W01_OrbCore',orb,(.126,.126,.126),3,'Weapon','WeaponGripR',(1.05,.12,0),32,16)
    # Six closed spherical wedges, 4 mm wall; tiny gaps are deliberately visible.
    for k in range(6):
        v=[];f=[];rows=12;cols=8
        for r in [.16,.156]:
            for i in range(rows+1):
                a=.14+(math.pi-.28)*i/rows
                for j in range(cols+1):
                    b=math.tau*k/6+.022+(math.tau/6-.044)*j/cols
                    v.append(orb+r*np.array([math.sin(a)*math.cos(b),math.cos(a),math.sin(a)*math.sin(b)]))
        n=(rows+1)*(cols+1)
        for i in range(rows):
            for j in range(cols):
                a=i*(cols+1)+j;b=a+1;c=b+cols+1;d=a+cols+1
                f.extend([[a,b,c],[a,c,d],[a+n,c+n,b+n],[a+n,d+n,c+n]])
        edges=[]
        for j in range(cols):edges.extend([(j,j+1),(rows*(cols+1)+j+1,rows*(cols+1)+j)])
        for i in range(rows):edges.extend([((i+1)*(cols+1),i*(cols+1)),(i*(cols+1)+cols,(i+1)*(cols+1)+cols)])
        for a,b in edges:f.extend([[a,b,b+n],[a,b+n,a+n]])
        # reverse global orientation: outer latitude goes from north to south
        f=[list(reversed(q)) for q in f]
        angle=math.tau*(k+.5)/6
        add('W03_OrbShell_'+str(k+1),v,f,1,'Weapon','WeaponGripR',(1.05+.28*math.cos(angle),.12,.28*math.sin(angle)))
    ring('W02_OrbStabilizer',orb,.168,.007,4,'Weapon','WeaponGripR',(1.05,.36,0))
    for sign in [-1,1]:ring('W02_PolarCollar_'+str(sign),orb+[0,sign*.155,0],.027,.005,4,'Weapon','WeaponGripR',(1.05,.12+sign*.24,0))
def normals(v,f):
    n=np.zeros_like(v);face=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]])
    for i in range(3):np.add.at(n,f[:,i],face)
    n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-12);return n
def export_obj(exploded=False):
    name='traveler_exploded' if exploded else 'traveler_prototype';lines=['mtllib traveler.mtl'];off=1
    for p in PARTS:
        lines.extend(['o '+p['name'],'usemtl '+MATS[p['mat']][0]])
        v=p['v']+(p['explode'] if exploded else 0)
        lines += ['v %.7f %.7f %.7f'%tuple(q) for q in v]
        lines += ['f %d %d %d'%tuple(q+off) for q in p['f']];off+=len(v)
    (ROOT/(name+'.obj')).write_text('\n'.join(lines)+'\n')
def export_gltf():
    data=bytearray();views=[];access=[];meshes=[];nodes=[]
    def arr(a,typ,component,target):
        while len(data)%4:data.append(0)
        off=len(data);data.extend(a.tobytes());view=len(views);views.append(dict(buffer=0,byteOffset=off,byteLength=a.nbytes,target=target))
        entry=dict(bufferView=view,componentType=component,count=len(a),type=typ)
        if typ=='VEC3':entry.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
        access.append(entry);return len(access)-1
    for p in PARTS:
        v=p['v'].astype('<f4');n=normals(p['v'],p['f']).astype('<f4');f=p['f'].astype('<u4').ravel()
        vi=arr(v,'VEC3',5126,34962);ni=arr(n,'VEC3',5126,34962);fi=arr(f,'SCALAR',5125,34963)
        meshes.append(dict(name=p['name'],primitives=[dict(attributes=dict(POSITION=vi,NORMAL=ni),indices=fi,material=p['mat'])]))
        nodes.append(dict(name=p['name'],mesh=len(meshes)-1,extras=dict(group=p['group'],suggestedBone=p['bone'],explodeOffset=p['explode'].tolist())))
    mats=[dict(name=n,pbrMetallicRoughness=dict(baseColorFactor=list(c)+[1],metallicFactor=m,roughnessFactor=r)) for n,c,m,r in MATS]
    g=dict(asset=dict(version='2.0',generator='Alterverse Traveler Nocturne V01 original procedural source'),scene=0,scenes=[dict(nodes=list(range(len(nodes))))],nodes=nodes,meshes=meshes,materials=mats,buffers=[dict(byteLength=len(data),uri='data:application/octet-stream;base64,'+base64.b64encode(data).decode())],bufferViews=views,accessors=access)
    (ROOT/'traveler_prototype.gltf').write_text(json.dumps(g,separators=(',',':')))
def render(exploded=False):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from matplotlib.collections import PolyCollection
    if exploded:
        fig,axes=plt.subplots(2,3,figsize=(15,10),facecolor='#c8c9ca')
        selections=[('Helmet','H01-H03 / HELMET'),('Torso','T01-T03 / PRESSURE SUIT + ARMOR'),('Backpack','T02 / BACKPACK'),('ArmL','A01-A04 / ARM MODULE'),('LegR','L01-L05 / LEG MODULE'),('Weapon','W01-W03 / SPHERE')]
    else:
        fig,axes=plt.subplots(1,2,figsize=(10,8),facecolor='#c8c9ca');selections=[(None,'FRONT'),(None,'REAR')]
    axes=np.atleast_1d(axes).ravel()
    for index,(ax,(group,title)) in enumerate(zip(axes,selections)):
        yaw=-48 if exploded else [-20,155][index]
        a=math.radians(yaw);R=np.array([[math.cos(a),0,math.sin(a)],[0,1,0],[-math.sin(a),0,math.cos(a)]])
        polys=[];colors=[];zs=[]
        selected=[p for p in PARTS if group is None or p['group']==group]
        for p in selected:
            v=(p['v']+(p['explode'] if exploded else 0))@R.T;f=p['f'];tri=v[f];n=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-10)
            light=np.array([-.4,.65,.65]);light/=np.linalg.norm(light)
            shade=.11+.19*np.maximum(n@light,0)+.13*np.maximum(n@np.array([.8,.2,-.4]),0)
            shade+=np.array(MATS[p['mat']][1]).mean()*1.7
            polys.extend(tri[:,:,:2]);colors.extend(np.repeat(np.clip(shade,0,1)[:,None],3,axis=1));zs.extend(tri[:,:,2].mean(axis=1))
        order=np.argsort(zs);ax.add_collection(PolyCollection(np.array(polys)[order],facecolors=np.array(colors)[order],edgecolors='none'))
        allv=np.concatenate([p['v']+(p['explode'] if exploded else 0) for p in selected])@R.T
        ax.set_xlim(allv[:,0].min()-.15,allv[:,0].max()+.18);ax.set_ylim(allv[:,1].min()-.13,allv[:,1].max()+.15);ax.set_aspect('equal');ax.axis('off')
        ax.set_title(title,fontsize=11,color='#222222')
    fig.suptitle('TRAVELER / NOCTURNE V01'+(' / EXPLODED GEOMETRY' if exploded else ' / FRONT + REAR'),fontsize=16,color='#222222')
    fig.text(.5,.025,'Exact prototype triangles / metres / unrigged / not a Unity screenshot',ha='center',fontsize=9,color='#333333')
    fig.savefig(ROOT/('exploded_geometry.png' if exploded else 'prototype_geometry.png'),dpi=180,facecolor=fig.get_facecolor());plt.close(fig)
def validate():
    names=set();issues=[];tri=0;verts=0;closed=0
    for p in PARTS:
        assert p['name'] not in names;names.add(p['name']);v=p['v'];f=p['f'];tri+=len(f);verts+=len(v)
        assert np.isfinite(v).all() and len(f)>0 and f.max()<len(v)
        area=np.linalg.norm(np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]]),axis=1)
        assert area.min()>1e-12,(p['name'],'degenerate')
        edges={}
        for face in f:
            for a,b in zip(face,np.roll(face,-1)):
                e=tuple(sorted((int(a),int(b))));edges[e]=edges.get(e,0)+1
        assert all(n==2 for n in edges.values()),(p['name'],'nonmanifold')
        closed+=1
    bounds=np.concatenate([p['v'] for p in PARTS]);report=dict(status='PASS',parts=len(PARTS),vertices=verts,triangles=tri,closed_manifold_parts=closed,bounds_m=[bounds.min(axis=0).tolist(),bounds.max(axis=0).tolist()],checks=['unique part names','finite positions','valid triangle indices','nonzero triangle areas','each edge has exactly two incident faces','all materials achromatic'],not_tested=['Blender execution','FBX round trip','skin weights','animations','Unity rendering','Android performance'])
    assert all(max(c)-min(c)<1e-9 for _,c,_,_ in MATS)
    (ROOT/'validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if __name__=='__main__':
    build();validate();export_obj();export_obj(True);export_gltf()
    (ROOT/'traveler.mtl').write_text('\n'.join('newmtl %s\nKd %s\nKs .25 .25 .25\nNs %s\n'%(n,' '.join(map(str,c)),int(120*(1-r))) for n,c,m,r in MATS))
    registry=[dict(id=p['name'],group=p['group'],suggested_bone=p['bone'],material=MATS[p['mat']][0],vertices=len(p['v']),triangles=len(p['f']),explode_offset_m=p['explode'].tolist()) for p in PARTS]
    (ROOT/'parts.json').write_text(json.dumps(registry,indent=2));render();render(True)
