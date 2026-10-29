"""Build the 2026-10-29 V3 armor concept on the existing animated hero.

Blender 5.0.1. Rebuilds armor only; retains the current sword, hands, undersuit,
rest skeleton, every original action and the existing five runtime materials.
Run: blender -b -t 4 --python-exit-code 1 --python Armor-V3/build_armor_v3.py
The reinforced V3 concept is geometry revision 8: fuller thighs and machined armor.
"""
from pathlib import Path
import bpy, bmesh, math, json, hashlib
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
SOURCE=HERE.parent/'VoidKnight.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
rig.animation_data.action=None
bpy.context.view_layer.update()
black,edge,cloth,energy,core=[bpy.data.materials[n] for n in
    ['Void black metal','Void bevel metal','Void woven undersuit','Void energy','Void core energy']]

def protected(o):
    return o.type=='MESH' and (o.get('bone') in {'WeaponGripR','HandL','HandR'} or
        o.name.startswith(('VK_Undersuit','VK_Joint','VK_Neck','VK_Palm','VK_Finger','VK_Thumb')))

def geometry_digest(o):
    record=([tuple(v.co) for v in o.data.vertices],
        [(tuple(p.vertices),p.material_index) for p in o.data.polygons],
        [s.material.name for s in o.material_slots],
        [[(o.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in o.data.vertices],
        [[tuple(u.uv) for u in uv.data] for uv in o.data.uv_layers])
    return hashlib.sha256(repr(record).encode()).hexdigest()

def actions_digest():
    return {a.name:[(f.data_path,f.array_index,
        [(tuple(k.co),tuple(k.handle_left),tuple(k.handle_right),k.interpolation,k.easing)
            for k in f.keyframe_points])
        for layer in a.layers for strip in layer.strips for slot in a.slots
        for f in (strip.channelbag(slot).fcurves if strip.channelbag(slot) else [])]
        for a in bpy.data.actions}

before={o.name:geometry_digest(o) for o in scene.objects if protected(o)}
before_actions=actions_digest()
before_bones={b.name:(tuple(b.head_local),tuple(b.tail_local),tuple(x for r in b.matrix_local for x in r),b.parent.name if b.parent else None) for b in rig.data.bones}
replaced_count=sum(o.type=='MESH' and not protected(o) for o in scene.objects)
for o in list(scene.objects):
    if o.type=='MESH' and not protected(o):bpy.data.objects.remove(o,do_unlink=True)
for data in list(bpy.data.meshes):
    if data.users==0:bpy.data.meshes.remove(data)

created=[]
detail_batches={}
surface_cache={}

def seated(point,normal,bone,module,assembly):
    """Place hardware on the actual finished shell, including its raised facets."""
    p=Vector(point);n=Vector(normal).normalized()
    candidates=[o for o in created if o.get('bone')==bone and o.get('assembly')==assembly
        and o.get('v3_module')==module and any(s.material==black for s in o.material_slots)]
    if not candidates:
        candidates=[o for o in created if o.get('bone')==bone and o.get('assembly')==assembly
            and any(s.material==black for s in o.material_slots)]
    if not candidates:return p,n
    key=(bone,module,assembly,len(candidates))
    if key not in surface_cache:
        verts=[];polys=[]
        for o in candidates:
            offset=len(verts);verts.extend(v.co.copy() for v in o.data.vertices)
            polys.extend(tuple(offset+i for i in f.vertices) for f in o.data.polygons)
        surface_cache[key]=BVHTree.FromPolygons(verts,polys)
    hit,hit_normal,_,distance=surface_cache[key].ray_cast(p+n*.45,-n,.9)
    if hit is None:return p,n
    return hit+n*.00065,hit_normal if hit_normal.dot(n)>0 else -hit_normal

def mesh(name,verts,faces,mat,bone,module,assembly,bevel=0,frame=None):
    name=name if name.startswith('VK_Eye_') else 'VK8_'+name
    data=bpy.data.meshes.new(name)
    data.from_pydata([tuple(frame(v)) if frame else tuple(v) for v in verts],[],faces)
    data.update()
    bm=bmesh.new();bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o)
    o.parent=rig;o['bone']=bone;o['design_version']=8;o['blueprint_version']=3
    o['v3_module']=module;o['assembly']=assembly
    data.materials.append(mat)
    if bevel:
        if mat==black:data.materials.append(edge)
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        m=o.modifiers.new('Machined plate chamfer','BEVEL');m.width=bevel;m.segments=1
        m.limit_method='ANGLE';m.angle_limit=math.radians(50)
        m.material=1 if mat==black else -1
        bpy.ops.object.modifier_apply(modifier=m.name)
        # Broad facets remain planar. Smooth averaging across a raised cap can
        # turn a metal panel into a soft cushion and erase the intended machining.
        for p in data.polygons:p.use_smooth=False
    uv=data.uv_layers.new(name='SurfaceUV')
    for p in data.polygons:
        dominant=max(range(3),key=lambda i:abs(p.normal[i]))
        axes=[i for i in range(3) if i!=dominant]
        for j in p.loop_indices:
            co=data.vertices[data.loops[j].vertex_index].co
            uv.data[j].uv=(co[axes[0]]*4,co[axes[1]]*4)
    vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(data.vertices))),1,'REPLACE')
    m=o.modifiers.new('Preserved animation rig','ARMATURE');m.object=rig
    created.append(o)
    return o

def plate(name,outline,front,depth,mat,bone,module,assembly,bevel=.0012,frame=None,ridge=0):
    n=len(outline)
    ys=list(front) if isinstance(front,(tuple,list)) else [front]*n
    if isinstance(front,(tuple,list)):
        # Resolve a broad panel into one machined plane. Arbitrary corner
        # depths would otherwise create warped triangular reflections.
        basis=np.array([(1,x,z) for x,z in outline],dtype=float)
        fitted=basis @ np.linalg.lstsq(basis,np.array(ys),rcond=None)[0]
        ys=fitted.tolist()
    verts=[(x,y,z) for (x,z),y in zip(outline,ys)]
    verts += [(x,y+depth,z) for (x,z),y in zip(outline,ys)]
    faces=[tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    if ridge:
        # A shallow center facet supplies a restrained crease, not a pyramid.
        crest=sum(ys)/n-max(-.0025,min(.0025,ridge))
        verts.append((sum(x for x,z in outline)/n,crest,sum(z for x,z in outline)/n))
        faces += [(i,(i+1)%n,2*n) for i in range(n)]
    else:faces.append(tuple(range(n)))
    # A second closed, seated island supplies the inset machining layer. It is
    # editable with Select Linked while keeping source assembly counts stable.
    inset_panels=('Pectoral','ShoulderLame','ThighPlate','FaceShell','BrowWing',
        'Scapular','ShinShell','ForearmShell','HipMain','KneeShell')
    if mat==black and any(token in name for token in inset_panels):
        cx=sum(x for x,z in outline)/n;cz=sum(z for x,z in outline)/n
        scale=.84;sign=1 if depth>0 else -1
        inner=[(cx+(x-cx)*scale,cz+(z-cz)*scale) for x,z in outline]
        plane=np.linalg.lstsq(np.array([(1,x,z) for x,z in outline]),np.array(ys),rcond=None)[0]
        iy=[float(np.dot((1,x,z),plane))-sign*.0038 for x,z in inner]
        start=len(verts)
        verts += [(x,y,z) for (x,z),y in zip(inner,iy)]
        verts += [(x,y+sign*.0025,z) for (x,z),y in zip(inner,iy)]
        faces += [tuple(start+i for i in range(n)),tuple(start+n+i for i in range(n))]
        faces += [(start+i,start+(i+1)%n,start+(i+1)%n+n,start+i+n) for i in range(n)]
    return mesh(name,verts,faces,mat,bone,module,assembly,bevel,frame)

def shell(name,rings,mat,bone,module,assembly,sides=16,frame=None):
    verts=[(cx+rx*math.cos(i*math.tau/sides),cy+ry*math.sin(i*math.tau/sides),z)
        for z,rx,ry,cx,cy in rings for i in range(sides)]
    faces=[tuple(range(sides-1,-1,-1)),tuple(range((len(rings)-1)*sides,len(rings)*sides))]
    faces += [(j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i)
        for j in range(len(rings)-1) for i in range(sides)]
    return mesh(name,verts,faces,mat,bone,module,assembly,.0008,frame)

def frame_for(bone_name):
    b=rig.data.bones[bone_name];head=b.head_local.copy();d=(b.tail_local-head).normalized()
    u=Vector((1,0,0));u=(u-d*u.dot(d)).normalized();v=u.cross(d).normalized()
    return lambda p:head+u*p[0]+v*p[1]+d*p[2],(b.tail_local-head).length

def batch(verts,faces,mat,bone,module,assembly):
    key=(mat.name,bone,module,assembly)
    target=detail_batches.setdefault(key,[[],[]]);offset=len(target[0])
    target[0].extend(tuple(v) for v in verts)
    target[1].extend(tuple(offset+i for i in f) for f in faces)

def strip(name,points,width,mat,bone,module,assembly,frame=None,depth=.001,pool=False):
    if mat in [energy,core] and module!='H09':
        # Follow every facet of a segmented shoulder instead of bridging the
        # surface with one long triangle that disappears under the next plate.
        dense=[]
        for a,b in zip(points,points[1:]):
            dense.extend(Vector(a).lerp(Vector(b),j/6) for j in range(6))
        points=dense+[Vector(points[-1])]
    front=[]
    for i,p in enumerate(points):
        p=Vector(p);delta=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])
        perpendicular=Vector((delta.z,0,-delta.x)).normalized()*width*.5
        front += [p+perpendicular,p-perpendicular]
    n=len(front);verts=front+[p+Vector((0,depth,0)) for p in front]
    faces=[]
    for i in range(len(points)-1):
        a=2*i;b=a+2
        faces += [(a,b,b+1,a+1),(n+a+1,n+b+1,n+b,n+a),(a,n+a,n+b,b),(a+1,b+1,n+b+1,n+a+1)]
    faces += [(0,1,n+1,n),(n-2,2*n-2,2*n-1,n-1)]
    project=pool or (mat in [energy,core] and module!='H09' and 'ShoulderChevron' not in name)
    if project:
        normal=Vector((0,-1 if depth>=0 else 1,0))
        if frame:normal=Vector(frame(normal))-Vector(frame((0,0,0)))
        extra=.0014 if mat in [energy,core] else 0
        placed=[seated(frame(p) if frame else p,normal,bone,module,assembly)[0]+normal.normalized()*extra for p in front]
        # Retain real inset thickness after projecting the visible surface.
        vertices=placed+[p-normal.normalized()*abs(depth) for p in placed]
        if pool:batch(vertices,faces,mat,bone,module,assembly)
        else:return mesh(name,vertices,faces,mat,bone,module,assembly)
    else:return mesh(name,verts,faces,mat,bone,module,assembly,0,frame)

def bolt(center,normal,bone,module,assembly,r=.003):
    c,n=seated(center,normal,bone,module,assembly)
    u=n.cross(Vector((0,0,1)))
    if u.length<.1:u=n.cross(Vector((0,1,0)))
    u.normalize();v=n.cross(u).normalized();sides=8
    verts=[c+u*(radius*math.cos(i*math.tau/sides))+v*(radius*math.sin(i*math.tau/sides))
        for radius in [r,r*.53] for i in range(sides)]
    faces=[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    batch(verts,faces,edge,bone,module,assembly)
    center_back=c-n*.0007
    disk=[center_back+u*(r*.52*math.cos(i*math.tau/sides))+v*(r*.52*math.sin(i*math.tau/sides)) for i in range(sides)]
    batch(disk,[tuple(range(sides))],cloth,bone,module,assembly)

def glyph(x,y,z,bone,module,assembly,sg=1,frame=None,size=1):
    for path in [[(0,0),(.009,.008),(.009,.015)],[(.014,.001),(.019,.006),(.014,.011)]]:
        strip('Etching',[(x+sg*a*size,y,z+b*size) for a,b in path],.0010*size,cloth,bone,module,assembly,frame,depth=-.00025 if y>0 else .00025,pool=True)

# H01/H02: fitted inner dome and genuine segmented shell around the original head.
shell('H02_InnerCranialShell',[(1.612,.079,.087,0,.007),(1.675,.125,.123,0,.008),
    (1.756,.139,.138,0,.009),(1.822,.124,.121,0,.010),(1.871,.078,.086,0,.012),
    (1.891,.020,.031,0,.012)],black,'Head','H02','Helmet',20)

# Swept crown tiles. Their sidewalls are modeled, not floating decorative lines.
for i,(yf,yb,zf,zb,width) in enumerate([(-.107,-.025,1.832,1.897,.049),(-.029,.055,1.897,1.886,.046),(.050,.121,1.886,1.812,.041)]):
    verts=[(-width,yf,zf),(0,yf-.004,zf+.011),(width,yf,zf),
        (width*.88,yb,zb),(0,yb+.003,zb+.012),(-width*.88,yb,zb)]
    mesh('H03_CrownTile_'+str(i),verts+[(x,y,z-.008) for x,y,z in verts],
        [tuple(range(6)),tuple(range(6,12))]+[(j,(j+1)%6,(j+1)%6+6,j+6) for j in range(6)],black,'Head','H03','Helmet',.0011)
    for sg in [-1,1]:bolt((sg*width*.72,yf+.012,zf+.005),(0,-.2,1),'Head','H03','Helmet',.0023)

plate('H04_CentralBrowKeel',[(-.019,1.794),(-.024,1.852),(0,1.877),(.024,1.852),(.019,1.794),(0,1.749)],
    [-.188,-.143,-.110,-.143,-.188,-.224],.009,black,'Head','H04','Helmet',.0012,ridge=.006)
strip('H04_CrownSignal',[(0,-.126,1.865),(0,-.161,1.835),(0,-.183,1.813)],.0030,energy,'Head','H04','Helmet')

for sg,label in [(-1,'L'),(1,'R')]:
    sx=lambda points,s=sg:[(s*x,z) for x,z in points]
    plate('H03_CrownFlank_'+label,sx([(.034,1.790),(.046,1.860),(.082,1.864),(.118,1.824),(.133,1.786),(.102,1.763)]),
        [-.174,-.102,-.091,-.079,-.076,-.152],.011,black,'Head','H03','Helmet',.0011,ridge=.004)
    plate('H04_BrowWing_'+label,sx([(.023,1.782),(.036,1.837),(.089,1.836),(.126,1.793),(.118,1.775),(.070,1.752),(.008,1.731)]),
        [-.204,-.160,-.132,-.110,-.151,-.204,-.230],.009,black,'Head','H04','Helmet',.0010,ridge=.003)
    points=[(sg*.125,-.153,1.772),(sg*.074,-.204,1.746),(sg*.002,-.234,1.721)]
    strip('H08_VisorGasket_'+label,[(x,y+.003,z) for x,y,z in points],.020,cloth,'Head','H08','Helmet',depth=.004)
    strip('VK_Eye_'+label,points,.0095,energy,'Head','H09','Helmet',depth=.0028)
    strip('H09_LensLip_'+label,[(x,y-.0007,z+.0028) for x,y,z in points],.0016,core,'Head','H09','Helmet',depth=.0005)
    plate('H08_FaceShell_'+label,sx([(.008,1.706),(.076,1.729),(.126,1.750),(.135,1.701),(.102,1.637),(.028,1.594),(.008,1.622)]),
        [-.232,-.208,-.156,-.139,-.157,-.218,-.235],.012,black,'Head','H08','Helmet',.0013,ridge=.002)
    strip('H08_FaceRecess_'+label,[(sg*.021,-.239,1.691),(sg*.032,-.229,1.648),(sg*.057,-.210,1.624)],.0023,cloth,'Head','H08','Helmet',pool=True)
    glyph(sg*.066,-.208,1.681,'Head','H08','Helmet',sg,size=.65)
    bolt((sg*.103,-.175,1.713),(sg*.4,-1,0),'Head','H08','Helmet',.0024)
    for i in range(3):
        z=1.687-i*.030
        plate('H07_CheekLame_'+label+'_'+str(i),sx([(.082,z+.031),(.121,z+.041),(.147,z+.015),(.137,z-.022),(.105,z-.037),(.071,z-.008)]),
            [-.178,-.125,-.035,-.026,-.112,-.193],.008,black,'Head','H07','Helmet',.0010,ridge=.002)
        bolt((sg*.135,-.063,z+.013),(sg*.7,-.5,0),'Head','H07','Helmet',.0022)
    # The ring lies in the temple plane and includes a recessed service center.
    temple=lambda p,s=sg:Vector((s*(.142-p[1]),p[0]+.008,p[2]+1.750))
    circle=lambda r,n=16:[(r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n)) for i in range(n)]
    plate('H06_TempleChassis_'+label,circle(.041,12),-.009,.014,black,'Head','H06','Helmet',.0011,temple)
    plate('H06_ServiceRing_'+label,circle(.033,16),-.017,.004,edge,'Head','H06','Helmet',.0006,temple)
    plate('H06_ServiceRecess_'+label,circle(.027,16),-.020,.004,black,'Head','H06','Helmet',.0007,temple)
    plate('H06_ServiceHub_'+label,circle(.018,12),-.023,.003,black,'Head','H06','Helmet',.0006,temple)
    for a in [math.pi*.2,math.pi*.9,math.pi*1.55]:
        bolt((sg*.166,.008+.035*math.cos(a),1.750+.035*math.sin(a)),(sg,0,0),'Head','H06','Helmet',.0024)
    strip('H06_TempleIndicator_'+label,[(-.006,-.024,0),(.006,-.024,0)],.0014,energy,'Head','H06','Helmet',temple,depth=.0007)
    plate('H05_RearShell_'+label,sx([(.009,1.867),(.079,1.861),(.125,1.800),(.119,1.709),(.084,1.661),(.014,1.675)]),
        [.103,.096,.093,.119,.132,.139],-.010,black,'Head','H05','Helmet',.0012,ridge=-.004)
    side_frame=lambda p,s=sg:Vector((s*(.124-p[1]),p[0],p[2]))
    plate('H05_UpperTempleCover_'+label,[(-.027,1.810),(-.011,1.856),(.043,1.854),(.104,1.807),(.105,1.774),(.061,1.764),(.009,1.786)],
        [-.006,.015,.007,-.003,-.009,-.015,-.012],.008,black,'Head','H05','Helmet',.0010,side_frame,ridge=.002)
    for path in [[(-.019,1.812),(.004,1.832),(.047,1.831)],[(.007,1.805),(.056,1.792),(.085,1.798)]]:
        strip('H05_TempleRecess',[(x,-.018,z) for x,z in path],.0013,cloth,'Head','H05','Helmet',side_frame,pool=True)
    strip('H05_RearRecess_'+label,[(sg*.023,.144,1.826),(sg*.075,.141,1.795),(sg*.095,.136,1.727)],.0021,cloth,'Head','H05','Helmet',depth=-.001,pool=True)
    for i in range(3):
        strip('H11_UndersideVent',[(sg*.066,-.200,1.627+i*.009),(sg*.089,-.180,1.643+i*.009)],.0026,cloth,'Head','H11','Helmet',pool=True)

plate('H11_ChinGuard',[(-.034,1.632),(-.020,1.653),(.020,1.653),(.034,1.632),(.017,1.590),(0,1.580),(-.017,1.590)],
    [-.220,-.225,-.225,-.220,-.224,-.225,-.224],.010,black,'Head','H11','Helmet',.0010,ridge=.003)
strip('H09_CentralVisorSlit',[(0,-.237,1.724),(0,-.239,1.675),(0,-.235,1.617)],.0045,energy,'Head','H09','Helmet',depth=.002)
strip('H10_RearLightCarrier',[(0,.147,1.841),(0,.151,1.779),(0,.148,1.727)],.0040,energy,'Head','H10','Helmet',depth=-.002)
for i in range(3):
    z=1.681-i*.032;w=.106-i*.012
    plate('H12_NapeLame_'+str(i),[(-w,z+.026),(-w*.82,z+.035),(w*.82,z+.035),(w,z+.026),(w*.80,z-.012),(-w*.80,z-.012)],
        [.104,.143,.143,.104,.148,.148],-.008,black,'Head','H12','Helmet',.0011,ridge=-.003)

# A02/A03/A04: layered front, shoulder bridges, scapulae and protected core.
shell('A03_ThoracicCarrier',[(1.235,.174,.127,0,0),(1.392,.231,.143,0,0),(1.511,.192,.116,0,0)],black,'Chest','A03','Cuirass',16)
for sg,label in [(-1,'L'),(1,'R')]:
    sx=lambda points,s=sg:[(s*x,z) for x,z in points]
    plate('A02_Gorget_'+label,sx([(.015,1.545),(.078,1.568),(.145,1.537),(.175,1.514),(.127,1.492),(.042,1.505)]),
        [-.106,-.092,-.082,-.073,-.132,-.148],.012,black,'Chest','A02','Cuirass',.0012,ridge=.003)
    plate('A03_Clavicle_'+label,sx([(.040,1.513),(.143,1.527),(.218,1.496),(.211,1.466),(.112,1.467),(.029,1.483)]),
        [-.163,-.139,-.082,-.116,-.194,-.201],.012,black,'Chest','A03','Cuirass',.0011,ridge=.005)
    plate('A03_Pectoral_'+label,sx([(.021,1.470),(.111,1.476),(.205,1.452),(.224,1.405),(.192,1.351),(.091,1.329),(.034,1.355),(.049,1.400)]),
        [-.204,-.190,-.131,-.117,-.160,-.204,-.222,-.225],.016,black,'Chest','A03','Cuirass',.0015,ridge=.008)
    for i in range(2):
        z=1.343-i*.047
        plate('A03_CostalLame_'+label+'_'+str(i),sx([(.055,z+.031),(.176,z+.049),(.206,z+.023),(.169,z-.027),(.060,z-.039),(.033,z-.010)]),
            [-.225,-.159,-.112,-.160,-.222,-.231],.011,black,'Chest','A03','Cuirass',.0011,ridge=.004)
    strip('A18_ChestSide_'+label,[(sg*.169,-.172,1.371),(sg*.184,-.143,1.353),(sg*.165,-.174,1.329)],.007,energy,'Chest','A18','Cuirass',depth=.002)
    for x,y,z in [(.182,-.150,1.431),(.106,-.200,1.343),(.105,-.185,1.486)]:
        bolt((sg*x,y-.004,z),(sg*.2,-1,0),'Chest','A03','Cuirass')
    glyph(sg*.106,-.204,1.405,'Chest','A03','Cuirass',sg,size=.85)
    for path in [[(.060,1.428),(.089,1.445),(.142,1.438),(.169,1.415)],[(.077,1.388),(.108,1.375),(.150,1.385)]]:
        strip('A03_EngravedContour',[(sg*x,-.230,z) for x,z in path],.0014,cloth,'Chest','A03','Cuirass',pool=True)
    strip('A03_PectoralMachining_'+label,[(sg*.065,-.225,1.446),(sg*.119,-.207,1.447),(sg*.181,-.157,1.421)],.0018,cloth,'Chest','A03','Cuirass',pool=True)
    plate('A04_Scapular_'+label,sx([(.022,1.490),(.111,1.523),(.206,1.477),(.208,1.376),(.127,1.327),(.042,1.343)]),
        [.160,.139,.093,.118,.174,.190],-.014,black,'Chest','A04','Cuirass',.0013,ridge=-.006)
    plate('A04_LowerBack_'+label,sx([(.034,1.351),(.148,1.366),(.187,1.322),(.131,1.256),(.028,1.249)]),
        [.182,.157,.109,.153,.187],-.010,black,'Chest','A04','Cuirass',.0012,ridge=-.004)
    for x,y,z in [(.172,.144,1.442),(.081,.185,1.332)]:bolt((sg*x,y,z),(sg*.2,1,0),'Chest','A04','Cuirass')
    glyph(sg*.09,.184,1.389,'Chest','A04','Cuirass',sg,size=.8)

diamond=[(0,1.474),(.058,1.407),(0,1.328),(-.058,1.407)]
plate('A17_CoreCradle',diamond,-.234,.025,black,'Chest','A17','Cuirass',.0014,ridge=.003)
plate('A17_CoreBezel',[(x*.85,(z-1.405)*.87+1.405) for x,z in diamond],-.254,.012,edge,'Chest','A17','Cuirass',.0010)
plate('A17_OpticalGasket',[(x*.70,(z-1.405)*.73+1.405) for x,z in diamond],-.260,.008,cloth,'Chest','A17','Cuirass',.0005)
plate('A17_FacetedCore',[(x*.58,(z-1.405)*.64+1.405) for x,z in diamond],[-.262,-.262,-.262,-.262],.009,energy,'Chest','A17','Cuirass',.0006,ridge=.014)
plate('A17_InnerFacet',[(0,1.434),(.016,1.405),(0,1.374),(-.016,1.405)],-.280,.0015,core,'Chest','A17','Cuirass',0,ridge=.004)
for i in range(5):
    z=1.271+i*.048;w=.030+(2-abs(2-i))*.005
    plate('A04_SpineVertebra_'+str(i),[(-w,z+.018),(0,z+.030),(w,z+.018),(w*.82,z-.015),(0,z-.026),(-w*.82,z-.015)],
        .197,-.010,black,'Chest','A04','Cuirass',.0010,ridge=-.004)
    strip('A18_DorsalInsert_'+str(i),[(0,.205,z-.009),(0,.205,z+.014)],.0065,energy,'Chest','A18','Cuirass',depth=-.002)

# A10/A11: separate abdominal lames and a mechanical pelvic belt.
for i in range(3):
    z=1.095+i*.059;w=.147+i*.012
    plate('A10_AbdominalLame_'+str(i),[(-w,z+.037),(-w*.87,z+.055),(0,z+.025),(w*.87,z+.055),(w,z+.037),(w*.78,z-.013),(0,z-.035),(-w*.78,z-.013)],
        [-.147,-.173,-.215,-.173,-.147,-.179,-.224,-.179],.012,black,'Spine','A10','Waist',.0011,ridge=.003)
    strip('A10_LowerRecess', [(-w*.70,-.186,z+.003),(0,-.230,z-.024),(w*.70,-.186,z+.003)],.0017,cloth,'Spine','A10','Waist',pool=True)
    plate('A10_LumbarLame_'+str(i),[(-w,z+.039),(w,z+.039),(w*.88,z-.018),(-w*.88,z-.018)],
        [.123,.123,.163,.163],-.010,black,'Spine','A10','Waist',.0011,ridge=-.003)
shell('A11_BeltCarrier',[(1.005,.202,.142,0,0),(1.045,.198,.140,0,0)],black,'Hips','A11','Waist',16)
plate('A11_Buckle',[(-.047,1.048),(.047,1.048),(.052,1.013),(0,.988),(-.052,1.013)],-.169,.022,black,'Hips','A11','Waist',.0015,ridge=.004)
strip('A11_BuckleInlay',[(-.021,-.183,1.032),(0,-.186,1.016),(.021,-.183,1.032)],.0030,edge,'Hips','A11','Waist',depth=.001)
for sg,label in [(-1,'L'),(1,'R')]:
    for i in range(3):
        x=sg*(.074+i*.045);y=-.153+i*.013
        plate('A11_BeltLink_'+label+'_'+str(i),[(x-.017,1.044),(x+.017,1.044),(x+.017,1.010),(x-.017,1.010)],y,.016,black,'Hips','A11','Waist',.0009)
        bolt((x,y-.002,1.027),(0,-1,0),'Hips','A11','Waist',.0025)

plate('A12_CentralPelvicGuard',[(-.052,1.003),(.052,1.003),(.057,.954),(.037,.884),(0,.847),(-.037,.884),(-.057,.954)],
    [-.174,-.174,-.186,-.195,-.196,-.195,-.186],.013,black,'Hips','A12','Tassets',.0013,ridge=.002)

for sg,label in [(-1,'L'),(1,'R')]:
    sx=lambda points,s=sg:[(s*x,z) for x,z in points]
    arm='UpperArm'+label;fore='Forearm'+label;thigh='Thigh'+label;shin='Shin'+label;foot='Foot'+label
    # A05: domed suspension shell and three layered shoulder scales.
    shell('A05_ShoulderBridge_'+label,[(1.461,.115,.104,sg*.301,0),(1.532,.135,.127,sg*.301,0),(1.594,.068,.065,sg*.291,0)],black,arm,'A05','Pauldron_'+label,12)
    for i in range(3):
        z=1.531-i*.053
        plate('A05_ShoulderLame_'+label+'_'+str(i),sx([(.211,z+.035),(.275,z+.065),(.358,z+.047),(.417,z-.013),(.395,z-.052),(.307,z-.036),(.230,z-.010)]),
            [-.071,-.133,-.134,-.074,-.097,-.164,-.124],.016,black,arm,'A05','Pauldron_'+label,.0014,ridge=.005)
        strip('A05_LameRecess',[(sg*.254,-.143,z-.003),(sg*.310,-.173,z-.024),(sg*.378,-.126,z-.030)],.0018,cloth,arm,'A05','Pauldron_'+label,pool=True)
    # The optical seat follows the TOP plate's plane, never a lower overlapping
    # scale. This preserves the V signal across a stepped shoulder assembly.
    cap_outline=sx([(.211,1.566),(.275,1.596),(.358,1.578),(.417,1.518),(.395,1.479),(.307,1.495),(.230,1.521)])
    cap_plane=np.linalg.lstsq(np.array([(1,x,z) for x,z in cap_outline]),np.array([-.071,-.133,-.134,-.074,-.097,-.164,-.124]),rcond=None)[0]
    light_points=[(sg*x,float(np.dot((1,sg*x,z),cap_plane))-.010,z) for x,z in [(.239,1.541),(.309,1.511),(.394,1.540)]]
    strip('A05_EnergySeat_'+label,[(x,y+.003,z) for x,y,z in light_points],.021,cloth,arm,'A05','Pauldron_'+label,depth=.004)
    strip('A18_ShoulderChevron_'+label,light_points,.0105,energy,arm,'A18','Pauldron_'+label,depth=.002)
    plate('A05_RearScale_'+label,sx([(.222,1.546),(.307,1.569),(.394,1.510),(.393,1.439),(.309,1.427),(.239,1.474)]),
        [.098,.129,.076,.096,.142,.127],-.012,black,arm,'A05','Pauldron_'+label,.0012,ridge=-.003)
    strip('A18_RearShoulder_'+label,[(sg*.250,.139,1.509),(sg*.309,.155,1.482),(sg*.365,.124,1.499)],.007,energy,arm,'A18','Pauldron_'+label,depth=-.002)
    for x,y,z in [(.248,-.122,1.550),(.390,-.111,1.482)]:bolt((sg*x,y-.004,z),(sg*.3,-1,0),arm,'A05','Pauldron_'+label)
    glyph(sg*.281,-.166,1.439,arm,'A05','Pauldron_'+label,sg,size=.8)
    strip('A05_CrownMachining',[(sg*.263,-.175,1.560),(sg*.311,-.182,1.542),(sg*.358,-.157,1.553)],.0016,cloth,arm,'A05','Pauldron_'+label,pool=True)
    # A06/A07: upper arm and an open-back elbow shield.
    f,L=frame_for(arm)
    shell('A06_BicepCarrier_'+label,[(.076,.077,.072,0,0),(.137,.082,.078,0,0),(L-.037,.064,.067,0,0)],black,arm,'A06','Pauldron_'+label,12,f)
    plate('A06_BicepShell_'+label,[(-.066,.105),(-.050,.066),(.050,.066),(.066,.105),(.058,L*.82),(0,L*.92),(-.058,L*.82)],-.082,.030,black,arm,'A06','Pauldron_'+label,.0013,f,ridge=.005)
    plate('A07_ElbowShield_'+label,[(-.063,L-.026),(0,L-.049),(.063,L-.026),(.058,L+.024),(0,L+.041),(-.058,L+.024)],-.086,.016,black,arm,'A07','Pauldron_'+label,.0012,f,ridge=.006)
    glyph(-.024,-.092,.134,arm,'A06','Pauldron_'+label,frame=f,size=.8)
    # A08: fitted half shell, separate luminous insert and closed rear cover.
    f,L=frame_for(fore)
    shell('A08_InnerForearm_'+label,[(.043,.063,.066,0,0),(.104,.070,.071,0,0),(L-.023,.049,.053,0,0)],black,fore,'A08','Vambrace_'+label,12,f)
    outline=[(-.063,.039),(0,.020),(.063,.039),(.068,.078),(.046,L-.019),(0,L+.002),(-.046,L-.019),(-.068,.078)]
    plate('A08_ForearmShell_'+label,outline,-.096,.030,black,fore,'A08','Vambrace_'+label,.0014,f,ridge=.005)
    plate('A08_RearClosure_'+label,[(-.048,.051),(.048,.051),(.043,L-.026),(0,L-.005),(-.043,L-.026)],.063,-.015,black,fore,'A08','Vambrace_'+label,.0011,f,ridge=-.003)
    inset=[(-.013,.064),(.017,.054),(.022,.082),(.009,L-.045),(-.004,L-.022),(-.014,L-.058)]
    plate('A08_InsertGasket_'+label,[(x*1.35,z) for x,z in inset],-.112,.005,cloth,fore,'A08','Vambrace_'+label,.0004,f)
    plate('A18_ForearmLens_'+label,inset,-.116,.003,energy,fore,'A18','Vambrace_'+label,.0005,f,ridge=.001)
    for side in [-1,1]:
        strip('A08_EngravedRail',[(side*.044,-.107,.073),(side*.037,-.110,L-.048)],.0021,cloth,fore,'A08','Vambrace_'+label,f,pool=True)
        bolt(f((side*.048,-.109,.065)),f((0,-1,0))-f((0,0,0)),fore,'A08','Vambrace_'+label,.0027)
    glyph(-.037,-.111,.107,fore,'A08','Vambrace_'+label,frame=f,size=.65)
    # A12: split hip plate follows the existing thigh joint, not a rigid skirt.
    plate('A12_HipMain_'+label,sx([(.033,1.000),(.133,1.013),(.229,.969),(.225,.875),(.171,.814),(.093,.862),(.048,.922)]),
        [-.146,-.154,-.101,-.119,-.167,-.176,-.177],.016,black,thigh,'A12','Tassets',.0014,ridge=.005)
    plate('A12_HipSide_'+label,sx([(.171,.977),(.237,.947),(.256,.885),(.224,.834),(.193,.864)]),
        [-.078,-.049,-.039,-.054,-.092],.016,black,thigh,'A12','Tassets',.0012,ridge=.004)
    strip('A18_HipInsert_'+label,[(sg*.188,-.150,.945),(sg*.191,-.158,.906),(sg*.177,-.176,.876)],.0055,energy,thigh,'A18','Tassets',depth=.002)
    glyph(sg*.109,-.181,.897,thigh,'A12','Tassets',sg,size=.7)
    strip('A12_HipContour',[(sg*.080,-.186,.974),(sg*.129,-.190,.965),(sg*.173,-.169,.929),(sg*.155,-.183,.884)],.0017,cloth,thigh,'A12','Tassets',pool=True)
    plate('A12_RearFauld_'+label,sx([(.018,1.009),(.136,1.012),(.207,.967),(.196,.880),(.124,.850),(.032,.901)]),
        [.144,.130,.099,.119,.161,.171],-.014,black,'Hips','A12','Tassets',.0013,ridge=-.004)
    # A13: deep, full-wrap cuisses; broad quadriceps taper into the original knees.
    # The outboard flare adds mass without closing the inner-leg movement gap.
    f,L=frame_for(thigh)
    shell('A13_ThighCarrier_'+label,[(.085,.102,.103,sg*.012,0),(.165,.116,.111,sg*.014,0),
        (.255,.110,.108,sg*.014,0),(L-.072,.081,.082,sg*.010,0)],black,thigh,'A13','Cuisse_'+label,16,f)
    broad=[(-.099,.125),(-.070,.084),(.070,.084),(.111,.125),(.109,.226),(.083,L-.083),
        (.018,L-.030),(-.078,L-.077),(-.096,.220)]
    plate('A13_ThighPlate_'+label,[(x+sg*.010,z) for x,z in broad],-.124,.027,black,thigh,'A13','Cuisse_'+label,.0018,f,ridge=.006)
    # Overlapping upper plate and a long center keel replace the small knee-like patch.
    plate('A13_QuadricepsCrown_'+label,[(-.079,.116),(-.056,.091),(.062,.091),(.089,.125),(.073,.181),(0,.208),(-.074,.177)],
        -.143,.010,black,thigh,'A13','Cuisse_'+label,.0013,f,ridge=.003)
    plate('A13_QuadricepsKeel_'+label,[(-.028,.193),(0,.180),(.033,.193),(.028,L-.102),(0,L-.061),(-.027,L-.103)],
        [-.145,-.157,-.145,-.142,-.146,-.142],.012,black,thigh,'A13','Cuisse_'+label,.0012,f,ridge=.002)
    side_frame=lambda p,s=sg:f((s*(.107-p[1]),p[0],p[2]))
    plate('A13_OuterThighScale_'+label,[(-.077,.130),(-.033,.096),(.042,.114),(.085,.174),(.076,.261),(.040,L-.077),(-.055,L-.076),(-.085,.248)],
        -.013,.018,black,thigh,'A13','Cuisse_'+label,.0014,side_frame,ridge=.004)
    plate('A13_HamstringCover_'+label,[(-.082,.118),(-.050,.092),(.060,.092),(.093,.135),(.075,L-.089),(0,L-.053),(-.071,L-.094)],
        .106,-.017,black,thigh,'A13','Cuisse_'+label,.0012,f,ridge=-.003)
    strip('A13_ThighMachining',[(-.066,-.148,.173),(-.058,-.147,.258),(-.031,-.142,L-.079)],.0030,cloth,thigh,'A13','Cuisse_'+label,f,pool=True)
    for x in [-.061,.071]:bolt(f((x,-.150,.132)),f((0,-1,0))-f((0,0,0)),thigh,'A13','Cuisse_'+label,.0030)
    # A14: faceted knee with a pair of recessed lateral pivot caps.
    f,L=frame_for(shin)
    shell('A15_ShinCarrier_'+label,[(.106,.063,.067,0,0),(.202,.069,.071,0,0),(L-.041,.048,.053,0,0)],black,shin,'A15','Greave_'+label,12,f)
    plate('A14_KneeShell_'+label,[(-.068,-.023),(0,-.053),(.068,-.023),(.075,.035),(.038,.083),(0,.096),(-.038,.083),(-.075,.035)],-.109,.019,black,shin,'A14','Greave_'+label,.0014,f,ridge=.007)
    for side in [-1,1]:
        pivot=lambda p,s=side:Vector((s*(.073-p[1]),p[0]-.010,p[2]+.020))
        pf=lambda p: f(pivot(p))
        circle=[(.027*math.cos(i*math.tau/12),.027*math.sin(i*math.tau/12)) for i in range(12)]
        plate('A14_Pivot_'+label+'_'+str(side),circle,-.003,.006,black,shin,'A14','Greave_'+label,.0008,pf)
        plate('A14_PivotInset_'+label+'_'+str(side),[(x*.65,z*.65) for x,z in circle],-.007,.003,edge,shin,'A14','Greave_'+label,.0004,pf)
    # A15: broad lower-leg plates plus separate back cover and a seated lens.
    outline=[(-.057,.100),(0,.081),(.057,.100),(.059,.180),(.043,L-.051),(0,L-.017),(-.043,L-.051),(-.059,.180)]
    plate('A15_ShinShell_'+label,outline,-.096,.022,black,shin,'A15','Greave_'+label,.0013,f,ridge=.005)
    plate('A15_CalfCover_'+label,[(-.047,.119),(.047,.119),(.043,L-.065),(0,L-.029),(-.043,L-.065)],.067,-.015,black,shin,'A15','Greave_'+label,.0011,f,ridge=-.003)
    inset=[(-.014,.132),(0,.113),(.016,.135),(.012,L-.065),(0,L-.037),(-.010,L-.071)]
    plate('A15_LensSeat_'+label,[(x*1.5,z) for x,z in inset],-.110,.005,cloth,shin,'A15','Greave_'+label,.0004,f)
    plate('A18_ShinLens_'+label,inset,-.115,.003,energy,shin,'A18','Greave_'+label,.0005,f,ridge=.001)
    for side in [-1,1]:
        strip('A15_SecondaryRail',[(side*.041,-.106,.143),(side*.030,-.110,L-.070)],.0020,cloth,shin,'A15','Greave_'+label,f,pool=True)
        bolt(f((side*.043,-.108,.133)),f((0,-1,0))-f((0,0,0)),shin,'A15','Greave_'+label,.0026)
    for j in range(3):strip('A15_CalfVent',[(-.023,.083,.145+j*.031),(.023,.083,.145+j*.031)],.0030,cloth,shin,'A15','Greave_'+label,f,depth=-.001,pool=True)
    # A16: compact layered boot, retained floor height and original foot binding.
    cx=sg*.170
    shell('A16_Outsole_'+label,[(.002,.089,.162,cx,-.071),(.023,.098,.171,cx,-.071)],cloth,foot,'A16','Sabaton_'+label,12)
    shell('A16_BootShell_'+label,[(.025,.097,.168,cx,-.070),(.070,.095,.160,cx,-.064),(.121,.078,.112,cx,-.019),(.174,.065,.072,cx,.005)],black,foot,'A16','Sabaton_'+label,12)
    for i in range(3):
        yf=-.236+i*.062;yb=yf+.075;zf=.083+i*.024;zb=zf+.029;w=.081-i*.005
        verts=[(cx-w,yf+.010,zf),(cx-w*.65,yf,zf-.006),(cx+w*.65,yf,zf-.006),(cx+w,yf+.010,zf),(cx+w*.87,yb,zb),(cx-w*.87,yb,zb)]
        mesh('A16_InstepLame_'+label+'_'+str(i),verts+[(x,y,z-.008) for x,y,z in verts],
            [tuple(range(6)),tuple(range(6,12))]+[(j,(j+1)%6,(j+1)%6+6,j+6) for j in range(6)],black,foot,'A16','Sabaton_'+label,.0010)
    plate('A16_AnkleGuard_'+label,[(cx-.062,.153),(cx-.044,.192),(cx+.044,.192),(cx+.062,.153),(cx+.043,.122),(cx-.043,.122)],-.073,.018,black,foot,'A16','Sabaton_'+label,.0011,ridge=.004)
    strip('A16_ToeInset_'+label,[(cx-.020,-.239,.084),(cx,-.244,.079),(cx+.020,-.239,.084)],.0024,edge,foot,'A16','Sabaton_'+label,pool=True)

# Reinforced armor's secondary structures use the same rest rig/material palette.
exec(compile((HERE/'reinforce_armor_v8.py').read_text(),str(HERE/'reinforce_armor_v8.py'),'exec'))

# Small hardware/engravings are consolidated by module, material and binding.
# The main shells and luminous inserts remain independently editable.
for (material,bone,module,assembly),(verts,faces) in detail_batches.items():
    mesh(module+'_Machining_'+bone+'_'+material.replace('Void ', '').replace(' ','_'),verts,faces,bpy.data.materials[material],bone,module,assembly)

after={o.name:geometry_digest(o) for o in scene.objects if protected(o)}
assert before==after,'Protected sword, hands or undersuit changed'
assert before_actions==actions_digest(),'Action keyframes changed'
assert before_bones=={b.name:(tuple(b.head_local),tuple(b.tail_local),tuple(x for r in b.matrix_local for x in r),b.parent.name if b.parent else None) for b in rig.data.bones},'Rig changed'
meshes=[o for o in scene.objects if o.type=='MESH']
for o in meshes:o.data.calc_loop_triangles()
triangles=sum(len(o.data.loop_triangles) for o in meshes)
assert triangles<=35000,triangles
assert len(meshes)<=480,len(meshes)
assert len({s.material.name for o in meshes for s in o.material_slots})==5
assert len(rig.data.bones)==19 and len(bpy.data.actions)==12
rig['design_version']=8;rig['armor_blueprint_version']=3
scene['void_knight_design']='Reinforced V3 armor / geometry revision 8 / 2026-10-29'
rig.data.pose_position='POSE';rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
scene.render.fps=30
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
report=dict(result='PASS',concept='Reinforced Armor V3',geometry_revision=8,source_parts=len(meshes),new_armor_parts=len(created),replaced_armor_parts=replaced_count,
    protected_parts=len(before),protected_geometry_unchanged=True,original_actions_unchanged=True,original_rest_rig_unchanged=True,
    bones=len(rig.data.bones),clips=len(bpy.data.actions),triangles=triangles,materials=5,
    parts=[dict(name=o.name,module=o['v3_module'],assembly=o['assembly'],bone=o['bone'],triangles=len(o.data.loop_triangles)) for o in created])
(HERE/'build-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('ARMOR_V3_BUILT',json.dumps({k:v for k,v in report.items() if k!='parts'}),flush=True)
