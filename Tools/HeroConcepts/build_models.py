"""Apply the nine archived concepts to real skinned geometry in Blender 4.5 LTS.

Existing anatomy, skeletons, animations and contact sockets are retained. New
costume geometry is built from named editable parts, never from the concept PNG.
The runtime payload's canonical weapon coordinates remain compatible with its
existing fitter; the editable Ione scene shows the actual forward-facing focus.
"""
from pathlib import Path
import copy,gzip,hashlib,json,math,subprocess,sys,tempfile
import numpy as np
import bpy,bmesh
from mathutils import Vector,Matrix

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/HeroParts'))
from build_parts import Parts,amend,validate,unit,PALETTES,obj_source
from blender_export import xyz,material,reset,mesh_object,skin,parent_name,studio,render,export_fbx,rig_signature,action_signature,runtime_scene

BASE='a605c0635eaccd57dc48e0e60a6a344170153a1e'
REVISION='nine-hero-concepts-20261012-v1'
ART=ROOT/'ArtSource/Heroes/ConceptModels'
RESOURCE=ROOT/'Assets/Alterverse/Resources'
KINDS={'Sivra':'CyberRanger','Ione':'UmbralMage','Nerok':'CyberShark','Auren':'SiegeTank','Traveler':'ChronoTraveler'}
NATIVE={'Zhaev':('Veyr','ArtSource/Heroes/Veyr.blend'),'Tahla':('Mira','ArtSource/Heroes/Mira.blend'),'Brakk':('Rook','ArtSource/Heroes/Rook.blend'),'Timmy':('Timmy','ArtSource/Heroes/AndromedaTimmy/AndromedaTimmy.blend')}
PALETTES['SiegeTank']=[(.03,.035,.045),(.12,.16,.065),(.38,.32,.19),(1,.32,.035),(.32,.34,.36),(1,.92,.025),(1,.85,.015)]
PALETTES['ChronoTraveler']=[(.028,.041,.044),(.062,.082,.10),(.30,.38,.42),(.02,.65,.94),(.007,.018,.016),(.025,.18,1)]
REPORT={'revision':REVISION,'sourceRevision':BASE,'models':{}}

def original(relative):
    return subprocess.run(['git','show',BASE+':'+relative],cwd=ROOT,check=True,capture_output=True).stdout

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def diamond(p,name,c,w,h,mat,bone,depth=.009):
    x,y,z=c;p.plate(name,[(x,y+h),(x+w,y),(x,y-h),(x-w,y)],z,depth,mat,bone)

def node(p,name,c,r,metal,light,bone):
    p.ellipsoid(name+' recessed housing',c,[r*1.17,r*1.17,.010],metal,bone,24,12)
    p.ring(name+' luminous ring',np.asarray(c)+[0,0,.012],r,.0027,light,bone,(0,0,1),28)
    p.ellipsoid(name+' lens',np.asarray(c)+[0,0,.009],[r*.65,r*.65,.006],light,bone,24,12)

def leaf(p,name,a,b,width,mat,bone,vein=None):
    a=np.array(a);b=np.array(b);mid=(a+b)*.5;axis=unit(b-a);across=unit(np.cross(axis,[0,0,1]))*width
    outline=[a,mid+across,b,mid-across]
    p.plate(name,[(v[0],v[1]) for v in outline],float(mid[2]),.004,mat,bone)
    if vein is not None:p.tube(name+' raised central vein',[a+[0,0,.008],b+[0,0,.008]],.0013,vein,bone,6)

def shoulder_lames(p,names,anchors,metal,accent,scale=1):
    for side,sign in [('L',-1),('R',1)]:
        bone=names.index('UpperArm'+side);head=np.array(anchors[bone]['head'])
        for layer in range(3):
            center=head+[sign*.026,-.018-layer*.032*scale,.006]
            radii=np.array([.091,.052,.094])*scale
            p.patch('three overlapping shoulder lames '+side+str(layer),center,radii,(.12,1.80),(-1.13,1.13),metal,bone,10,16)
            line=[center+radii*np.array([math.sin(1.78)*math.sin(a),math.cos(1.78),math.sin(1.78)*math.cos(a)])+[0,0,.002] for a in np.linspace(-1.10,1.10,22)]
            p.tube('shoulder inset edge '+side+str(layer),line,.0022*scale,accent,bone,8)
            for point in [line[2],line[-3]]:p.ellipsoid('shoulder recessed rivet',point,[.003,.003,.002],accent,bone,10,6)

def runtime_parts(label,data):
    p=Parts();names=data['boneNames'];idx=names.index;anchors=data['anchors'];v=np.asarray(data['vertices']).reshape(-1,3);bi=np.asarray(data['boneIndices']);groups=np.asarray(data.get('groups',[0]*len(v)))
    remove=np.zeros(len(v),bool)
    def front(x,y,bone,fallback=.15):
        points=v[(bi==idx(bone))&(groups==0)];near=points[(np.abs(points[:,0]-x)<.023)&(np.abs(points[:,1]-y)<.024)]
        return float(np.max(near[:,2])+.006) if len(near) else fallback
    chest=idx('Chest');hips=idx('Hips');head=idx('Head');cy=anchors[chest]['head'][1]
    if label in ('Sivra','Ione'):
        for side,sign in [('L',-1),('R',1)]:
            upper=idx('UpperArm'+side);fore=idx('Forearm'+side);thigh=idx('Thigh'+side);shin=idx('Shin'+side)
            h=np.array(anchors[fore]['head']);w=np.array(anchors[idx('Hand'+side)]['head'])
            for offset in [-.020,.020]:
                points=[h+(w-h)*t+[offset,0,.075] for t in np.linspace(.13,.83,13)]
                p.tube('twin forearm ceramic rails '+side,points,.004,2,fore,8)
                p.tube('forearm luminous inset '+side,[point+[0,0,.005] for point in points],.0016,3,fore,6)
            k=np.array(anchors[shin]['head']);k[2]=front(k[0],k[1],'Shin'+side,.084)
            diamond(p,'split knee diamond '+side,k+[0,-.018,.008],.024,.038,2,shin,.014)
            diamond(p,'knee light inlay '+side,k+[0,-.018,.020],.010,.020,3,shin,.004)
            t=np.array(anchors[thigh]['head']);c=(t+np.array(anchors[shin]['head']))*.5
            z=front(c[0],c[1],'Thigh'+side,.085)
            for dx in [-.029,.029]:
                p.plate('separate thigh armor leaf '+side,[(c[0]+dx-.012,c[1]+.104),(c[0]+dx+.016,c[1]+.075),(c[0]+dx+.014,c[1]-.072),(c[0]+dx-.010,c[1]-.094)],z,.011,2,thigh)
        if label=='Sivra':
            for sign in [-1,1]:
                center=[sign*.077,cy+.136,front(sign*.077,cy+.136,'Chest')]
                angles=np.linspace(.65,math.pi*1.77,35)
                line=[[center[0]+.060*math.cos(a),center[1]+.055*math.sin(a),center[2]-.013*math.sin(a)] for a in angles]
                p.tube('contoured breast pod titanium cradle',line,.005,2,chest,10)
                for j in range(3):
                    p.tube('chest machined vent',[[sign*.108,cy+.08+j*.011,center[2]],[sign*.132,cy+.085+j*.011,center[2]-.007]],.0016,4,chest,6)
            for sign in [-1,1]:
                p.tube('waist emerald routed seam',[[sign*.106,cy+.012,.123],[sign*.075,cy-.076,.094],[sign*.087,cy-.128,.083]],.0023,3,idx('Spine'),8)
            shoulder_lames(p,names,anchors,1,2,.65)
        else:
            centers=[[-.066,cy+.094,.18],[.066,cy+.094,.18],[0,cy+.184,.145]]
            for i,c in enumerate(centers):
                c[2]=front(c[0],c[1],'Chest',c[2]);node(p,'triangular chest resonance emitter '+str(i),c,.014,2,3,chest)
            for a,b in [(0,1),(1,2),(2,0)]:p.tube('chest triangular violet circuit',[np.array(centers[a])+[0,0,.004],np.array(centers[b])+[0,0,.004]],.0015,3,chest,6)
            shoulder_lames(p,names,anchors,2,3,1.00)
            for sign in [-1,1]:node(p,'hip orbit reactor',[sign*.151,anchors[hips]['head'][1]-.01,.125],.038,1,3,hips)
    elif label=='Auren':
        # Replace the old emissive letter glyph without disturbing shield metal.
        for face in np.asarray(data['submeshes'][5]['triangles']).reshape(-1,3):
            if (groups[face]==1).all():remove[face]=True
        shield=v[groups==1];center=np.median(shield,axis=0);center[2]=float(shield[:,2].max())+.006
        grip=idx('WeaponGripL');first=len(p.v)
        p.ellipsoid('segmented sunburst shield boss',center,[.046,.057,.010],2,grip,32,16)
        p.ring('shield sunburst inner ring',center+[0,0,.013],.036,.003,5,grip,(0,0,1),36)
        for a in np.linspace(0,math.tau,12,endpoint=False):
            start=center+np.array([.049*math.cos(a),.063*math.sin(a),0]);end=center+np.array([.074*math.cos(a),.092*math.sin(a),0])
            p.tube('twelve segmented sunburst rays',[start,end],.0033,5,grip,8)
        shield_end=len(p.v)
        for sign in [-1,1]:
            for row in range(5):
                y=cy+.175-row*.026;x=sign*.043;z=front(x,y,'Chest',.18)
                p.plate('amber chest vent rail',[(x-.009,y+.008),(x+.009,y+.008),(x+.009,y-.008),(x-.009,y-.008)],z,.005,3,chest)
            for side in ['L' if sign<0 else 'R']:
                shin=idx('Shin'+side);k=np.array(anchors[shin]['head']);f=np.array(anchors[idx('Foot'+side)]['head']);c=(k+f)*.5
                p.plate('scalloped bronze shin ridge',[(c[0]-.058,c[1]+.115),(c[0]+.058,c[1]+.115),(c[0]+.051,c[1]-.095),(c[0],c[1]-.126),(c[0]-.051,c[1]-.095)],.105,.017,2,shin)
                for dy in [-.055,0,.055]:p.tube('shin industrial machined joint',[[c[0]-.040,c[1]+dy,.13],[c[0]+.040,c[1]+dy,.13]],.003,4,shin,8)
        shoulder_lames(p,names,anchors,1,2,1.65)
        node(p,'abdomen amber reactor',[0,cy-.077,.166],.024,2,3,idx('Spine'))
        p.shield_range=(first,shield_end)
    elif label=='Traveler':
        hc=float(anchors[head]['head'][1])+.215
        y=hc+.024;z=front(0,y,'Head',.108)
        p.tube('single horizontal time slit',[[-.065,y,z],[.065,y,z]],.0023,3,head,10)
        for sign in [-1,1]:
            for j in range(7):
                a=-.67+j*.22;c=np.array([sign*.109,hc+.025,.0]);point=c+[sign*.005,.051*math.sin(a),.051*math.cos(a)]
                p.tube('temple clock tick',[point,point+[0,.004*math.cos(a),.004*math.sin(a)]],.0013,2,head,6)
        for bone,y,r in [(chest,cy+.121,.033),(idx('Spine'),cy-.081,.024)]:
            c=[0,y,front(0,y,names[bone],.145)];node(p,'compass disc',c,r,2,3,bone)
            for a in np.linspace(0,math.tau,8,endpoint=False):
                p.tube('compass radial scale',[[r*.85*math.cos(a),y+r*.85*math.sin(a),c[2]+.015],[r*.65*math.cos(a),y+r*.65*math.sin(a),c[2]+.015]],.0009,2,bone,5)
        for side,sign in [('L',-1),('R',1)]:
            thigh=idx('Thigh'+side);t=np.array(anchors[thigh]['head']);k=np.array(anchors[idx('Shin'+side)]['head']);c=t*.59+k*.41+[sign*.083,0,.025]
            p.plate('outer-thigh chronometer phone casing',[(c[0]-.024,c[1]+.056),(c[0]+.024,c[1]+.056),(c[0]+.024,c[1]-.056),(c[0]-.024,c[1]-.056)],c[2]+.033,.010,1,thigh)
            p.plate('blue chronometer screen',[(c[0]-.018,c[1]+.042),(c[0]+.018,c[1]+.042),(c[0]+.018,c[1]-.029),(c[0]-.018,c[1]-.029)],c[2]+.044,.004,3,thigh)
            for j in range(4):p.tube('display telemetry row',[[c[0]-.013,c[1]+.026-j*.012,c[2]+.048],[c[0]+.009,c[1]+.026-j*.012,c[2]+.048]],.0008,2,thigh,5)
            for j in range(3):
                points=[c+[sign*(.016+j*.009),-.045,.038],c+[sign*(.027+j*.009),-.071-j*.009,.031],c+[sign*(.014+j*.009),-.099-j*.014,.025],c+[sign*.011,-.132-j*.015,.023]]
                p.tube('three chronometer insulated cables '+side,points,.0022,0,thigh,8)
            knee=k+[0,-.018,.081];diamond(p,'chronometer knee chevron',knee,.028,.034,2,idx('Shin'+side))
        shoulder_lames(p,names,anchors,1,5,.69)
    else:
        # Retain the newly tailored garments and add their harbor-specific fittings.
        for sign in [-1,1]:
            for y in [1.18,1.275,1.37]:
                p.tube('brass jacket toggle loop',[[sign*.102,y,.211],[sign*.120,y,.210],[sign*.120,y-.015,.208],[sign*.102,y-.015,.210]],.0025,4,chest,8)
                p.tube('dark jacket toggle',[[sign*.108,y-.007,.216],[sign*.134,y-.006,.213]],.0037,0,chest,8)
        c=[-.186,1.424,.226]
        p.tube('harbor trident central stem',[np.array(c)+[0,-.038,0],np.array(c)+[0,.030,0]],.0028,4,chest,8)
        for dx in [-.018,0,.018]:
            p.tube('harbor three-prong insignia',[np.array(c)+[dx,-.002,0],np.array(c)+[dx,.025,0],np.array(c)+[dx,.029,.003]],.0026,4,chest,8)
        p.tube('harbor trident curved crossbar',[np.array(c)+[-.018,.002,0],np.array(c)+[0,-.010,0],np.array(c)+[.018,.002,0]],.0026,4,chest,8)
        for side,sign in [('L',-1),('R',1)]:
            fore=idx('Forearm'+side);a=np.array(anchors[fore]['head']);w=np.array(anchors[idx('Hand'+side)]['head']);axis=unit(w-a)
            for j,t in enumerate([.50,.63,.76]):
                center=a+(w-a)*t;p.ring('three leather cuff bands '+side,center,.099-.012*t,.006,0,fore,axis,32)
                p.ring('cuff brass stitch edge '+side,center+[0,-.006,.0],.101-.012*t,.0011,4,fore,axis,32)
    return p,remove

def forward_ione(data):
    d=copy.deepcopy(data);v=np.asarray(d['vertices']).reshape(-1,3);n=np.asarray(d['normals']).reshape(-1,3);g=np.array(d['groups'])==2
    source=np.array([.34977,.97991,.08252]);target=source*np.array([-1,1,1]);rotation=np.array([[-1,0,0],[0,0,-1],[0,1,0]])
    v[g]=target+(v[g]-source)@rotation.T;n[g]=n[g]@rotation.T
    d['boneIndices']=np.asarray(d['boneIndices']);d['boneIndices'][g]=d['boneNames'].index('WeaponGripL');d['boneIndices']=d['boneIndices'].tolist()
    for sub in d['submeshes']:
        faces=np.asarray(sub['triangles']).reshape(-1,3);selected=g[faces].all(1);faces[selected]=faces[selected][:,[0,2,1]];sub['triangles']=faces.ravel().tolist()
    for marker in d['markers']:marker['position']=(target+rotation@(np.array(marker['position'])-source)).tolist();marker['bone']='WeaponGripL'
    points={m['name']:np.array(m['position']) for m in d['markers']};axis=unit(points['StaffTop']-points['StaffGrip']);assert axis[2]>.97 and abs(axis[1])<.05,axis
    REPORT['models']['Ione']['forwardWeaponAxis']=axis.tolist();d['vertices']=v.ravel().tolist();d['normals']=n.ravel().tolist();return d

def runtime_source(label,kind,data):
    display=forward_ione(data) if label=='Ione' else copy.deepcopy(data)
    if kind in ('CyberRanger','UmbralMage','CyberShark'):body,arm=runtime_scene(kind,display)
    else:
        reset();mats=[material(label+' '+str(i),c,.60,.38,i==3 or kind=='SiegeTank' and i in (5,6) or kind=='ChronoTraveler' and i==5) for i,c in enumerate(PALETTES[kind])]
        faces=[];mi=[]
        for m,s in enumerate(display['submeshes']):f=np.asarray(s['triangles']).reshape(-1,3);faces.extend(f.tolist());mi.extend([m]*len(f))
        body=mesh_object('DF_Anatomy',[xyz(v) for v in np.asarray(display['vertices']).reshape(-1,3)],faces,mi,mats,[xyz(n) for n in np.asarray(display['normals']).reshape(-1,3)])
        ad=bpy.data.armatures.new('DF_Skeleton');arm=bpy.data.objects.new('DF_Skeleton',ad);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
        for name,a in zip(display['boneNames'],display['anchors']):b=ad.edit_bones.new(name);b.head=xyz(a['head']);b.tail=xyz(a['tail'])
        for name in display['boneNames']:
            parent=parent_name(name)
            if parent:ad.edit_bones[name].parent=ad.edit_bones[parent]
        bpy.ops.object.mode_set(mode='OBJECT');skin(body,arm,display['boneNames'],display['boneIndices'])
    if label=='Traveler':
        p=Parts();bone=display['boneNames'].index('HandR');c=np.array(display['anchors'][bone]['head'])+[.0,.06,.21]
        p.ellipsoid('floating cyan chrono orb',c,[.052,.052,.052],3,bone,32,18)
        for normal in [(1,.35,.3),(.3,1,.35),(.35,.3,1)]:p.ring('three tilted orbital metal rings',c,.076,.0033,2,bone,normal,44)
        obj=mesh_object('DF_WeaponR',[xyz(v) for v in p.v],p.f,p.m,list(body.data.materials),[xyz(n) for n in p.normals()]);skin(obj,arm,display['boneNames'],p.b)
    if label=='Sivra':
        # Keep the original rifle mesh and materials in the editable full-character source.
        existing_objects=set(bpy.data.objects)
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'Nyra.blend';path.write_bytes(original('ArtSource/Heroes/Nyra.blend'))
            with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.objects=['DF_WeaponR','DF_HandleContactR','DF_Muzzle']
            objects={o.name:o for o in dst.objects if o};weapon=objects['DF_WeaponR'];h=objects['DF_HandleContactR'].matrix_world.translation;m=objects['DF_Muzzle'].matrix_world.translation
            q=(m-h).rotation_difference(Vector((0,-1,0)));target=Vector(xyz(display['anchors'][display['boneNames'].index('HandR')]['head']))
            for vert in weapon.data.vertices:vert.co=q@(weapon.matrix_world@vert.co-h)+target
            weapon.matrix_world=Matrix.Identity(4);weapon.parent=None
            for modifier in list(weapon.modifiers):weapon.modifiers.remove(modifier)
            weapon.vertex_groups.clear();bpy.context.collection.objects.link(weapon);skin(weapon,arm,['HandR'],[0]*len(weapon.data.vertices))
            # Blender also appends dependency objects such as the old armature.
            # Remove them after transferring the rifle to this source skeleton.
            for o in list(bpy.data.objects):
                if o not in existing_objects and o!=weapon:bpy.data.objects.remove(o,do_unlink=True)
        REPORT['models'][label]['nativeRifleForwardAxis']=[0,0,1]
    body['concept_revision']=REVISION
    return body,arm

def native_parts(label,arm,body):
    names=[b.name for b in arm.data.bones];idx=names.index
    def game(point):x,y,z=point;return np.array([x,z,-y])
    anchors=[{'head':game(arm.matrix_world@b.head_local).tolist(),'tail':game(arm.matrix_world@b.tail_local).tolist()} for b in arm.data.bones]
    p=Parts();chest=idx('Chest');hips=idx('Hips');head=idx('Head');cy=anchors[chest]['head'][1];hc=anchors[head]['head'][1]+.235
    if label=='Zhaev':
        # A fitted crown ribbon follows the retained helmet's measured round surface.
        ys=np.linspace(hc-.025,hc+.20,27)
        points=[]
        for a in np.linspace(-1.0,1.45,36):points.append([0,hc+.166*math.cos(a),.011+.169*math.sin(a)])
        p.tube('single violet crown stripe',points,.0053,3,head,10)
        node(p,'central oval violet chest reactor',[0,cy+.128,.193],.026,2,3,chest)
        for sign in [-1,1]:
            for j in range(4):p.tube('short chest vent rail',[[sign*.035,cy+.105+j*.013,.182],[sign*.078,cy+.106+j*.013,.169]],.0022,2,chest,8)
            for j in range(3):
                u=idx('UpperArmL' if sign<0 else 'UpperArmR');a=np.array(anchors[u]['head']);x=a[0]+sign*.038;y=a[1]-.023-j*.026
                p.plate('three compact shoulder chevrons',[(x-sign*.065,y+.018),(x+sign*.020,y+.038),(x+sign*.071,y+.005),(x+sign*.020,y-.006)],.108,.010,3,u)
            c=[sign*.120,anchors[hips]['head'][1]-.056,.137];diamond(p,'violet hip tab',c,.040,.077,3,hips)
            # Existing cape remains; embroidered trim follows its back edges.
            line=[[sign*(.092+.032*t),1.35-.64*t,-.17-.04*t] for t in np.linspace(0,1,28)]
            p.tube('cape violet inner seam',line,.0025,3,idx('Spine'),7)
            for j in range(5):
                t=j/5;p.tube('cape branched circuit embroidery',[[sign*.095,1.26-j*.105,-.204],[sign*.069,1.23-j*.105,-.207],[sign*.05,1.18-j*.105,-.209]],.0012,2,idx('Spine'),6)
    elif label=='Tahla':
        # Three shoulder lames are already authored in the pinned source.
        for sign in [-1,1]:
            p.tube('open-face golden brow diadem',[[0,hc+.045,.144],[sign*.05,hc+.073,.130],[sign*.091,hc+.044,.103]],.004,2,head,10)
            for j in range(12):
                x=sign*(.038+j*.0052);top=hc+.109-j*.002
                points=[[x,top,-.039],[x+sign*.025,top-.100,-.107],[x+sign*.037,top-.256,-.111],[x+sign*.030,top-.414,-.068]]
                p.tube('flowing swept white hair strand',points,[.014,.011,.009,.0009],5,head,9)
            for j in range(3):
                start=[sign*.027,cy+.141-j*.023,.145];end=[sign*(.085+j*.014),cy+.114-j*.038,.137]
                leaf(p,'branching chest botanical filigree',start,end,.008,2,chest)
            p.tube('ivory tunic gold seam',[[sign*.103,cy+.148,.127],[sign*.076,cy-.07,.117],[sign*.091,cy-.17,.128]],.0026,2,idx('Spine'),8)
            for j in range(2):
                a=[sign*(.044+.075*j),1.008,.148];b=[sign*(.065+.09*j),.79,.151]
                leaf(p,'split ivory leaf hip mantle',a,b,.043,1,hips,2)
                leaf(p,'emerald leaf mantle inset',np.array(a)+[0,-.014,.008],np.array(b)+[0,.018,.008],.021,3,hips,2)
        node(p,'emerald diadem stone',[0,hc+.059,.148],.008,2,3,head)
        ring_center=np.array([0,hc+.045,-.04]);p.ring('thin golden leaf halo',ring_center,.191,.0034,2,head,(0,0,1),64)
        for a in np.linspace(.20,math.pi-.20,7):
            c=ring_center+[.191*math.cos(a),.191*math.sin(a),0];tip=c+[.018*math.cos(a),.035*math.sin(a),0]
            leaf(p,'emerald halo leaf',c,tip,.009,3,head,2)
    elif label=='Brakk':
        shoulder_lames(p,names,anchors,2,4,1.57)
        node(p,'warm industrial chest reactor',[0,cy+.145,.258],.031,4,3,chest)
        for sign in [-1,1]:
            p.plate('split copper chest flange',[(sign*.034,cy+.181),(sign*.17,cy+.209),(sign*.22,cy+.088),(sign*.166,cy+.010),(sign*.048,cy+.035)],.205,.017,2,chest)
            for j in range(10):
                x=sign*(.009+j*.0071);y=hc-.097+abs(x)*.39
                points=[[x,y,.158],[x*1.05,y-.027,.168],[x*.88,y-.070,.142]]
                p.tube('short grey beard clump',points,[.007,.006,.0015],5,head,7)
            ear=[sign*.139,hc+.010,.005];p.ellipsoid('bronze communications earcup',ear,[.025,.043,.039],4,head,28,14)
            p.ring('earcup amber inset',np.array(ear)+[sign*.022,0,0],.024,.0028,3,head,(sign,0,0),28)
            fore=idx('ForearmL' if sign<0 else 'ForearmR');a=np.array(anchors[fore]['head']);b=np.array(anchors[idx('HandL' if sign<0 else 'HandR')]['head'])
            for j in range(3):
                c=a+(b-a)*(.48+j*.13)+[0,0,.121];p.tube('gauntlet three-step dorsal ridge',[[c[0]-.065,c[1],c[2]],[c[0]+.065,c[1],c[2]]],.005,4,fore,10)
                p.tube('gauntlet amber channel',[[c[0]-.044,c[1]-.008,c[2]+.006],[c[0]+.044,c[1]-.008,c[2]+.006]],.0016,3,fore,7)
        p.tube('headset microphone arm',[[.154,hc+.017,.012],[.162,hc-.038,.088],[.081,hc-.053,.174]],.004,4,head,10)
        p.ellipsoid('headset microphone',[.073,hc-.056,.177],[.016,.008,.009],0,head,18,10)
    else:
        # Timmy's anatomy, tabby textures, clear visor and tail are kept intact.
        c=[-.053,1.17,.172];p.ellipsoid('cyan comet chest badge',c,[.014,.014,.004],3,chest,24,12)
        for dy in [-.008,0,.008]:p.tube('comet badge three tails',[[c[0]-.010,c[1]+dy,c[2]],[c[0]-.039,c[1]+dy-.016,c[2]]],.0021,3,chest,7)
        p.plate('chest telemetry casing',[(-.017,1.245),(.079,1.245),(.079,1.128),(-.017,1.128)],.177,.014,0,chest)
        p.plate('blue telemetry display',[(-.007,1.233),(.069,1.233),(.069,1.175),(-.007,1.175)],.191,.004,3,chest)
        for x in [.012,.051]:p.ellipsoid('telemetry two gold buttons',[x,1.15,.197],[.008,.007,.003],4,chest,16,8)
        p.ring('cyan domed visor gasket',[0,1.54,.119],.245,.0043,3,head,(0,0,1),64)
        for side,sign in [('L',-1),('R',1)]:
            fore=idx('Forearm'+side);a=np.array(anchors[fore]['head']);b=np.array(anchors[idx('Hand'+side)]['head'])
            p.tube('white forearm layered edge',[a+(b-a)*.21+[0,0,.068],a+(b-a)*.75+[0,0,.068]],.005,1,fore,9)
            c=np.array(anchors[idx('Shin'+side)]['head'])+[0,-.013,.104];diamond(p,'white knee enamel layer',c,.055,.039,1,idx('Shin'+side),.012)
            for dy in [-.034,0,.034]:p.tube('jetpack cyan exhaust grille',[[sign*.093,1.09+dy,-.252],[sign*.172,1.09+dy,-.252]],.004,3,chest,10)
    return p,names,anchors

def remove_native_obsolete(label,body):
    bm=bmesh.new();bm.from_mesh(body.data);deform=bm.verts.layers.deform.active;groups={g.index:g.name for g in body.vertex_groups};seen=set();remove=[];removed=[]
    for first in bm.verts:
        if first in seen:continue
        stack=[first];seen.add(first);component=[]
        while stack:
            v=stack.pop();component.append(v)
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other not in seen:seen.add(other);stack.append(other)
        mats={body.data.materials[f.material_index].name for v in component for f in v.link_faces};bound={groups.get(i,'?') for v in component for i,w in v[deform].items() if w>.5}
        world=[body.matrix_world@v.co for v in component];lo=min(v.z for v in world);hi=max(v.z for v in world)
        selected=label=='Zhaev' and bound=={'Head'} and len(component)<30 and hi>1.95
        selected|=label=='Brakk' and bound=={'Head'} and mats=={'Hair'} and len(component)!=30
        selected|=label=='Tahla' and 'Tahla pink flower' in mats
        if label in ('Zhaev','Brakk') and len(component)<1000 and len(bound)==1 and next(iter(bound)).startswith('UpperArm') and lo>1.39 and hi>1.50 and not any('fabric' in m.lower() or m=='Skin' for m in mats):selected=True
        if selected:remove.extend(component);removed.append({'vertices':len(component),'materials':sorted(mats),'bones':sorted(bound)})
    gone=set(remove);preserved=[tuple(v.co) for v in bm.verts if v not in gone];bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(body.data);bm.free();assert [tuple(v.co) for v in body.data.vertices]==preserved
    return preserved,removed

def native_source(label,hero,source):
    with tempfile.TemporaryDirectory() as tmp:
        path=Path(tmp)/'original.blend';path.write_bytes(original(source));bpy.ops.wm.open_mainfile(filepath=str(path))
    bpy.context.preferences.filepaths.save_version=0;arm=bpy.data.objects['DF_Skeleton'];body=bpy.data.objects['DF_Anatomy'];before=rig_signature(arm);actions=action_signature()
    sockets={o.name:list(o.matrix_world.translation) for o in bpy.data.objects if o.name.startswith(('DF_Palm','DF_Handle','DF_Muzzle','DF_Support','DF_TimmyJet'))}
    preserved,removed=remove_native_obsolete(label,body);p,names,anchors=native_parts(label,arm,body)
    colors=[(.025,.035,.051),(.91,.91,.88),(.57,.26,.07),(.68,.08,1),(.47,.32,.14),(.19,.21,.22)]
    if label=='Tahla':colors=[(.02,.10,.06),(.92,.87,.72),(.65,.39,.10),(.04,.38,.16),(.48,.31,.13),(.97,.97,.96)]
    if label=='Brakk':colors=[(.025,.032,.050),(.30,.34,.36),(.54,.20,.07),(1,.38,.05),(.46,.32,.16),(.26,.27,.25)]
    if label=='Timmy':colors=[(.025,.039,.062),(.87,.90,.91),(.37,.39,.42),(.025,.61,.95),(.73,.46,.10),(.93,.92,.87)]
    mats=[material(label+' concept surface '+str(i),color,.58 if i in (2,4) else .08,.38,i==3) for i,color in enumerate(colors)]
    detail=mesh_object('DF_ConceptDetails',[xyz(v) for v in p.v],p.f,p.m,mats,[xyz(n) for n in p.normals()]);skin(detail,arm,names,p.b);detail.matrix_parent_inverse=arm.matrix_world.inverted()
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);detail.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
    assert [tuple(v.co) for v in body.data.vertices[:len(preserved)]]==preserved
    assert rig_signature(arm)==before and action_signature()==actions
    for name,pos in sockets.items():assert np.allclose(bpy.data.objects[name].matrix_world.translation,pos,atol=1e-7),name
    body['concept_revision']=REVISION;obj_source(ART/(label+'-new-parts.obj'),p,[f'{label}_surface_{i}' for i in range(len(colors))],colors)
    REPORT['models'][label]={'source':source,'shippingModel':f'Assets/Alterverse/Resources/AuthoredHeroes/{hero}.fbx','removedComponents':removed,'preservedVertices':len(preserved),'preservedPositionsExact':True,'rigExact':True,'originalActionsExact':list(actions),'socketsExact':True,'newVertices':len(p.v),'newTriangles':len(p.f),'newParts':p.parts}
    export_fbx(RESOURCE/'AuthoredHeroes'/(hero+'.fbx'),arm,bool(actions));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/source),compress=True)
    for o in bpy.data.objects:
        if o.type in ('LIGHT','CAMERA'):o.hide_render=True
        if o.name.startswith('DF_'):o.hide_render=False
    arm.data.pose_position='REST';studio();return body,arm

def previews(label,arm):
    if bpy.context.scene.camera is None:studio()
    scene=bpy.context.scene;scene.cycles.samples=12;scene.render.resolution_x=660;scene.render.resolution_y=880
    arm.data.pose_position='REST'
    bpy.ops.wm.save_as_mainfile(filepath=str(ART/(label+'-model.blend')),compress=True)
    if label in KINDS:export_fbx(ART/(label+'-model.fbx'),arm)
    render(ART/(label+'-front.png'),(0,0,1.00),2.4 if label not in ('Nerok','Auren','Brakk') else 2.75,24)
    render(ART/(label+'-back.png'),(0,0,1.00),2.4 if label not in ('Nerok','Auren','Brakk') else 2.75,158)

def validate_exports():
    for label,entry in REPORT['models'].items():
        path=ROOT/entry['shippingModel'] if label in NATIVE else ART/(label+'-model.fbx')
        reset();bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
        arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE'];assert len(arms)==1,(label,len(arms))
        arm=arms[0];names={b.name for b in arm.data.bones};body=bpy.data.objects.get('DF_Anatomy');assert body and {'Hips','Spine','Chest','Head','HandL','HandR'}<=names
        assert any(m.type=='ARMATURE' and m.object==arm for m in body.modifiers)
        for v in body.data.vertices:
            weights=[g.weight for g in v.groups if body.vertex_groups[g.group].name in names];assert weights and abs(sum(weights)-1)<.002,(label,v.index,weights)
        originals=entry.get('originalActionsExact',[]);imported=[a.name for a in bpy.data.actions];assert all(any(name in a for a in imported) for name in originals),(label,originals,imported)
        if label=='Timmy':assert len(names)==20 and 'Tail' in names
        entry['fbxRoundTrip']={'bones':len(names),'skinningImported':True,'normalizedWeights':True,'animationClipsImported':imported,'vertices':len(body.data.vertices)}
        entry['modelSha256']=sha(path);entry['blenderSha256']=sha(ART/(label+'-model.blend'));print('FBX_VALID',label,flush=True)

def finish_provenance():
    for label,entry in REPORT['models'].items():
        path=ROOT/'ArtSource/Heroes/Concepts/Provenance'/(label+'.json');doc=json.loads(path.read_text())
        doc['implementation'].update(fullConceptModel='modeled and Blender/FBX validated; Unity not run',revision=REVISION,validationReport='ArtSource/Heroes/ConceptModels/validation-report.json',nativeSource=f'ArtSource/Heroes/ConceptModels/{label}-model.blend',model=entry.get('shippingModel',f'ArtSource/Heroes/ConceptModels/{label}-model.fbx'),modelSha256=entry['modelSha256'])
        doc['openEvidence']=[x for x in doc['openEvidence'] if x!='full new-concept model and render/FBX validation'];doc['openEvidence'].append('Unity editor and device visual verification')
        path.write_text(json.dumps(doc,indent=2)+'\n')
    marker=RESOURCE/'HeroConcepts/StyleVersion.txt';marker.parent.mkdir(exist_ok=True);marker.write_text(REVISION+'\n')
    marker.with_suffix('.txt.meta').write_text('fileFormatVersion: 2\nguid: 3510139f36ec4617b71ed7a9c5d2d643\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    (RESOURCE/'HeroConcepts.meta').write_text('fileFormatVersion: 2\nguid: 1b74e762d7324a2a9c9c8a52705dfaa5\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    status=ROOT/'Docs/HeroProvenanceStatus.md';text=status.read_text();text=text.replace('Die vollständige Gestaltung aller neun 3D-Modelle nach diesen Konzepten ist noch nicht abgeschlossen.','Alle neun 3D-Modelle wurden nach den archivierten Konzepten ergänzt bzw. umgestaltet und mit Blender/FBX geprüft. Die renderbare Geometrie behält die ausdrücklich erlaubten vorhandenen Grundformen. Unity und Android wurden nicht geprüft.')
    for label in REPORT['models']:
        lines=text.splitlines();text='\n'.join(line[:-8]+' geprüft |' if f'Provenance/{label}.json)' in line and line.endswith(' offen |') else line for line in lines)+'\n'
    status.write_text(text)

def main():
    ART.mkdir(parents=True,exist_ok=True)
    for label,kind in KINDS.items():
        data=json.loads(gzip.decompress(original(f'Assets/Alterverse/Resources/{kind}/Model.bytes')));p,remove=runtime_parts(label,data);changed,checks=amend(data,remove,p)
        if label=='Auren':
            a,b=p.shield_range;first=checks['preservedVertices'];changed['groups'][first+a:first+b]=[1]*(b-a)
        if label=='Nerok':changed['clothingVertices']=sorted(set(changed['clothingVertices'])|set(range(checks['preservedVertices'],len(changed['boneIndices']))))
        changed['designRevision']=REVISION
        if label=='Traveler':changed['legEquipmentAuthored']=True
        validate(changed);destination=RESOURCE/kind/'Model.bytes';destination.write_bytes(gzip.compress(json.dumps(changed,separators=(',',':')).encode(),mtime=0))
        checks.update(shippingModel=destination.relative_to(ROOT).as_posix(),newParts=p.parts,canonicalWeaponMarkersExact=True);REPORT['models'][label]=checks
        obj_source(ART/(label+'-new-parts.obj'),p,[f'{label}_surface_{i}' for i in range(len(PALETTES[kind]))],PALETTES[kind])
        body,arm=runtime_source(label,kind,changed);previews(label,arm);print('MODELED',label,flush=True)
    for label,(hero,source) in NATIVE.items():body,arm=native_source(label,hero,source);previews(label,arm);print('MODELED',label,flush=True)
    validate_exports();REPORT['validation']={'runtimePayload':'passed','nativeFbxImport':'passed','rigsAndSourceActions':'preserved','UnityEditor':'not run','Android':'not run'}
    (ART/'validation-report.json').write_text(json.dumps(REPORT,indent=2)+'\n');finish_provenance();print('NINE_HEROES_COMPLETE',flush=True)

if __name__=='__main__':main()
