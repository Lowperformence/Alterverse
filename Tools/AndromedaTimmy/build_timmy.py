"""Original Season 2 cat astronaut: 19 shared hero bones plus one animated tail.
Run: blender --background --python-exit-code 1 --python Tools/AndromedaTimmy/build_timmy.py
Exports editable Blender source, Unity FBX, baked surface textures and studio preview.
All geometry/materials are original project work; no human base mesh is required.
"""
from pathlib import Path
import math, json, argparse, sys, os
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/AndromedaTimmy'
OUT = ROOT / 'Assets/Alterverse/Resources/AndromedaTimmy/Icons'
FBX = ROOT / 'Assets/Alterverse/Resources/AuthoredHeroes/Timmy.fbx'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--model-only',action='store_true',help='Rebuild model/preview without rewriting attack icons.')
parser.add_argument('--prototype-icons',action='store_true',help='Render the old modeled icon prototypes into ArtSource only.')
options=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
VISOR_OPACITY=.50 # 50% transparent; a single curved surface avoids doubled tint.
VISOR_COLOR=(.78,.80,.82)
for p in (ART, OUT, FBX.parent): p.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)

def material(name, color, metal=0, glow=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=.36
    bs.inputs.get('Emission Color',bs.inputs.get('Emission')).default_value=(*color,1)
    bs.inputs['Emission Strength'].default_value=glow
    return m

white=material('Timmy pearl space suit',(.74,.81,.84),.35)
navy=material('Timmy graphite seals',(.025,.046,.085),.35)
fabric=material('Timmy woven astronaut fabric',(.84,.87,.88))
nodes=fabric.node_tree.nodes;links=fabric.node_tree.links;bs=nodes.get('Principled BSDF')
bs.inputs['Roughness'].default_value=.88
coord=nodes.new('ShaderNodeTexCoord');weaves=[]
for axis in ('X','Y'):
    wave=nodes.new('ShaderNodeTexWave');wave.bands_direction=axis
    wave.inputs['Scale'].default_value=180;wave.inputs['Distortion'].default_value=.3
    links.new(coord.outputs['Generated'],wave.inputs['Vector']);weaves.append(wave)
weave=nodes.new('ShaderNodeMath');weave.operation='MULTIPLY'
for i,wave in enumerate(weaves):links.new(wave.outputs['Fac'],weave.inputs[i])
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.68,.73,.75,1)
ramp.color_ramp.elements[1].color=(.86,.89,.90,1)
links.new(weave.outputs[0],ramp.inputs[0]);links.new(ramp.outputs['Color'],bs.inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.20;bump.inputs['Distance'].default_value=.001
links.new(weave.outputs[0],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
red=material('Timmy muted red suit bands',(.46,.075,.045))
red.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.75
def fur_material(name,low,high):
    m=material(name,high);nodes=m.node_tree.nodes;links=m.node_tree.links
    bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.82
    coord=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY'
    mapping.inputs[1].default_value=(1,1,7);links.new(coord.outputs['Generated'],mapping.inputs[0])
    fibers=nodes.new('ShaderNodeTexNoise');fibers.inputs['Scale'].default_value=62;fibers.inputs['Detail'].default_value=2
    links.new(mapping.outputs['Vector'],fibers.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.22;ramp.color_ramp.elements[0].color=(*low,1)
    ramp.color_ramp.elements[1].position=.78;ramp.color_ramp.elements[1].color=(*high,1)
    links.new(fibers.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],bs.inputs['Base Color'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.22;bump.inputs['Distance'].default_value=.0007
    links.new(fibers.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    return m
fur=fur_material('Timmy orange tabby fur',(.48,.13,.022),(.98,.46,.12))
cream=fur_material('Timmy muzzle cream',(.67,.48,.27),(.96,.86,.63))
stripe=fur_material('Timmy cinnamon tabby fur',(.13,.040,.012),(.39,.13,.028))
nose=material('Timmy warm feline nose',(.22,.075,.045))
nose.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.65
cyan=material('Timmy cyan energy',(.04,.68,1),.45,2)
back_blue=material('Timmy blue back stripe energy',(.025,.22,1),.25,3)
gold=material('Timmy jet amber energy',(1,.46,.075),.35,1.4)
eye=material('Timmy emerald eyes',(.08,.49,.21),.25,.25)
glass=material('Timmy light gray visor glass',VISOR_COLOR)
glass.diffuse_color=(*VISOR_COLOR,VISOR_OPACITY)
glass.use_backface_culling=True
glass.surface_render_method='DITHERED'
glass_bs=glass.node_tree.nodes.get('Principled BSDF')
glass_bs.inputs['Base Color'].default_value=(*VISOR_COLOR,VISOR_OPACITY)
glass_bs.inputs['Alpha'].default_value=VISOR_OPACITY
glass_bs.inputs['Roughness'].default_value=.10
meshes=[]

def finish(ob,name,mat,bone=None):
    ob.name=name;ob.data.materials.append(mat)
    for f in ob.data.polygons:f.use_smooth=True
    if bone:ob['bone']=bone
    meshes.append(ob);return ob
def ellipsoid(name,p,s,mat,bone=None,segments=16,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=p)
    ob=bpy.context.object;ob.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(ob,name,mat,bone)
def box(name,p,s,mat,bone=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);ob=bpy.context.object;ob.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel=ob.modifiers.new('Soft molded corners','BEVEL');bevel.width=min(s)*.2;bevel.segments=2
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=bevel.name)
    return finish(ob,name,mat,bone)
def tube(name,points,radius,mat,bone=None):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=radius;c.bevel_resolution=2
    spline=c.splines.new('POLY');spline.points.add(len(points)-1)
    for q,p in zip(spline.points,points):q.co=(*p,1)
    ob=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH');ob.select_set(False)
    return finish(ob,name,mat,bone)
def ring(name,p,r,thickness,mat,bone=None):
    return tube(name,[(p[0]+r*math.cos(i*math.tau/48),p[1],p[2]+r*math.sin(i*math.tau/48)) for i in range(49)],thickness,mat,bone)
def triangle(name,points,depth,mat,bone=None):
    verts=[(x,y+dy,z) for dy in (-depth/2,depth/2) for x,y,z in points]
    faces=[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)]
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob);return finish(ob,name,mat,bone)
def oval_band(name,p,r,height,mat,bone=None):
    # Closed 16-sided molded seam/cuff, 128 triangles, with an oval XY profile.
    segments=16;verts=[];faces=[]
    for scale,z in ((.96,-height/2),(1,-height*.28),(1,height*.28),(.96,height/2)):
        for i in range(segments):
            a=i*math.tau/segments;verts.append((p[0]+r[0]*scale*math.cos(a),p[1]+r[1]*scale*math.sin(a),p[2]+z))
    for row in range(4):
        nxt=(row+1)%4
        for i in range(segments):
            j=(i+1)%segments;faces.append((row*segments+i,row*segments+j,nxt*segments+j,nxt*segments+i))
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob);return finish(ob,name,mat,bone)
PAW_FORWARD=Vector((0,-.068,-.05)).normalized()
PAW_DEPTH=Vector((0,-PAW_FORWARD.z,PAW_FORWARD.y))
def feline_paw(side,k):
    # One continuous, flattened forepaw: narrow wrist, oval palm and four
    # scalloped toe tips. Avoid separate spherical knuckles or a human thumb.
    outline=[(-.040,-.035),(-.049,-.015),(-.061,.029),(-.065,.070),
             (-.064,.103),(-.055,.123),(-.043,.126),(-.035,.118),
             (-.030,.133),(-.018,.145),(-.007,.138),(0,.126),
             (.007,.138),(.018,.145),(.030,.133),(.035,.118),
             (.043,.126),(.055,.123),(.064,.103),(.065,.070),
             (.061,.029),(.049,-.015),(.040,-.035),(0,-.040)]
    perimeter=[]
    for i,p1 in enumerate(outline):
        p0=Vector(outline[(i-1)%len(outline)]);p1=Vector(p1)
        p2=Vector(outline[(i+1)%len(outline)]);p3=Vector(outline[(i+2)%len(outline)])
        for t in (0,.5):
            perimeter.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
    wrist=Vector((side*.35,-.072,.79));center=.047;verts=[];faces=[]
    for scale,depth in ((.32,.035),(.68,.029),(.91,.017),(1,0),(.91,-.017),(.68,-.029),(.32,-.035)):
        for x,t in perimeter:
            verts.append(tuple(wrist+Vector((side*x*scale,0,0))+PAW_FORWARD*(center+(t-center)*scale)+PAW_DEPTH*depth))
    n=len(perimeter)
    for row in range(6):
        for i in range(n):
            j=(i+1)%n;faces.append((row*n+i,row*n+j,(row+1)*n+j,(row+1)*n+i))
    for row,depth in ((0,.037),(6,-.037)):
        cap=len(verts);verts.append(tuple(wrist+PAW_FORWARD*center+PAW_DEPTH*depth))
        for i in range(n):
            j=(i+1)%n;faces.append((cap,row*n+j,row*n+i) if row==0 else (cap,row*n+i,row*n+j))
    data=bpy.data.meshes.new('Feline forepaw'+k);data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new('Paw'+k,data);bpy.context.collection.objects.link(ob)
    # Mirror changes winding; recalculate before baking the normal atlas.
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT');ob.select_set(False)
    return finish(ob,'Paw'+k,fur,'Hand'+k)

HEAD_CENTER=Vector((0,-.075,1.545))
HEAD_RADII=Vector((.178,.145,.17))
def head_width(z):
    relative=(z-HEAD_CENTER.z)/HEAD_RADII.z
    return 1+.12*math.exp(-((relative+.22)/.34)**2)-.13*max(0,-relative)-.10*max(0,relative)
def head_surface_y(x,z):
    r2=(x/(HEAD_RADII.x*head_width(z)))**2+((z-HEAD_CENTER.z)/HEAD_RADII.z)**2
    return HEAD_CENTER.y-HEAD_RADII.y*math.sqrt(max(.02,1-r2))
def feline_eye(name,side,width,height,front,mat):
    # Pointed inner/outer corners, a domed surface and a raised outer corner.
    segments=24;verts=[(side*.080,front-.010,1.581)];faces=[]
    for scale in (.52,1):
        for i in range(segments):
            a=i*math.tau/segments;dx=width*math.cos(a)*scale
            dz=height*math.sin(a)*abs(math.sin(a))**.35*scale+side*dx*.17
            verts.append((side*.080+dx,front-.010*(1-scale*scale),1.581+dz))
    for i in range(segments):
        j=(i+1)%segments;faces.append((0,1+i,1+j));faces.append((1+i,1+segments+i,1+segments+j,1+j))
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob)
    return finish(ob,name,mat,'Head')

def visor_dome():
    # A circular spherical cap fitted inside the rim, in front of muzzle/whiskers.
    # Negative Y faces forward in the authored rig. Wind only the outside surface.
    radius=.255;depth=.205;center=(0,-.118,1.54);segments=48;rings=12
    sphere=(radius*radius+depth*depth)/(2*depth)
    verts=[(center[0],center[1]-depth,center[2])];faces=[]
    for row in range(1,rings+1):
        r=radius*row/rings
        y=center[1]-(math.sqrt(max(0,sphere*sphere-r*r))-(sphere-depth))
        for i in range(segments):
            a=i*math.tau/segments
            verts.append((center[0]+r*math.cos(a),y,center[2]+r*math.sin(a)))
    for i in range(segments):faces.append((0,1+i,1+(i+1)%segments))
    for row in range(1,rings):
        inner=1+(row-1)*segments;outer=1+row*segments
        for i in range(segments):
            n=(i+1)%segments;faces.append((inner+i,outer+i,outer+n,inner+n))
    data=bpy.data.meshes.new('Timmy round visor');data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new('DF_Visor',data);bpy.context.collection.objects.link(ob)
    return finish(ob,'DF_Visor',glass,'Head')

def face_mark(name,points,width):
    # Thin fitted fur markings on the existing head surface, not metal bars.
    verts=[]
    for index,(x,z) in enumerate(points):
        w=width*(.35+.65*math.sin((index+.5)/len(points)*math.pi))
        for dx in (-w,w):
            px=x+dx;y=head_surface_y(px,z)-.001
            verts.append((px,y,z))
    faces=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(points)-1)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    return finish(ob,name,stripe,'Head')

# Compact feline astronaut: the helmet encloses the head; no external ears.
ellipsoid('Suit torso',(0,0,1.11),(.23,.16,.30),fabric,'Chest')
ellipsoid('Flexible waist',(0,0,.86),(.20,.145,.12),navy,'Hips')
for i in range(3):oval_band('Waist pressure fold'+str(i),(0,0,.86+i*.028),(.202,.147),.018,fabric,'Hips')
oval_band('Helmet neck seal',(0,0,1.35),(.13,.115),.03,navy,'Chest')
box('Chest control housing',(0,-.170,1.12),(.19,.045,.16),white,'Chest')
box('Cyan chest display',(-.026,-.197,1.145),(.085,.010,.055),cyan,'Chest')
for i in range(3):box('Chest tactile switch'+str(i),(-.051+i*.052,-.198,1.086),(.026,.009,.014),navy,'Chest')
ellipsoid('Oxygen chest valve',(.064,-.202,1.145),(.018,.012,.018),gold,'Chest')
box('Oxygen backpack',(0,.18,1.15),(.26,.14,.31),white,'Chest')
box('Backpack center service hatch',(0,.257,1.16),(.12,.013,.20),navy,'Chest')
box('Blue luminous back stripe',(0,.271,1.16),(.027,.010,.166),back_blue,'Chest')
for side,k in ((-1,'L'),(1,'R')):
    tube('Chest harness seam'+k,[(side*.147,-.081,1.36),(side*.161,-.132,1.23),(side*.146,-.139,1.04),(side*.126,-.106,.96)],.010,navy,'Chest')
    box('Harness buckle'+k,(side*.145,-.151,1.018),(.042,.019,.042),white,'Chest')
    tube('Oxygen hose'+k,[(side*.17,.23,1.23),(side*.235,.125,1.17),(side*.222,-.01,1.12),(side*.195,-.105,1.07)],.012,navy,'Chest')
for side,k in ((-1,'L'),(1,'R')):
    ellipsoid('Jet housing'+k,(side*.13,.245,1.12),(.075,.07,.15),white,'Chest')
    ellipsoid('Jet nozzle'+k,(side*.13,.255,.99),(.043,.04,.025),cyan,'Chest')
ellipsoid('Helmet back',(0,.035,1.54),(.27,.18,.285),white,'Head')
head=ellipsoid('Cat head',HEAD_CENTER,HEAD_RADII,fur,'Head',segments=32,rings=16)
for v in head.data.vertices:v.co.x*=head_width(v.co.z+HEAD_CENTER.z)
ellipsoid('Feline nose bridge',(0,-.208,1.542),(.032,.038,.056),fur,'Head')
ellipsoid('Cream feline chin',(0,-.213,1.444),(.061,.035,.026),cream,'Head')
for side,k in ((-1,'L'),(1,'R')):
    ellipsoid('Whisker pad'+k,(side*.037,-.226,1.491),(.052,.048,.040),cream,'Head')
triangle('Cat nose',[(-.019,-.267,1.524),(.019,-.267,1.524),(0,-.275,1.501)],.010,nose,'Head')
tube('Nose to mouth',[(0,-.277,1.505),(0,-.270,1.487),(0,-.264,1.475)],.002,navy,'Head')
for side,k in ((-1,'L'),(1,'R')):
    tube('Feline mouth'+k,[(0,-.264,1.475),(side*.015,-.266,1.470),(side*.028,-.263,1.475)],.002,navy,'Head')
ring('Visor rim',(0,-.118,1.54),.255,.018,white,'Head')
box('Helmet chin guard',(0,-.145,1.306),(.31,.105,.085),white,'Head')
box('Helmet chin vent',(0,-.201,1.309),(.16,.008,.032),navy,'Head')
visor=visor_dome()
face_mark('Tabby forehead center',[(-.007,1.656),(0,1.625),(.002,1.596)],.006)
for side,k in ((-1,'L'),(1,'R')):
    ellipsoid('Helmet hinge'+k,(side*.257,-.024,1.535),(.043,.065,.076),white,'Head')
    ellipsoid('Helmet hinge seal'+k,(side*.29,-.024,1.535),(.014,.046,.053),navy,'Head')
    ellipsoid('Helmet hinge light'+k,(side*.30,-.024,1.535),(.007,.026,.031),cyan,'Head')
    feline_eye('Almond eye socket'+k,side,.053,.029,-.219,navy)
    feline_eye('Almond eye'+k,side,.047,.023,-.222,eye)
    ellipsoid('Vertical pupil'+k,(side*.080,-.235,1.581),(.006,.004,.020),navy,'Head')
    for i in range(3):
        tube('Whisker'+k+str(i),[(side*.062,-.268,1.491+(i-1)*.010),(side*.135,-.243,1.491+(i-1)*.020),(side*.193,-.183,1.484+(i-1)*.035)],.0015,cream,'Head')
    face_mark('Tabby forehead M'+k,[(side*.075,1.628),(side*.052,1.648),(side*.027,1.612)],.006)
    for i in range(2):
        face_mark('Tabby cheek stripe'+k+str(i),[(side*.147,1.507+i*.024),(side*.126,1.498+i*.024),(side*.104,1.482+i*.024)],.006)
    ellipsoid('Shoulder'+k,(side*.26,0,1.28),(.12,.115,.13),white,'UpperArm'+k)
    ellipsoid('Upper suit arm'+k,(side*.29,-.01,1.15),(.08,.075,.17),fabric,'UpperArm'+k)
    for i in range(2):oval_band('Arm pressure fold'+k+str(i),(side*.30,-.01,1.08+i*.035),(.077,.073),.012,fabric,'UpperArm'+k)
    ellipsoid('Elbow seal'+k,(side*.315,-.018,1.01),(.07,.073,.075),navy,'Forearm'+k)
    ellipsoid('Forearm gauntlet'+k,(side*.335,-.04,.91),(.082,.085,.125),white,'Forearm'+k)
    feline_paw(side,k)
    for i in range(3):
        x=side*(.35+(i-1)*.038);t=.120
        root=Vector((x,-.072,.79))+PAW_FORWARD*t-PAW_DEPTH*.004
        tube('Claw'+k+str(i),[root,root+PAW_FORWARD*.038+PAW_DEPTH*.004,root+PAW_FORWARD*.062-PAW_DEPTH*.015],.006,cyan,'WeaponGrip'+k)
    ellipsoid('Thigh suit'+k,(side*.13,0,.66),(.095,.09,.23),fabric,'Thigh'+k)
    oval_band('Red thigh ID band'+k,(side*.13,0,.73),(.096,.091),.035,red,'Thigh'+k)
    box('Thigh fabric pocket'+k,(side*.135,-.082,.615),(.11,.024,.105),fabric,'Thigh'+k)
    box('Pocket closure'+k,(side*.135,-.097,.658),(.092,.008,.014),navy,'Thigh'+k)
    for i in range(2):oval_band('Knee pressure fold'+k+str(i),(side*.139,-.01,.51+i*.025),(.077,.073),.012,fabric,'Thigh'+k)
    ellipsoid('Knee seal'+k,(side*.14,-.02,.46),(.079,.073,.075),navy,'Shin'+k)
    ellipsoid('Shin suit'+k,(side*.14,0,.29),(.08,.072,.16),fabric,'Shin'+k)
    tube('Shin seam'+k,[(side*.20,-.03,.40),(side*.203,-.048,.30),(side*.19,-.042,.20)],.006,navy,'Shin'+k)
    ellipsoid('Round space boot'+k,(side*.14,-.045,.095),(.105,.145,.085),navy,'Foot'+k)
    box('Boot toe cap'+k,(side*.14,-.14,.11),(.16,.10,.075),white,'Foot'+k)
tail=[(0,.12,.86),(0,.25,.83),(0,.39,.78),(0,.52,.75),(0,.64,.78),(0,.70,.85)]
tube('Centered tabby tail',tail,.04,fur,'Tail')
for i,p in enumerate(tail[1::2]):ellipsoid('Tail stripe'+str(i),p,(.042,.043,.041),stripe,'Tail')

data=bpy.data.armatures.new('DF_Skeleton');rig=bpy.data.objects.new('DF_Skeleton',data)
bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(n,h,t,parent=None):
    b=data.edit_bones.new(n);b.head=h;b.tail=t
    if parent:b.parent=bones[parent]
    bones[n]=b
bone('Root',(0,0,0),(0,0,.15));bone('Hips',(0,0,.86),(0,0,1.02),'Root')
bone('Spine',(0,0,1.02),(0,0,1.20),'Hips');bone('Chest',(0,0,1.20),(0,0,1.42),'Spine');bone('Head',(0,0,1.42),(0,0,1.75),'Chest')
bone('Tail',(0,.12,.86),(0,.42,.80),'Hips')
for s,k in ((-1,'L'),(1,'R')):
    shoulder=(s*.26,0,1.28);elbow=(s*.315,-.018,1.01);wrist=(s*.35,-.072,.79);tip=(s*.35,-.14,.74)
    bone('UpperArm'+k,shoulder,elbow,'Chest');bone('Forearm'+k,elbow,wrist,'UpperArm'+k)
    bone('Hand'+k,wrist,tip,'Forearm'+k);bone('WeaponGrip'+k,wrist,tip,'Hand'+k)
    bone('Thigh'+k,(s*.13,0,.86),(s*.14,-.02,.46),'Hips');bone('Shin'+k,(s*.14,-.02,.46),(s*.14,0,.15),'Thigh'+k)
    bone('Foot'+k,(s*.14,0,.15),(s*.14,-.14,.08),'Shin'+k)
bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
for ob in meshes:
    g=ob.vertex_groups.new(name=ob['bone']);g.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    ob.parent=rig;mod=ob.modifiers.new('Authored hero rig','ARMATURE');mod.object=rig

body=[o for o in meshes if not o['bone'].startswith('WeaponGrip') and o!=visor]
def join(group,name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in group:ob.select_set(True)
    bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name;return ob
claw_groups={k:[o for o in meshes if o.name.startswith('Claw'+k)] for k in ('L','R')}
body=join(body,'DF_Anatomy')

# Bake fur fibers and woven suit into ordinary mobile URP textures. No hair particles.
texture_dir=FBX.parent/'TimmyTextures';texture_dir.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
scene.render.bake.margin=8;scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False
scene.render.bake.use_pass_color=True
textures={}
for kind,bake_type in [('Albedo','DIFFUSE'),('Normal','NORMAL')]:
    img=bpy.data.images.new('Timmy surface '+kind,width=1024,height=1024,alpha=False)
    if kind=='Normal':img.colorspace_settings.name='Non-Color'
    for mat in body.data.materials:
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;mat.node_tree.nodes.active=node
    bpy.ops.object.bake(type=bake_type)
    img.filepath_raw=str(texture_dir/('TimmySurface_'+kind+'.png'));img.file_format='PNG';img.save()
    img.pack() # Editable source remains usable even when opened outside this checkout.
    img.filepath='//'+os.path.relpath(texture_dir/('TimmySurface_'+kind+'.png'),ART)
    textures[kind]=img
for mat in (fur,cream,stripe,fabric):
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF')
    base=nodes.new('ShaderNodeTexImage');base.image=textures['Albedo'];links.new(base.outputs['Color'],bs.inputs['Base Color'])
    normal=nodes.new('ShaderNodeTexImage');normal.image=textures['Normal']
    normal_map=nodes.new('ShaderNodeNormalMap');normal_map.inputs['Strength'].default_value=.5
    links.new(normal.outputs['Color'],normal_map.inputs['Color']);links.new(normal_map.outputs['Normal'],bs.inputs['Normal'])
    nodes.active=base
weapons=[]
for k in ('L','R'):weapons.append(join(claw_groups[k],'DF_Weapon'+k))
for k in ('L','R'):
    for name,parent in (('DF_PalmGrip','Hand'),('DF_HandleContact','WeaponGrip')):
        socket=bpy.data.objects.new(name+k,None);bpy.context.collection.objects.link(socket)
        socket.parent=rig;socket.parent_type='BONE';socket.parent_bone=parent+k
        bpy.context.view_layer.update();world=socket.matrix_world.copy()
        world.translation=Vector(((-.35 if k=='L' else .35),-.072,.79));socket.matrix_world=world
    socket=bpy.data.objects.new('DF_TimmyJet'+k,None);bpy.context.collection.objects.link(socket)
    socket.parent=rig;socket.parent_type='BONE';socket.parent_bone='Chest'
    bpy.context.view_layer.update();world=socket.matrix_world.copy()
    world.translation=Vector(((-.13 if k=='L' else .13),.255,.975));socket.matrix_world=world
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'AndromedaTimmy.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(FBX),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True)
triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in [body,*weapons,visor])
(ART/'model-report.json').write_text(json.dumps({'hero':'Andromeda Timmy','bones':len(rig.data.bones),'shared_hero_bones':19,'tail_bones':1,'tail_centered':True,'triangles':triangles,'body_renderers':1,'claw_renderers':2,'visor_renderers':1,'visor_opacity':VISOR_OPACITY,'visor_color':VISOR_COLOR,'surface_texture_resolution':1024,'fur_and_fabric_normal_map':True,'jet_nozzle_sockets':2,'runtime_scale':.70,'external_ears':False,'blue_emissive_back_stripe':True,'original_geometry':True},indent=2)+'\n')

# CPU rendering also works on build machines without a display/GPU.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24
scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.world.color=(.035,.045,.065)
scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast'
def aim(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.5,-4,2.0));camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=2.15;aim(camera,(0,0,.88));scene.camera=camera
for p,power,color in [((-3,-4,5),450,(.70,.86,1)),((3,-2,3),250,(1,.70,.42)),((1,3,4),500,(.20,.70,1))]:
    bpy.ops.object.light_add(type='AREA',location=p);light=bpy.context.object;light.data.energy=power;light.data.color=color;light.data.size=3;aim(light,(0,0,1))
scene.render.filepath=str(ART/'AndromedaTimmy-preview.png');bpy.ops.render.render(write_still=True)
if options.model_only or not options.prototype_icons:
    print('TIMMY MODEL BUILD COMPLETE:',triangles,'triangles,',len(rig.data.bones),'bones, 50% transparent visor')
    sys.exit(0)
OUT=ART/'PrototypeIcons';OUT.mkdir(parents=True,exist_ok=True)
for ob in [body,*weapons,visor,rig,*[o for o in bpy.data.objects if o.type=='EMPTY']]:ob.hide_render=True
camera.location=(0,-5,.25);camera.data.ortho_scale=2.2;aim(camera,(0,0,0))
all_icon_objects=[]
for slot in range(5):
    start=set(bpy.data.objects)
    # Full shaded illustration on a dark, opaque background; HUD supplies the frame.
    if slot in (0,3,4):
        ellipsoid('Astronaut paw',(0,.06,-.04),(.34,.14,.29),white)
        for i in range(3):
            x=(i-1)*.23
            tube('Luminous claw',[(x,-.10,.13),(x+.09,-.12,.43),(x+.16,-.11,.63)],.027,cyan)
            if slot!=4: tube('Scratch trail',[(x-.31,.10,-.60),(x-.10,.10,-.30),(x+.25,.10,.17)],.022 if slot==4 else .035,gold if slot==3 else cyan)
        if slot==3:
            ring('Single target grip',(0,.13,-.12),.53,.028,gold)
            ellipsoid('Captured target',(0,.08,-.14),(.18,.10,.20),navy)
    elif slot==1:
        ellipsoid('Stardust core',(0,0,.07),(.28,.17,.28),cyan)
        for i in range(8):
            a=i*math.tau/8;x=.53*math.cos(a);z=.53*math.sin(a)
            triangle('Bright star shard',[(x*.72,-.04,z*.72),(x*1.35,-.04,z*1.35),(x*.77+.09,-.04,z*.77-.08)],.065,gold)
        ring('Placed dust circle',(0,.14,0),.77,.026,cyan)
    else:
        box('Space boot',(0,0,.25),(.42,.25,.45),white)
        ellipsoid('Jet boot sole',(.03,-.08,-.01),(.28,.18,.10),navy)
        for i in range(3):
            x=(i-1)*.14
            tube('Jet flame',[(x,0,-.04),(x+.16,0,-.31),(x+.24,0,-.68)],.036,gold if i==1 else cyan)
        tube('Motion arc',[(-.7,.12,-.45),(-.45,.12,.15),(-.12,.12,.67),(.60,.12,.71)],.028,cyan)
    current=list(set(bpy.data.objects)-start);all_icon_objects.extend(current)
    scene.render.filepath=str(OUT/(str(slot)+'.png'));bpy.ops.render.render(write_still=True)
    for ob in current:ob.hide_render=True
for ob in all_icon_objects:ob.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'AndromedaTimmy-icons.blend'),compress=True)
print('TIMMY BUILD COMPLETE:',triangles,'triangles,',len(rig.data.bones),'bones, 5 attack icons')
