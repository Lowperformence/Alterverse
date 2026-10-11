"""Original Season 2 cat astronaut, matching the existing 19-bone authored-hero rig.
Run: blender --background --python-exit-code 1 --python Tools/AndromedaTimmy/build_timmy.py
Exports editable Blender source, Unity FBX, studio preview and five modeled ability icons.
All geometry/materials are original project work; no human base mesh is required.
"""
from pathlib import Path
import math, json, argparse, sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Heroes/AndromedaTimmy'
OUT = ROOT / 'Assets/Alterverse/Resources/AndromedaTimmy/Icons'
FBX = ROOT / 'Assets/Alterverse/Resources/AuthoredHeroes/Timmy.fbx'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--model-only',action='store_true',help='Rebuild model/preview without rewriting attack icons.')
options=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
VISOR_OPACITY=.30 # 70% transparent; a single curved surface avoids doubled tint.
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
fur=material('Timmy orange tabby fur',(.72,.31,.095))
cream=material('Timmy muzzle cream',(.91,.77,.52))
cyan=material('Timmy cyan energy',(.04,.68,1),.45,2)
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
def ellipsoid(name,p,s,mat,bone=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=p)
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
def visor_dome():
    # A circular spherical cap fitted inside the rim, in front of muzzle/whiskers.
    # Negative Y faces forward in the authored rig. Wind only the outside surface.
    radius=.215;depth=.17;center=(0,-.167,1.55);segments=48;rings=12
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

# Compact, unmistakably feline astronaut: fur face/ears/tail remain visible.
ellipsoid('Suit torso',(0,0,1.11),(.23,.16,.30),white,'Chest')
ellipsoid('Flexible waist',(0,0,.86),(.20,.145,.12),navy,'Hips')
box('Cyan chest display',(0,-.157,1.12),(.12,.028,.10),cyan,'Chest')
box('Oxygen backpack',(0,.16,1.15),(.26,.14,.31),navy,'Chest')
for side,k in ((-1,'L'),(1,'R')):
    ellipsoid('Jet housing'+k,(side*.13,.245,1.12),(.075,.07,.15),white,'Chest')
    ellipsoid('Jet nozzle'+k,(side*.13,.255,.99),(.043,.04,.025),cyan,'Chest')
ellipsoid('Helmet back',(0,.035,1.54),(.235,.17,.23),white,'Head')
ellipsoid('Cat head',(0,-.086,1.53),(.175,.135,.16),fur,'Head')
ellipsoid('Cream muzzle',(0,-.213,1.48),(.10,.028,.06),cream,'Head')
triangle('Cat nose',[(-.020,-.244,1.50),(.020,-.244,1.50),(0,-.247,1.48)],.014,navy,'Head')
ring('Visor rim',(0,-.167,1.55),.215,.016,white,'Head')
visor=visor_dome()
for side,k in ((-1,'L'),(1,'R')):
    triangle('Feline ear'+k,[(side*.065,-.025,1.63),(side*.18,-.01,1.78),(side*.19,-.015,1.60)],.075,fur,'Head')
    triangle('Ear lining'+k,[(side*.097,-.066,1.635),(side*.166,-.063,1.73),(side*.172,-.063,1.63)],.009,cream,'Head')
    ellipsoid('Eye'+k,(side*.07,-.21,1.56),(.044,.014,.025),eye,'Head')
    ellipsoid('Vertical pupil'+k,(side*.07,-.225,1.56),(.009,.006,.023),navy,'Head')
    for i in range(3):
        tube('Whisker'+k+str(i),[(side*.065,-.238,1.48),(side*.14,-.239,1.48+(i-1)*.018)],.002,cream,'Head')
    for i in range(2):
        tube('Tabby cheek stripe'+k+str(i),[(side*.125,-.188,1.51+i*.025),(side*.151,-.176,1.49+i*.025)],.008,navy,'Head')
    ellipsoid('Shoulder'+k,(side*.26,0,1.28),(.12,.115,.13),white,'UpperArm'+k)
    ellipsoid('Upper suit arm'+k,(side*.29,-.01,1.15),(.08,.075,.17),white,'UpperArm'+k)
    ellipsoid('Elbow seal'+k,(side*.315,-.018,1.01),(.07,.073,.075),navy,'Forearm'+k)
    ellipsoid('Forearm gauntlet'+k,(side*.335,-.04,.91),(.082,.085,.125),white,'Forearm'+k)
    ellipsoid('Paw'+k,(side*.35,-.072,.79),(.087,.075,.083),fur,'Hand'+k)
    for i in range(3):
        x=side*.35+(i-1)*.038
        tube('Claw'+k+str(i),[(x,-.115,.79),(x,-.175,.775),(x,-.19,.745)],.006,cyan,'WeaponGrip'+k)
    ellipsoid('Thigh suit'+k,(side*.13,0,.66),(.095,.09,.23),white,'Thigh'+k)
    ellipsoid('Knee seal'+k,(side*.14,-.02,.46),(.079,.073,.075),navy,'Shin'+k)
    ellipsoid('Shin suit'+k,(side*.14,0,.29),(.08,.072,.16),white,'Shin'+k)
    ellipsoid('Round space boot'+k,(side*.14,-.045,.095),(.105,.145,.085),navy,'Foot'+k)
    box('Boot toe cap'+k,(side*.14,-.14,.11),(.16,.10,.075),white,'Foot'+k)
tail=[(0,.12,.86),(.10,.25,.82),(.22,.30,.72),(.31,.28,.66),(.37,.23,.72),(.38,.20,.81)]
tube('Curved tabby tail',tail,.04,fur,'Hips')
for i,p in enumerate(tail[1::2]):ellipsoid('Tail stripe'+str(i),p,(.043,.04,.045),navy,'Hips')

data=bpy.data.armatures.new('DF_Skeleton');rig=bpy.data.objects.new('DF_Skeleton',data)
bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(n,h,t,parent=None):
    b=data.edit_bones.new(n);b.head=h;b.tail=t
    if parent:b.parent=bones[parent]
    bones[n]=b
bone('Root',(0,0,0),(0,0,.15));bone('Hips',(0,0,.86),(0,0,1.02),'Root')
bone('Spine',(0,0,1.02),(0,0,1.20),'Hips');bone('Chest',(0,0,1.20),(0,0,1.42),'Spine');bone('Head',(0,0,1.42),(0,0,1.75),'Chest')
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
weapons=[]
for k in ('L','R'):weapons.append(join(claw_groups[k],'DF_Weapon'+k))
for k in ('L','R'):
    for name,parent in (('DF_PalmGrip','Hand'),('DF_HandleContact','WeaponGrip')):
        socket=bpy.data.objects.new(name+k,None);bpy.context.collection.objects.link(socket)
        socket.parent=rig;socket.parent_type='BONE';socket.parent_bone=parent+k
        bpy.context.view_layer.update();world=socket.matrix_world.copy()
        world.translation=Vector(((-.35 if k=='L' else .35),-.072,.79));socket.matrix_world=world
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'AndromedaTimmy.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(FBX),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True)
triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in [body,*weapons,visor])
(ART/'model-report.json').write_text(json.dumps({'hero':'Andromeda Timmy','bones':len(rig.data.bones),'triangles':triangles,'body_renderers':1,'claw_renderers':2,'visor_renderers':1,'visor_opacity':VISOR_OPACITY,'visor_color':VISOR_COLOR,'runtime_scale':.70,'original_geometry':True},indent=2)+'\n')

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
if options.model_only:
    print('TIMMY MODEL BUILD COMPLETE:',triangles,'triangles,',len(rig.data.bones),'bones, 70% transparent visor')
    sys.exit(0)
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
