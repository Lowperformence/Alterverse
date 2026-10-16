"""Rebuild the modular V3 armor, PBR surfaces and shoulder-carried greatsword.

Run in Blender 5 Python. V2 is regenerated from its frozen original rig; V3 keeps
rest bones and hand proportions, and bakes a two-hand shoulder pose into the clips.
"""
from pathlib import Path
import bpy, bmesh, runpy, math, json
import numpy as np
from mathutils import Vector, Matrix
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
scope = runpy.run_path(str(HERE / 'redesign_void_knight.py'))
for name in ['rig', 'mesh', 'plate', 'loft', 'strip', 'bone_frame', 'mean', 'black', 'edge', 'cloth', 'energy', 'core', 'weapon_frame']:
    globals()[name] = scope[name]
rig.data.pose_position = 'REST'
bpy.context.view_layer.update()

# Remove the broad shoulder cap and weapon surfaces; replace them with layered shells.
remove = ('VK2_Pauldron', 'VK2_Shoulder', 'VK2_GuardShoulderLip', 'VK2_SwordBlade',
          'VK2_SwordFuller', 'VK2_BladeEtch', 'VK2_SweptGuard', 'VK2_GuardEdge',
          'VK2_GuardCore', 'VK2_Pectoral', 'VK2_ChestEdge', 'VK2_ChestLight')
for obj in list(bpy.data.objects):
    if obj.type == 'MESH' and obj.name.startswith(remove):
        bpy.data.objects.remove(obj, do_unlink=True)

# Preserve the original crowned identity, with less needle-like fins and a fuller jaw.
for obj in bpy.data.objects:
    if obj.type == 'MESH' and obj.name.startswith(('VK_Crown_', 'VK_CrownEdge_')):
        for v in obj.data.vertices:
            if v.co.z > 1.80: v.co.z = 1.80 + (v.co.z - 1.80) * .83
for side, label in [(-1, 'L'), (1, 'R')]:
    poly = [(side*x,z) for x,z in [(.035,1.711),(.123,1.735),(.149,1.686),(.115,1.593),(.034,1.550),(.054,1.626)]]
    plate('VK3_CheekFrame_'+label, poly, [-.195,-.163,-.139,-.151,-.187,-.201], .014, edge, 'Head', .0025, ridge=.008)
    center = Vector((side*.082,1.655))
    inner = [(center.x+(x-center.x)*.84,center.y+(z-center.y)*.88) for x,z in poly]
    plate('VK3_CheekPanel_'+label, inner, [-.200,-.169,-.144,-.157,-.192,-.206], .009, black, 'Head', .002, ridge=.008)
    for i in range(3):
        z = 1.643 + i*.024
        strip('VK3_JawVent_'+label+'_'+str(i),[(side*.085,-.205,z),(side*.115,-.185,z+.015)],.004,cloth,'Head')
    plate('VK3_Temple_'+label, [(side*x,z) for x,z in [(.120,1.81),(.157,1.79),(.162,1.697),(.137,1.669),(.128,1.730)]],-.111,.069,black,'Head',.003)

# Separate rimmed pectoral shells: one large quiet surface, a shaped border and a small light.
for side,label in [(-1,'L'),(1,'R')]:
    poly=[(side*x,z) for x,z in [(.017,1.484),(.119,1.525),(.245,1.465),(.225,1.350),(.090,1.292),(.024,1.334)]]
    front=[-.202,-.184,-.132,-.153,-.210,-.221]
    plate('VK3_CuirassRim_'+label,poly,front,.025,edge,'Chest',.004,ridge=.020)
    cx,cz=side*.119,1.408
    inner=[(cx+(x-cx)*.92,cz+(z-cz)*.92) for x,z in poly]
    plate('VK3_Cuirass_'+label,inner,[y-.005 for y in front],.018,black,'Chest',.003,ridge=.021)
    strip('VK3_Clavicle_'+label,[(side*.035,-.226,1.467),(side*.109,-.209,1.493),(side*.192,-.167,1.459)],.006,black,'Chest')
    strip('VK3_ChestInset_'+label,[(side*.073,-.234,1.357),(side*.139,-.213,1.388),(side*.191,-.184,1.418)],.0035,energy,'Chest')
    # Layered shoulder cap, articulated lower lames and a rear counter-plate.
    cx=side*.305
    loft('VK3_PauldronShell_'+label,[(1.427,.116,.116,cx,0),(1.486,.159,.139,cx,0),(1.552,.134,.121,cx,0),(1.603,.043,.049,cx,0)],black,'UpperArm'+label,sides=16,bevel=.0035)
    outline=[(side*x,z) for x,z in [(.211,1.549),(.291,1.587),(.420,1.536),(.436,1.482),(.330,1.457),(.224,1.484)]]
    ys=[-.081,-.101,-.088,-.101,-.144,-.130]
    plate('VK3_PauldronRim_'+label,outline,ys,.020,edge,'UpperArm'+label,.003,ridge=.012)
    pc=Vector((side*.319,1.522));inner=[(pc.x+(x-pc.x)*.91,pc.y+(z-pc.y)*.86) for x,z in outline]
    plate('VK3_PauldronCrown_'+label,inner,[y-.006 for y in ys],.015,black,'UpperArm'+label,.003,ridge=.014)
    for i in range(2):
        z=1.449-i*.055
        poly=[(side*x,z+dz) for x,dz in [(.239,.019),(.338,.036),(.424,.012),(.420,-.027),(.344,-.045),(.260,-.026)]]
        plate('VK3_ShoulderLame_'+label+'_'+str(i),poly,[-.109,-.148,-.101,-.110,-.149,-.120],.022,black,'UpperArm'+label,.003,ridge=.008)
        strip('VK3_ShoulderLip_'+label+'_'+str(i),[(side*.269,-.130,z-.020),(side*.344,-.161,z-.036),(side*.402,-.129,z-.020)],.005,edge,'UpperArm'+label)
    strip('VK3_ShoulderSignal_'+label,[(side*.282,-.159,1.521),(side*.333,-.162,1.504),(side*.384,-.137,1.520)],.004,energy,'UpperArm'+label)
    plate('VK3_ScapularGuard_'+label,[(side*x,z) for x,z in [(.203,1.499),(.291,1.549),(.402,1.502),(.379,1.416),(.259,1.434)]],.105,.024,black,'UpperArm'+label,.003,ridge=-.010)
    frame,length=bone_frame('Forearm'+label)
    for i in range(2):
        z=length*(.27+i*.24)
        plate('VK3_VambraceLame_'+label+'_'+str(i),[(-.053,z),(.053,z),(.049,z+.041),(0,z+.055),(-.049,z+.041)],-.108,.014,black,'Forearm'+label,.0025,frame,ridge=.005)
        strip('VK3_VambraceLip_'+label+'_'+str(i),[(-.041,-.116,z+.007),(0,-.122,z+.019),(.041,-.116,z+.007)],.004,edge,'Forearm'+label,frame)
    frame,length=bone_frame('UpperArm'+label)
    plate('VK3_ElbowWing_'+label,[(-.071,length*.86),(0,length*.81),(.071,length*.86),(.083,length*1.01),(0,length*1.06),(-.083,length*1.01)],-.074,.024,black,'UpperArm'+label,.003,frame,ridge=.012)
    # Narrow stacked side tassets preserve the split hip/leg articulation.
    for i in range(2):
        z=.932-i*.065
        plate('VK3_Tasset_'+label+'_'+str(i),[(side*x,z+dz) for x,dz in [(.155,.028),(.217,.021),(.253,-.012),(.241,-.064),(.193,-.082),(.164,-.047)]],-.095,.036,black,'Thigh'+label,.0035,ridge=.007)
    frame,length=bone_frame('Shin'+label)
    knee=[(-.078,-.022),(0,-.052),(.078,-.022),(.084,.035),(.041,.091),(0,.110),(-.041,.091),(-.084,.035)]
    plate('VK3_KneeRim_'+label,knee,-.105,.014,edge,'Shin'+label,.0025,frame,ridge=.011)
    inner=[(x*.85,(z-.024)*.85+.024) for x,z in knee]
    plate('VK3_KneeShield_'+label,inner,-.111,.010,black,'Shin'+label,.0025,frame,ridge=.015)
    for sign in [-1,1]:
        plate('VK3_GreaveFlute_'+label+str(sign),[(sign*.044,.118),(sign*.072,.131),(sign*.059,.278),(sign*.031,.332),(sign*.027,.284)],-.080,.014,black,'Shin'+label,.002,frame,ridge=.005)

# Rear armor is visible in the gameplay camera too: a split occipital shell,
# articulated lumbar lames and fitted rear hip protection cover the large fabric lobes.
for side,label in [(-1,'L'),(1,'R')]:
    plate('VK3_Occipital_'+label,[(side*x,z) for x,z in [(.010,1.858),(.093,1.859),(.141,1.793),(.119,1.661),(.041,1.613),(.013,1.696)]],[.105,.098,.087,.106,.124,.137],-.018,black,'Head',.003,ridge=-.010)
    strip('VK3_RearHelmetSeam_'+label,[(side*.026,.150,1.824),(side*.080,.139,1.807),(side*.102,.137,1.714)],.005,edge,'Head')
    plate('VK3_RearFauld_'+label,[(side*x,z) for x,z in [(.012,1.007),(.158,1.020),(.207,.972),(.177,.886),(.071,.870),(.020,.929)]],.141,-.027,black,'Hips',.003,ridge=-.012)
    frame,length=bone_frame('Shin'+label)
    plate('VK3_CalfPanel_'+label,[(-.047,.085),(.047,.085),(.056,.169),(.037,.306),(0,.339),(-.037,.306),(-.056,.169)],.076,-.019,black,'Shin'+label,.0025,frame,ridge=-.008)
    for i in range(3):
        z=.140+i*.040
        strip('VK3_CalfVent_'+label+'_'+str(i),[(-.027,.091,z),(0,.096,z+.011),(.027,.091,z)],.004,cloth,'Shin'+label,frame)
for i in range(3):
    z=1.080+i*.053;w=.161+i*.008
    plate('VK3_LumbarLame_'+str(i),[(-w,z+.036),(0,z+.045),(w,z+.036),(w*.92,z-.002),(0,z-.013),(-w*.92,z-.002)],.151,-.025,black,'Spine',.0025,ridge=-.009)
    strip('VK3_LumbarLip_'+str(i),[(-w*.82,.170,z+.028),(0,.176,z+.037),(w*.82,.170,z+.028)],.004,edge,'Spine')

# A forged cross section, recessed fuller, steel cutting facets and a restrained violet inlay.
section=[(-1,0),(-.90,.32),(-.69,.79),(-.24,1),(-.12,.66),(.12,.66),(.24,1),(.69,.79),(.90,.32),(1,0),(.90,-.32),(.69,-.79),(.24,-1),(.12,-.66),(-.12,-.66),(-.24,-1),(-.69,-.79),(-.90,-.32)]
rings=[(.212,.046,.026),(.326,.079,.030),(.424,.137,.034),(.570,.129,.034),(1.145,.081,.027),(1.377,.002,.001)]
verts=[(x*w,y*t,z) for z,w,t in rings for x,y in section]
n=len(section);faces=[tuple(range(n-1,-1,-1))];face_mats=[0]
for j in range(len(rings)-1):
    for i in range(n):
        faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
        face_mats.append(1 if i in [0,1,7,8,9,10,16,17] else 0)
faces.append(tuple(range((len(rings)-1)*n,len(rings)*n)));face_mats.append(0)
mesh('VK3_GreatswordBlade',verts,faces,[black,edge],'WeaponGripR',.001,weapon_frame,face_mats)
for sign in [-1,1]:
    poly=[(0,.360),(.005,.429),(.004,1.125),(0,1.283),(-.004,1.125),(-.005,.429)]
    plate('VK3_RecessedFuller_'+str(sign),poly,sign*.024,-sign*.002,energy,'WeaponGripR',.0005,weapon_frame)
    for i in range(3):
        z=.558+i*.184
        strip('VK3_ForgedSigil_'+str(sign)+'_'+str(i),[(-.044,sign*.030,z),(-.064,sign*.026,z+.023),(-.044,sign*.030,z+.046)],.003,edge,'WeaponGripR',weapon_frame)
    poly=[(0,.238),(sign*.094,.279),(sign*.188,.245),(sign*.264,.307),(sign*.247,.201),(sign*.161,.174),(sign*.068,.199),(0,.187)]
    plate('VK3_QuillonRim_'+str(sign),poly,-.038,.076,edge,'WeaponGripR',.004,weapon_frame)
    inner=[(x*.94,(z-.235)*.73+.235) for x,z in poly]
    plate('VK3_Quillon_'+str(sign),inner,-.043,.086,black,'WeaponGripR',.003,weapon_frame,ridge=.005)
    plate('VK3_GuardSocket_'+str(sign),[(0,.290),(.054,.226),(0,.155),(-.054,.226)],sign*.049,-sign*.018,black,'WeaponGripR',.003,weapon_frame)
    plate('VK3_GuardBezel_'+str(sign),[(0,.277),(.041,.226),(0,.173),(-.041,.226)],sign*.058,-sign*.010,edge,'WeaponGripR',.002,weapon_frame)
    plate('VK3_GuardCrystal_'+str(sign),[(0,.264),(.027,.226),(0,.186),(-.027,.226)],sign*.066,-sign*.008,core,'WeaponGripR',.0015,weapon_frame,ridge=-sign*.003)

# Bake repeatable UVs and two small PBR tile pairs. No extra runtime material slots.
texture_dir=ROOT/'Assets/Alterverse/Resources/AuthoredHeroes/VoidKnightTextures'
texture_dir.mkdir(parents=True,exist_ok=True)
def image(name,array,normal=False):
    h,w=array.shape[:2];im=bpy.data.images.new(name,width=w,height=h,alpha=True)
    im.colorspace_settings.name='Non-Color' if normal else 'sRGB'
    pixels=np.concatenate([array,np.ones((h,w,1),np.float32)],axis=2).astype(np.float32)
    im.pixels.foreach_set(pixels.ravel());im.filepath_raw=str(texture_dir/(name+'.png'));im.file_format='PNG';im.save()
    return im
rng=np.random.default_rng(731)
y,x=np.mgrid[0:512,0:512];grain=rng.normal(0,.009,(512,512))
metal=.88+.017*np.sin(y*.73)+.011*np.sin(y*2.1)+grain
metal_rgb=np.repeat(np.clip(metal,.78,.96)[...,None],3,2)
metal_albedo=image('VoidKnightMetal_Albedo',metal_rgb)
height=.002*np.sin(y*.73)+rng.normal(0,.0007,(512,512))
dy,dx=np.gradient(height);normal=np.stack([-dx*8,-dy*8,np.ones_like(dx)],axis=2);normal/=np.linalg.norm(normal,axis=2)[...,None]
metal_normal=image('VoidKnightMetal_Normal',normal*.5+.5,True)
weave=(np.sin(x*math.pi*.5)*np.sin(y*math.pi*.5))
fabric=.72+.065*weave+rng.normal(0,.008,(512,512))
fabric_albedo=image('VoidKnightFabric_Albedo',np.repeat(fabric[...,None],3,2))
dy,dx=np.gradient(weave*.018);normal=np.stack([-dx,-dy,np.ones_like(dx)],axis=2);normal/=np.linalg.norm(normal,axis=2)[...,None]
fabric_normal=image('VoidKnightFabric_Normal',normal*.5+.5,True)
for mat,color,rough in [(black,(.050,.064,.088),.31),(edge,(.245,.285,.355),.25),(cloth,(.040,.046,.060),.77)]:
    mat.diffuse_color=(*color,1);bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*color,1);bsdf.inputs['Roughness'].default_value=rough
    albedo=fabric_albedo if mat==cloth else metal_albedo
    bump=fabric_normal if mat==cloth else metal_normal
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=albedo
    tint=mat.node_tree.nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(*color,1)
    mat.node_tree.links.new(tex.outputs['Color'],tint.inputs[1]);mat.node_tree.links.new(tint.outputs[0],bsdf.inputs['Base Color'])
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bump
    node=mat.node_tree.nodes.new('ShaderNodeNormalMap');node.inputs['Strength'].default_value=.25 if mat==cloth else .22
    mat.node_tree.links.new(tex.outputs['Color'],node.inputs['Color']);mat.node_tree.links.new(node.outputs['Normal'],bsdf.inputs['Normal'])
for obj in bpy.data.objects:
    if obj.type!='MESH':continue
    if obj.name.startswith('VK3_'):obj['design_version']=3
    if obj.name.startswith('VK_Joint_'):
        obj.data.materials.clear();obj.data.materials.append(cloth)
    if obj.name.startswith('VK_Palm_'):
        # A shaped gauntlet palm replaces the spherical glove silhouette.
        # Preserve each exact bound and the requested 130% hand envelope.
        lo=Vector(tuple(min(v.co[i] for v in obj.data.vertices) for i in range(3)))
        hi=Vector(tuple(max(v.co[i] for v in obj.data.vertices) for i in range(3)))
        center=(lo+hi)*.5;radius=(hi-lo)*.5
        for vertex in obj.data.vertices:
            for i in range(3):
                u=(vertex.co[i]-center[i])/radius[i]
                vertex.co[i]=center[i]+radius[i]*math.copysign(abs(u)**.45,u)
        obj.data.materials.clear();obj.data.materials.append(black)
        obj.data.update()
    uv=obj.data.uv_layers.new(name='SurfaceUV') if not obj.data.uv_layers else obj.data.uv_layers.active
    for polygon in obj.data.polygons:
        normal=polygon.normal;dominant=max(range(3),key=lambda a:abs(normal[a]));axes=[a for a in range(3) if a!=dominant]
        for index in polygon.loop_indices:
            co=obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv=(co[axes[0]]*4,co[axes[1]]*4)
rig['design_version']=3
bpy.context.scene['void_knight_design']='V3 / layered obsidian plate / forged greatsword / shoulder carry'
# Pack textures into the editable source; Unity receives the same external PNGs.
for im in [metal_albedo,metal_normal,fabric_albedo,fabric_normal]:im.pack()

# Pose editing is kept separate and deterministic; it uses the original arm/hand rest frames.
exec(compile((HERE/'shoulder_carry.py').read_text(),str(HERE/'shoulder_carry.py'),'exec'))
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'VoidKnight.blend'),compress=True)
print('V3_BUILT',len([o for o in bpy.data.objects if o.type=='MESH']),flush=True)
