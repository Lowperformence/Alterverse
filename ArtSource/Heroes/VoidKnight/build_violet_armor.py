"""V4: concept-matched violet armor and a closed V-visor helmet.

Blender 5.0.1: start with the current modular source; keep all bones, actions,
hands and weapon unchanged. Separate editable objects use the same five materials.
"""
from pathlib import Path
import bpy, bmesh, math, json, shutil
from mathutils import Vector

HERE = Path(__file__).resolve().parent
SOURCE = HERE / 'VoidKnight.blend'
BASELINE = HERE / '_violet_baseline.blend'
shutil.copy2(SOURCE, BASELINE)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
rig.data.pose_position = 'REST'
bpy.context.view_layer.update()
black, edge, cloth, energy, core = [bpy.data.materials[n] for n in
    ['Void black metal', 'Void bevel metal', 'Void woven undersuit', 'Void energy', 'Void core energy']]
created = []

def mesh(name, vertices, faces, material, bone, bevel=.0015, frame=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata([frame(v) if frame else v for v in vertices], [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = rig; obj['bone'] = bone; obj['design_version'] = 4
    data.materials.append(material)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    if bevel:
        modifier = obj.modifiers.new('Machined micro bevel', 'BEVEL')
        modifier.width = bevel; modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        for polygon in data.polygons: polygon.use_smooth = True
        data.set_sharp_from_angle(angle=math.radians(42))
        modifier = obj.modifiers.new('Weighted plate normals', 'WEIGHTED_NORMAL')
        modifier.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(data.vertices))), 1.0, 'REPLACE')
    modifier = obj.modifiers.new('Original animation rig', 'ARMATURE')
    modifier.object = rig
    uv = data.uv_layers.new(name='SurfaceUV')
    for poly in data.polygons:
        dominant = max(range(3), key=lambda i: abs(poly.normal[i]))
        axes = [i for i in range(3) if i != dominant]
        for index in poly.loop_indices:
            co = data.vertices[data.loops[index].vertex_index].co
            uv.data[index].uv = (co[axes[0]]*4, co[axes[1]]*4)
    created.append(obj)
    return obj

def plate(name, outline, front, depth, material, bone, bevel=.0015, frame=None, ridge=0):
    n = len(outline)
    ys = list(front) if isinstance(front, (list, tuple)) else [front]*n
    vertices = [(x,y,z) for (x,z),y in zip(outline,ys)]
    vertices += [(x,y+depth,z) for (x,z),y in zip(outline,ys)]
    faces = []
    if ridge:
        vertices.append((sum(x for x,z in outline)/n, sum(ys)/n-ridge, sum(z for x,z in outline)/n))
        faces += [(i,(i+1)%n,2*n) for i in range(n)]
    else: faces.append(tuple(range(n)))
    faces.append(tuple(range(n,2*n)))
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vertices,faces,material,bone,bevel,frame)

def ribbon(name, points, width, material, bone, frame=None, depth=.003):
    # Continuous physical inlay with a closed back and sidewalls.
    front = []
    for i, point in enumerate(points):
        delta = Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        side = Vector((delta.z,0,-delta.x)).normalized()*width*.5
        front += [Vector(point)+side, Vector(point)-side]
    count = len(front)
    vertices = front+[p+Vector((0,depth,0)) for p in front]
    faces = []
    for i in range(len(points)-1):
        a=2*i; b=a+2
        faces += [(a,b,b+1,a+1),(count+a+1,count+b+1,count+b,count+a),
                  (a,count+a,count+b,b),(a+1,b+1,count+b+1,count+a+1)]
    faces += [(0,1,count+1,count),(count-2,2*count-2,2*count-1,count-1)]
    return mesh(name,vertices,faces,material,bone,0,frame)

def loft(name, rings, material, bone, sides=16):
    vertices = []
    for z,rx,ry,cx,cy in rings:
        for i in range(sides):
            a=2*math.pi*i/sides
            vertices.append((cx+rx*math.cos(a),cy+ry*math.sin(a),z))
    faces=[tuple(range(sides-1,-1,-1))]
    for j in range(len(rings)-1):
        for i in range(sides): faces.append((j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i))
    faces.append(tuple(range((len(rings)-1)*sides,len(rings)*sides)))
    return mesh(name,vertices,faces,material,bone,.0015)

def bone_frame(name):
    bone=rig.data.bones[name]; head=bone.head_local.copy()
    d=(bone.tail_local-head).normalized(); u=Vector((1,0,0))
    u=(u-d*u.dot(d)).normalized(); v=u.cross(d).normalized()
    return lambda p: head+u*p[0]+v*p[1]+d*p[2], (bone.tail_local-head).length

# Replace the complete old crowned helmet, retaining the neck understructure.
# Remove obsolete fine inlays where the concept calls for one readable light panel.
remove_prefixes=('VK3_ShoulderSignal_', 'VK2_BracerInlay_', 'VK2_ShinLight_',
                 'VK2_VambraceFace_', 'VK3_VambraceLame_', 'VK3_VambraceLip_',
                 'VK2_ShinRidge_', 'VK3_GreaveFlute_', 'VK2_ThighLight_', 'VK3_ChestInset_')
for obj in list(bpy.data.objects):
    if obj.type!='MESH': continue
    if (obj.get('bone')=='Head' and obj.name!='VK_Neck') or obj.name.startswith(remove_prefixes):
        bpy.data.objects.remove(obj,do_unlink=True)

# Closed angular dome: shaped perimeter rings, a small sagittal crest, segmented
# brow, split cheeks and an overlapping pointed chin. No ornamental horns.
loft('VK4_HelmetSkull',[(1.608,.084,.091,0,.007),(1.669,.126,.124,0,.010),
     (1.751,.143,.141,0,.008),(1.819,.128,.125,0,.009),
     (1.866,.087,.092,0,.012),(1.889,.027,.039,0,.013)],black,'Head',20)
plate('VK4_SagittalCrest',[(-.016,1.826),(-.018,1.868),(0,1.897),(.018,1.868),(.016,1.826)],
      [-.108,-.074,-.034,-.074,-.108],.023,black,'Head',.0015,ridge=.004)
plate('VK4_Forehead',[(-.121,1.791),(-.093,1.846),(0,1.872),(.093,1.846),
      (.121,1.791),(.076,1.754),(0,1.728),(-.076,1.754)],
      [-.106,-.128,-.143,-.128,-.106,-.181,-.204,-.181],.014,black,'Head',.002,ridge=.009)
for side,label in [(-1,'L'),(1,'R')]:
    sx=lambda p:[(side*x,z) for x,z in p]
    # Wide neon V visor. Keep original eye object names for visibility regression.
    points=[(side*.122,-.139,1.763),(side*.073,-.194,1.741),(side*.004,-.222,1.716)]
    ribbon('VK4_VisorGasket_'+label,[(x,y+.003,z) for x,y,z in points],.034,cloth,'Head',depth=.005)
    ribbon('VK_Eye_'+label,points,.017,energy,'Head',depth=.004)
    ribbon('VK4_VisorHighlight_'+label,[(x,y-.0008,z+.003) for x,y,z in points],.003,core,'Head',depth=.001)
    ribbon('VK4_BrowRail_'+label,[(side*.124,-.140,1.789),(side*.075,-.195,1.766),(side*.006,-.218,1.741)],.014,black,'Head',depth=.007)
    plate('VK4_CheekShell_'+label,sx([(.008,1.699),(.074,1.722),(.129,1.744),
          (.142,1.697),(.104,1.628),(.029,1.592),(.009,1.638)]),
          [-.224,-.201,-.149,-.123,-.147,-.201,-.221],.016,black,'Head',.002,ridge=.007)
    ribbon('VK4_CheekSeam_'+label,[(side*.112,-.163,1.710),(side*.084,-.191,1.663),(side*.033,-.217,1.627)],.004,edge,'Head')
    plate('VK4_JawGuard_'+label,sx([(.038,1.634),(.111,1.665),(.123,1.627),(.062,1.584),(.019,1.576)]),
          [-.207,-.154,-.126,-.179,-.210],.012,black,'Head',.0015,ridge=.005)
    # Ear discs have a separate rim and recessed dark center.
    center=Vector((side*.139,.002,1.744))
    ear_frame=lambda p,c=center,s=side: c+Vector((s*p[1],p[0],p[2]))
    disc=[(math.cos(i*math.tau/12)*.036,math.sin(i*math.tau/12)*.036) for i in range(12)]
    plate('VK4_EarRim_'+label,disc,.006,.012,edge,'Head',.001,ear_frame)
    plate('VK4_EarInset_'+label,[(x*.76,z*.76) for x,z in disc],.020,.003,black,'Head',.0008,ear_frame)
    plate('VK4_TemplePanel_'+label,sx([(.111,1.825),(.141,1.791),(.149,1.727),(.130,1.686),(.119,1.750)]),
          [-.045,-.039,-.026,-.027,-.072],.078,black,'Head',.0015)
    for i in range(3):
        z=1.650+i*.012
        ribbon('VK4_JawVent_'+label+'_'+str(i),[(side*.083,-.192,z),(side*.107,-.171,z+.010)],.003,cloth,'Head')
    plate('VK4_RearHelmetPanel_'+label,sx([(.010,1.866),(.084,1.858),(.128,1.794),(.113,1.675),(.048,1.622),(.012,1.659)]),
          [.100,.095,.090,.107,.117,.132],-.012,black,'Head',.002,ridge=-.007)
    ribbon('VK4_RearHelmetSeam_'+label,[(side*.024,.137,1.826),(side*.080,.130,1.793),(side*.096,.126,1.706)],.0035,edge,'Head',depth=-.002)
plate('VK4_ChinKeel',[(-.029,1.661),(0,1.679),(.029,1.661),(.018,1.598),(0,1.574),(-.018,1.598)],
      [-.214,-.227,-.214,-.220,-.227,-.220],.014,black,'Head',.0015,ridge=.006)

for side,label in [(-1,'L'),(1,'R')]:
    # Broad shoulder chevrons follow the three existing cap facets exactly.
    points=[(side*.245,-.146,1.539),(side*.330,-.177,1.494),(side*.407,-.129,1.526)]
    ribbon('VK4_ShoulderSocket_'+label,[(x,y+.001,z) for x,y,z in points],.043,cloth,'UpperArm'+label,depth=.007)
    ribbon('VK4_ShoulderV_'+label,[(x,y-.001,z) for x,y,z in points],.029,energy,'UpperArm'+label,depth=.004)
    ribbon('VK4_ShoulderVCore_'+label,[(x,y-.002,z+.004) for x,y,z in points],.004,core,'UpperArm'+label,depth=.001)
    # Matching smaller rear signal remains visible from the gameplay camera.
    ribbon('VK4_ShoulderRearV_'+label,[(side*.246,.141,1.495),(side*.317,.155,1.457),(side*.373,.134,1.479)],.012,energy,'UpperArm'+label,depth=-.003)
    frame,length=bone_frame('Forearm'+label)
    outline=[(-.057,length*.16),(.057,length*.16),(.068,length*.31),(.044,length*.86),(0,length*.97),(-.044,length*.86),(-.068,length*.31)]
    plate('VK4_VambraceFrame_'+label,outline,-.096,.022,edge,'Forearm'+label,.002,frame,ridge=.006)
    plate('VK4_VambracePlate_'+label,[(x*.91,(z-length*.5)*.94+length*.5) for x,z in outline],-.104,.018,black,'Forearm'+label,.002,frame,ridge=.006)
    blade=[(-.017,length*.26),(.020,length*.23),(.022,length*.39),(.008,length*.83),(-.006,length*.89),(-.015,length*.74)]
    plate('VK4_BracerSocket_'+label,[(x*1.25,z) for x,z in blade],-.118,.008,cloth,'Forearm'+label,.0005,frame)
    plate('VK4_BracerLight_'+label,blade,-.121,.004,energy,'Forearm'+label,.0006,frame,ridge=.001)
    ribbon('VK4_BracerCore_'+label,[(.007,-.124,length*.32),(0,-.124,length*.77)],.003,core,'Forearm'+label,frame,depth=.001)
    for sign in [-1,1]:
        ribbon('VK4_BracerEdge_'+label+str(sign),[(sign*.052,-.110,length*.32),(sign*.033,-.111,length*.80)],.0035,edge,'Forearm'+label,frame)
    frame,length=bone_frame('Shin'+label)
    outline=[(-.049,.106),(0,.087),(.049,.106),(.041,.265),(.020,.334),(0,.352),(-.020,.334),(-.041,.265)]
    plate('VK4_GreaveFrame_'+label,outline,-.094,.016,edge,'Shin'+label,.002,frame,ridge=.004)
    plate('VK4_GreavePlate_'+label,[(x*.84,(z-.22)*.96+.22) for x,z in outline],-.103,.014,black,'Shin'+label,.0015,frame,ridge=.004)
    blade=[(-.018,.121),(.013,.118),(.019,.148),(.010,.294),(0,.326),(-.010,.294)]
    plate('VK4_ShinSocket_'+label,[(x*1.3,z) for x,z in blade],-.113,.007,cloth,'Shin'+label,.0005,frame)
    plate('VK4_ShinLight_'+label,blade,-.116,.004,energy,'Shin'+label,.0006,frame,ridge=.001)
    ribbon('VK4_ShinCore_'+label,[(0,-.119,.143),(0,-.119,.290)],.003,core,'Shin'+label,frame,depth=.001)

# Material emission is shared by all inlays, never one light source per piece.
for material,color,strength in [(energy,(.31,.014,.76),2.2),(core,(.55,.09,1.0),2.6)]:
    material.diffuse_color=(*color,1)
    bsdf=material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Emission Color'].default_value=(*color,1)
    bsdf.inputs['Emission Strength'].default_value=strength
rig['design_version']=4
bpy.context.scene['void_knight_design']='V4 / violet chevrons / closed V-visor helmet'
rig.data.pose_position='POSE'
rig.animation_data.action=bpy.data.actions['Idle']
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
(HERE/'Validation').mkdir(exist_ok=True)
(HERE/'Validation/violet-created-parts.json').write_text(json.dumps([{'part':o.name,'bone':o['bone']} for o in created],indent=2)+'\n')
print('VIOLET_ARMOR_BUILT',len(created),flush=True)
