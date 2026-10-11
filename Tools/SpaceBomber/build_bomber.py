"""Blender 4.3+: visual-only replacement for the existing SpaceBomberFlyover.

Run: blender -b --python Tools/SpaceBomber/build_bomber.py
The Y-up mesh export uses the existing runtime palette and thruster sockets.
"""
import bpy
import gzip
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/SpaceBomber'
OUT = ROOT / 'Assets/Alterverse/Resources/AuthoredBomber'
ART.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
parts = []
materials = []
for name, color, metallic, roughness in [
    ('Armor', (.20, .235, .28), .66, .34),
    ('Dark', (.035, .045, .065), .48, .22),
    ('Energy', (.16, .88, 1), .18, .28),
]:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*color, 1)
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = roughness
    if name == 'Energy':
        p.inputs['Emission Color'].default_value = (*color, 1)
        p.inputs['Emission Strength'].default_value = 1.8
    materials.append(m)

def U(p):
    return Vector((p[0], p[2], p[1]))

def finish(o, name, material=0, bevel=0, smooth=False):
    o.name = name
    o.data.materials.append(materials[material])
    o['runtime_material'] = material
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel:
        b = o.modifiers.new('Machined edge chamfer', 'BEVEL')
        b.width = bevel
        b.segments = 2
        bpy.ops.object.modifier_apply(modifier=b.name)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    if smooth:
        for p in o.data.polygons:
            p.use_smooth = True
    parts.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    return o

def mesh(name, vertices, faces, material=0, bevel=0, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata([U(v) for v in vertices], [], faces)
    data.update()
    o = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(o)
    return finish(o, name, material, bevel, smooth)

def box(name, pos, size, material=0, bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(pos))
    o = bpy.context.object
    o.scale = U(size)
    return finish(o, name, material, bevel)

def rod(name, a, b, radius=.009, material=1, sides=8):
    a, b = U(a), U(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius,
        depth=(b-a).length, location=(a+b)/2)
    o = bpy.context.object
    o.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return finish(o, name, material)

def loft(name, rings, material=0, sides=16):
    # Rings: forward position, horizontal radius, vertical radius, center height.
    vertices = [(math.cos(i*math.tau/sides)*rx, cy+math.sin(i*math.tau/sides)*ry, z)
        for z, rx, ry, cy in rings for i in range(sides)]
    faces = [tuple(reversed(range(sides))), tuple(range((len(rings)-1)*sides, len(rings)*sides))]
    for r in range(len(rings)-1):
        for i in range(sides):
            a = r*sides+i
            b = r*sides+(i+1)%sides
            faces.append((a, b, b+sides, a+sides))
    return mesh(name, vertices, faces, material, smooth=True)

def wing(name, outline, y, thickness, material=0, bevel=.012):
    vertices = [(x, y+h, z) for h in [-thickness/2, thickness/2] for x, z in outline]
    n = len(outline)
    faces = [tuple(reversed(range(n))), tuple(range(n, n*2))]
    faces += [(i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n)]
    return mesh(name, vertices, faces, material, bevel)

# Continuous, tapered aircraft body instead of intersecting capsules and boxes.
loft('Tapered armored fuselage', [(-1.59,.10,.10,.00),(-1.33,.24,.17,.015),
    (-.65,.31,.24,.00),(.12,.34,.27,.005),(.88,.29,.23,.01),
    (1.49,.21,.16,.00),(1.93,.075,.07,-.035),(2.08,.008,.012,-.05)])
loft('Nose sensor fairing', [(1.67,.105,.075,-.07),(1.92,.065,.05,-.07),
    (2.085,.008,.012,-.05)], 1, 12)
loft('Raised cockpit frame', [(.43,.17,.055,.245),(.65,.23,.085,.305),
    (1.06,.20,.075,.29),(1.32,.105,.035,.21)], 1, 10)
loft('Faceted cyan canopy', [(.49,.13,.038,.345),(.68,.19,.068,.402),
    (1.04,.164,.061,.38),(1.26,.077,.027,.295)], 2, 8)
rod('Canopy central frame', (0,.469,.67),(0,.44,1.03), .014)
for z, y, width in [(.65,.455,.17),(1.05,.423,.15)]:
    rod('Cockpit cross frame',(-width,y-.035,z),(0,y+.015,z),.011)
    rod('Cockpit cross frame',(0,y+.015,z),(width,y-.035,z),.011)
for side in [-1,1]:
    s = 'L' if side < 0 else 'R'
    outline = [(side*.27,.69),(side*.70,.40),(side*2.08,-.36),
        (side*2.11,-.61),(side*1.82,-.76),(side*.34,-.40)]
    wing('Swept tapered wing '+s, outline, -.018, .085)
    wing('Trailing control surface '+s, [(side*.74,-.34),(side*1.84,-.62),
        (side*1.82,-.716),(side*.74,-.43)], .037, .014, 1, .004)
    wing('Wing service panel '+s, [(side*.69,.245),(side*1.24,-.055),
        (side*1.22,-.215),(side*.62,.02)], .033, .015, 1, .006)
    wing('Service panel inset '+s, [(side*.72,.214),(side*1.19,-.053),
        (side*1.17,-.176),(side*.66,.024)], .043, .012, 0, .006)
    rod('Recessed wing light '+s,(side*1.62,.037,-.59),(side*1.99,.037,-.47),.012,2)
    for x, z in [(.69,.145),(1.13,-.082),(.88,-.34),(1.57,-.55)]:
        rod('Flush wing fastener '+s,(side*x,.054,z),(side*x,.062,z),.014,0,6)
    # Engine shells, actual hollow nozzle lip and individually visible turbine fins.
    rings = [(-1.64,.145,.145,.02),(-1.49,.20,.19,.02),
        (-.92,.215,.19,.02),(-.55,.17,.15,.02)]
    o = loft('Engine nacelle '+s,rings,1,20)
    o.location.x = side*.54
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=False,scale=False);o.select_set(False)
    for z, radius in [(-1.62,.16),(-1.45,.197),(-.61,.175)]:
        bpy.ops.mesh.primitive_torus_add(major_radius=radius,minor_radius=.022,
            major_segments=20,minor_segments=6,location=U((side*.54,.02,z)),rotation=(math.pi/2,0,0))
        finish(bpy.context.object,'Nozzle reinforcing ring '+s,0)
    for i in range(10):
        t=i*math.tau/10
        rod('Turbine nozzle vane '+s,(side*.54+math.cos(t)*.11,.02+math.sin(t)*.11,-1.64),
            (side*.54+math.cos(t+.23)*.135,.02+math.sin(t+.23)*.135,-1.57),.009,0,5)
    # Top-mounted intakes have depth, a dark throat and separate grille blades.
    box('Intake frame '+s,(side*.38,.16,-.13),(.20,.12,.42),0,.025)
    box('Recessed intake throat '+s,(side*.38,.225,-.12),(.13,.012,.33),1,.012)
    for z in [-.24,-.16,-.08,0]:
        box('Intake louvre '+s,(side*.38,.243,z),(.14,.023,.018),0,.005)
    # Canted twin fins: sculpted profiles, inset dark rudders and edge markings.
    tip = side*.59
    vertices=[(side*.34,.06,-1.53),(side*.40,.66,-1.49),
        (tip,.73,-1.13),(side*.44,.14,-.89)]
    v=vertices+[(x+side*.07,y,z) for x,y,z in vertices]
    mesh('Canted tail fin '+s,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),
        (1,2,6,5),(2,3,7,6),(3,0,4,7)],0,.009)
    rod('Tail navigation edge '+s,(side*.42,.645,-1.47),(tip,.715,-1.13),.009,2)
    wing('Horizontal tailplane '+s,[(side*.23,-1.05),(side*.96,-1.36),
        (side*.92,-1.55),(side*.21,-1.49)],.025,.047,0,.01)
    box('Closed bomb bay door '+s,(side*.13,-.274,-.15),(.23,.043,1.00),1,.013)
    for z in [-.49,.15]:
        box('Bomb bay hinge '+s,(side*.245,-.27,z),(.055,.04,.10),0,.008)
for z in [-.43,-.69,-.95]:
    box('Dorsal maintenance panel',(0,.239,z),(.34,.045,.19),0,.016)
    box('Dorsal panel recessed joint',(0,.265,z-.063),(.24,.008,.013),1,.003)
rod('Dorsal aerial',(0,.26,-.35),(0,.43,-.43),.017,1)
box('Dorsal sensor base',(0,.279,-.20),(.105,.075,.20),1,.015)
box('Status light',(0,.322,-.20),(.05,.018,.09),2,.008)

# Retain the exact local socket positions and unit-sphere scale contract expected
# by PulseEngines(). These meshes are exported in their own local frame.
for side in [-1,1]:
    name='ThrusterL' if side<0 else 'ThrusterR'
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.5)
    o=finish(bpy.context.object,name,2,smooth=True)
    o['socket_position']=[side*.54,.02,-1.78]
    o['socket_scale']=[.18,.18,.34]

def export_group(name, objects, material, position=(0,0,0), scale=(1,1,1)):
    vertices=[];normals=[];uv=[];triangles=[];lookup={}
    for o in objects:
        data=o.data;data.calc_loop_triangles()
        for tri in data.loop_triangles:
            rounded = [Vector(tuple(round(a,6) for a in o.matrix_world@data.vertices[data.loops[li].vertex_index].co)) for li in tri.loops]
            if (rounded[1]-rounded[0]).cross(rounded[2]-rounded[0]).length < 1e-10:
                continue
            face=[]
            for li in tri.loops:
                p=o.matrix_world@data.vertices[data.loops[li].vertex_index].co
                n=(o.matrix_world.to_3x3().inverted().transposed()@data.corner_normals[li].vector).normalized()
                tex=data.uv_layers.active.data[li].uv
                key=tuple(round(a,6) for a in (p.x,p.z,p.y,n.x,n.z,n.y,*tex))
                if key not in lookup:
                    lookup[key]=len(vertices)//3
                    vertices.extend(key[:3]);normals.extend(key[3:6]);uv.extend(key[6:])
                face.append(lookup[key])
            triangles.extend((face[0],face[2],face[1]))
    return dict(name=name,material=material,position=list(position),scale=list(scale),
        vertices=vertices,normals=normals,uv=uv,triangles=triangles)

records=[]
for i,name in enumerate(['Hull','Mechanics','Energy']):
    records.append(export_group(name,[o for o in parts if 'socket_position' not in o and o['runtime_material']==i],i))
for o in parts:
    if 'socket_position' in o:
        records.append(export_group(o.name,[o],2,o['socket_position'],o['socket_scale']))
with gzip.GzipFile(filename=str(OUT/'Model.bytes'),mode='wb',mtime=0) as f:
    f.write(json.dumps(dict(version=1,parts=records),separators=(',',':')).encode())

# Source objects stay individually editable; the live export is only five meshes.
for o in parts:
    if 'socket_position' in o:
        o.location=U(o['socket_position']);o.scale=U(o['socket_scale'])
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=str(ART/'SpaceBomber.fbx'),use_selection=True,
    object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y')
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Bomber studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.035,.05,.08,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
scene.view_settings.view_transform='AgX'
scene.render.image_settings.compression=85
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in [((3,4,7),1000,5),((-4,-1,3),950,4),((0,-4,4),1300,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos)
    o=bpy.context.object;o.data.energy=power;o.data.size=size;aim(o,(0,0,0))
bpy.ops.object.camera_add(location=(5.5,6.5,5.4))
cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=5.8;aim(cam,(0,.05,.03));scene.camera=cam
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'SpaceBomber.blend'),compress=True)
scene.render.filepath=str(ART/'SpaceBomberPreview.png');bpy.ops.render.render(write_still=True)
cam.location=(0,.2,7);aim(cam,(0,0,0));scene.render.filepath=str(ART/'SpaceBomberTop.png');bpy.ops.render.render(write_still=True)
report=dict(blender=bpy.app.version_string,sourceObjects=len(parts),runtimeMeshes=len(records),
    vertices=sum(len(r['vertices'])//3 for r in records),triangles=sum(len(r['triangles'])//3 for r in records),
    parts=[dict(name=r['name'],vertices=len(r['vertices'])//3,triangles=len(r['triangles'])//3) for r in records],
    rootScale=.78,forward='+Z',thrusterSocketsUnchanged=True,gameplayChanged=False)
(ART/'geometry-report.json').write_text(json.dumps(report,indent=2)+'\n')
print('BOMBER_EXPORT',json.dumps(report),flush=True)
