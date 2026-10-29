"""Blender 5: reinforce the actual ChronoTraveler runtime payload and editable source.

Run once on the current carbon-thigh payload, then use --render-only for previews.
Keeps all 16 fitting anchors, the existing seven material slots, the runtime schema,
the imported FBX/animation clips, orb, face, hands and gameplay untouched.
"""
from pathlib import Path
import argparse,copy,gzip,hashlib,json,math,sys
import bpy,bmesh
import numpy as np
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
TARGET=ROOT/'Assets/Alterverse/Resources/ChronoTraveler/Model.bytes'
ART=ROOT/'ArtSource/Heroes/ChronoTraveler'
REVISION='traveler-reinforced-armor-20261029'
EXPECTED='414be5ce0286b8616cafd4f375ab78c84260014e'
SOURCE=ART/'TravelerReinforced.blend'
p=argparse.ArgumentParser();p.add_argument('--render-only',action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ART.mkdir(parents=True,exist_ok=True)
def cv(p):return Vector((p[0],-p[2],p[1]))
def rv(p):return (p[0],p[2],-p[1])
def blob(raw):return hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()

if args.render_only:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
else:
    raw=TARGET.read_bytes();data=json.loads(gzip.decompress(raw))
    if data.get('reinforcedArmorRevision')==REVISION:
        raise SystemExit('Already reinforced; use --render-only to regenerate previews.')
    assert blob(raw)==EXPECTED,'Unexpected runtime source; inspect it before rebuilding.'
    original=copy.deepcopy(data);base_count=len(data['boneIndices'])
    assert len(data['submeshes'])==7 and data['legEquipmentAuthored']
    vertices=np.array(data['vertices'],dtype=float).reshape(-1,3)
    indices=np.array(data['boneIndices'])
    # Bulk the actual thigh silhouette, tapering the added radial mass at hip/knee.
    # The existing runtime's uniform 1.12 leg fitting remains unchanged.
    changed=set();measurements={}
    used={v for s in data['submeshes'] for v in s['triangles']}
    for side in ['L','R']:
        name='Thigh'+side;b=data['boneNames'].index(name)
        head=np.array(data['anchors'][b]['head']);axis=np.array(data['anchors'][b]['tail'])-head
        length=np.linalg.norm(axis);axis/=length
        ids=np.where(indices==b)[0];old=vertices[ids].copy();off=old-head
        along=off@axis;t=along/length;radial=off-along[:,None]*axis
        blend=np.sin(np.clip((t-.035)/.93,0,1)*math.pi)**.65
        # 38% broader at the quadriceps, with the knee and hip centers fixed.
        vertices[ids]=old+radial*(.38*blend)[:,None]
        for index in ids:
            if np.linalg.norm(vertices[index]-np.array(original['vertices'][index*3:index*3+3]))>1e-12:changed.add(int(index))
        active=[int(i) for i in ids if int(i) in used and .20<(vertices[i,1]-.50)/.42<.80]
        before=np.array(original['vertices']).reshape(-1,3)[active];after=vertices[active]
        measurements[name]={'midsection_width_before_m':float(np.ptp(before[:,0])),
            'midsection_width_after_m':float(np.ptp(after[:,0])),
            'midsection_depth_before_m':float(np.ptp(before[:,2])),
            'midsection_depth_after_m':float(np.ptp(after[:,2]))}
    data['vertices']=vertices.ravel().tolist()
    # Recalculate only normals belonging to the resized thighs.
    normals=np.array(data['normals']).reshape(-1,3).copy();acc=np.zeros_like(vertices)
    for s in data['submeshes']:
        for face in np.array(s['triangles']).reshape(-1,3):
            if int(face[0]) not in changed:continue
            a,b,c=vertices[face];n=np.cross(b-a,c-a)
            for index in face:acc[index]+=n
    for index in changed:
        length=np.linalg.norm(acc[index])
        if length>1e-10:normals[index]=acc[index]/length
    data['normals']=normals.ravel().tolist()
    # Replace the two former narrow carbon badges with structural cuisse shells.
    removed_carbon_triangles=len(data['submeshes'][6]['triangles'])//3
    data['submeshes'][6]['triangles']=[]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    colors=[(.028,.041,.044,1),(.062,.082,.10,1),(.30,.38,.42,1),
        (.02,.65,.94,1),(.007,.018,.016,1),(.025,.18,1,1),(.055,.065,.072,1)]
    names=['Cyber undersuit','Graphite enamel','Brushed titanium','Cyan circuits','Optical recess','Blue shoulder stripes','Carbon thigh plates']
    materials=[]
    for slot,(name,color) in enumerate(zip(names,colors)):
        m=bpy.data.materials.new('Traveler '+name);m.use_nodes=True;m.diffuse_color=color
        shader=m.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=color
        shader.inputs['Metallic'].default_value=.82 if slot==2 else .66 if slot==1 else .18
        shader.inputs['Roughness'].default_value=.43 if slot in [1,2] else .58
        if slot in [3,5]:
            shader.inputs['Emission Color'].default_value=color;shader.inputs['Emission Strength'].default_value=1.8
        if slot==6:
            # Same compact twill and physical UV density as CarbonSurface in Unity.
            image=bpy.data.images.new('Traveler carbon twill',width=64,height=64)
            pixels=[]
            for y in range(64):
                for x in range(64):
                    warp=((x//8+y//8)%4)<2;across=(x if warp else y)%8;along=y if warp else x
                    value=.052+.034*math.sin((across+.5)*math.pi/8)**2+(along%2)*.005
                    pixels.extend((value*.94,value,value*1.04,1))
            image.pixels=pixels;image.pack()
            tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
            m.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
        materials.append(m)
    armature=bpy.data.armatures.new('Traveler original fitting anchors')
    rig=bpy.data.objects.new('Traveler fitting rig',armature);scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for name,anchor in zip(data['boneNames'],data['anchors']):
        b=armature.edit_bones.new(name);b.head=cv(anchor['head']);b.tail=cv(anchor['tail'])
    for name in data['boneNames']:
        parent={'Spine':'Hips','Chest':'Spine','Head':'Chest'}.get(name)
        if name.startswith('UpperArm'):parent='Chest'
        if name.startswith('Forearm'):parent='UpperArm'+name[-1]
        if name.startswith('Hand'):parent='Forearm'+name[-1]
        if name.startswith('Thigh'):parent='Hips'
        if name.startswith('Shin'):parent='Thigh'+name[-1]
        if name.startswith('Foot'):parent='Shin'+name[-1]
        if parent:armature.edit_bones[name].parent=armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
    def bind(obj,bone):
        obj.parent=rig;obj['bone']=bone
        vg=obj.vertex_groups.new(name=bone);vg.add(list(range(len(obj.data.vertices))),1,'REPLACE')
        mod=obj.modifiers.new('Original fitting skeleton','ARMATURE');mod.object=rig
    # Reconstruct the current live model as editable per-bone/material objects.
    for slot,sub in enumerate(data['submeshes']):
        groups={}
        for face in np.array(sub['triangles'],dtype=int).reshape(-1,3):
            b=data['boneIndices'][int(face[0])]
            assert all(data['boneIndices'][int(i)]==b for i in face)
            groups.setdefault(b,[]).append([int(i) for i in face])
        for b,faces in groups.items():
            used_ids=sorted({i for f in faces for i in f});mapping={old:new for new,old in enumerate(used_ids)}
            mesh=bpy.data.meshes.new('Original model surface');mesh.from_pydata([cv(vertices[i]) for i in used_ids],[],[[mapping[i] for i in f] for f in faces]);mesh.update()
            obj=bpy.data.objects.new('TravelerBase_'+data['boneNames'][b]+'_'+str(slot),mesh);scene.collection.objects.link(obj)
            mesh.materials.append(materials[slot]);uv=mesh.uv_layers.new(name='SurfaceUV')
            for f in mesh.polygons:
                f.use_smooth=True
                for li in f.loop_indices:
                    original_id=used_ids[mesh.loops[li].vertex_index];uv.data[li].uv=data['uvs'][original_id*2:original_id*2+2]
            bind(obj,data['boneNames'][b])
    added=[]
    def part(name,points,faces,slot,bone,bevel=.0012):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata([cv(v) for v in points],[],faces);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new('TR8_'+name,mesh);scene.collection.objects.link(obj);mesh.materials.append(materials[slot])
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        if bevel:
            mod=obj.modifiers.new('Machined bevel','BEVEL');mod.width=bevel;mod.segments=1;mod.limit_method='ANGLE'
            bpy.ops.object.modifier_apply(modifier=mod.name)
        uv=mesh.uv_layers.new(name='SurfaceUV')
        for face in mesh.polygons:
            for li in face.loop_indices:
                v=mesh.vertices[mesh.loops[li].vertex_index].co
                uv.data[li].uv=(v.x/.012,v.z/.012) if slot==6 else (v.x*5,v.z*5)
        obj['material_slot']=slot;obj['armor_revision']=8;bind(obj,bone);added.append(obj)
        return obj
    def plate(name,outline,z,depth,slot,bone,bevel=.0012):
        n=len(outline);front=list(z) if isinstance(z,(list,tuple)) else [z]*n
        return part(name,[(x,y,zz) for (x,y),zz in zip(outline,front)]+[(x,y,zz-depth) for (x,y),zz in zip(outline,front)],
            [tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],slot,bone,bevel)
    def local_plate(name,outline,front,depth,slot,bone,side=False,bevel=.0012):
        # Thigh coordinates: across, distance below hip, front. Outer panels face away.
        anchor=data['anchors'][data['boneNames'].index(bone)];head=np.array(anchor['head']);tail=np.array(anchor['tail'])
        sign=-1 if bone.endswith('L') else 1
        def position(x,down,z):
            h=head[1]-down;t=(h-head[1])/(tail[1]-head[1]);center=head+(tail-head)*t
            return (center[0]+sign*z,h,center[2]+x) if side else (center[0]+x,h,center[2]+z)
        n=len(outline);points=[position(x,y,front) for x,y in outline]+[position(x,y,front-depth) for x,y in outline]
        return part(name,points,[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],slot,bone,bevel)
    def inset(name,outline,z,depth,slot,bone,scale=.78):
        center=np.array(outline).mean(0);inner=[tuple(center+(np.array(v)-center)*scale) for v in outline]
        return plate(name,inner,z,depth,slot,bone)
    def fastener(name,x,y,z,bone,r=.0036):
        outer=[(x+r*math.cos(i*math.tau/8),y+r*math.sin(i*math.tau/8)) for i in range(8)]
        plate(name,outer,z,.002,2,bone,.0003)
        inset(name+'_Socket',outer,z+.0005,.001,4,bone,.48)
    for side,sg in [('L',-1),('R',1)]:
        thigh='Thigh'+side;shin='Shin'+side
        # Broad cuisses continue the scaled quadriceps with real front/side/back walls.
        broad=[(-.086,.091),(-.057,.064),(.057,.064),(.092,.100),(.091,.224),(.072,.323),(0,.370),(-.073,.324),(-.087,.219)]
        local_plate('CuisseTitaniumFrame_'+side,broad,.121,.021,2,thigh)
        center=np.array(broad).mean(0);carbon=[tuple(center+(np.array(v)-center)*.88) for v in broad]
        local_plate('CuisseCarbonShell_'+side,carbon,.129,.012,6,thigh)
        for j in range(2):
            d=.129+j*.060
            local_plate('CuisseOverlappingLame_'+side+'_'+str(j),[(-.070,d-.020),(-.033,d-.010),(.027,d-.002),(.067,d-.018),(.071,d+.025),(.022,d+.042),(-.031,d+.029),(-.069,d+.036)],
                .139,.009,1,thigh)
        local_plate('CuisseCenterSpine_'+side,[(-.014,.217),(0,.205),(.017,.218),(.014,.315),(0,.340),(-.014,.315)],.146,.010,2,thigh)
        local_plate('CuisseCyanChannelSeat_'+side,[(.040,.207),(.059,.197),(.062,.225),(.047,.310),(.028,.334),(.030,.302)],.143,.004,4,thigh)
        local_plate('CuisseCyanChannel_'+side,[(.045,.218),(.052,.211),(.052,.237),(.038,.304),(.033,.309)],.148,.003,3,thigh,bevel=.0006)
        # Side enclosure wraps around the quad instead of adding a phone-shaped badge.
        outer=[(-.069,.103),(-.043,.075),(.042,.091),(.076,.140),(.073,.251),(.040,.321),(-.043,.321),(-.076,.247)]
        local_plate('CuisseOuterHousing_'+side,outer,.108,.020,1,thigh,True)
        local_plate('CuisseSideRecess_'+side,[(-.040,.163),(.040,.163),(.045,.220),(.026,.266),(-.038,.256)],.130,.005,4,thigh,True)
        for j in range(4):
            d=.178+j*.019
            local_plate('CuisseHeatSink_'+side+'_'+str(j),[(-.032,d),(.030,d-.005),(.030,d+.002),(-.032,d+.007)],.135,.006,2,thigh,True,.0006)
        local_plate('CuisseRearClosure_'+side,[(-.075,.102),(.075,.102),(.070,.284),(.045,.335),(-.048,.335),(-.072,.283)],-.101,-.013,1,thigh)
        local_plate('CuisseHipTransition_'+side,[(-.074,.033),(-.041,.009),(.048,.016),(.089,.049),(.079,.101),(.041,.126),(-.065,.107)],.113,.012,1,thigh)
        # Visible shear bolts and a small serial recess break up the broad carbon face.
        head=data['anchors'][data['boneNames'].index(thigh)]['head']
        for dx,down in [(-.065,.112),(.068,.119),(-.053,.301)]:
            fastener('CuisseShearBolt_'+side+'_'+str(down),head[0]+dx,head[1]-down,.145,thigh)
        local_plate('CuisseSerialRecess_'+side,[(-.061,.234),(-.029,.238),(-.029,.249),(-.061,.245)],.145,.003,4,thigh,bevel=.0004)
        # Ankle/knee transition remains separated from the thigh during movement.
        x=sg*.133
        plate('KneeSegment_'+side,[(x-.054,.544),(x,.565),(x+.054,.544),(x+.057,.503),(x+.031,.472),(x-.031,.472),(x-.057,.503)],.099,.016,1,shin)
        plate('KneeTitaniumKeel_'+side,[(x-.024,.532),(x,.544),(x+.024,.532),(x+.020,.499),(x,.488),(x-.020,.499)],.118,.010,2,shin)
        for j in range(2):
            y=.398-j*.119;x=sg*(.143+j*.009)
            plate('GreaveSteppedShell_'+side+'_'+str(j),[(x-.048,y+.047),(x+.048,y+.047),(x+.044,y-.042),(x,y-.064),(x-.044,y-.042)],.131,.013,1,shin)
            plate('GreaveStatus_'+side+'_'+str(j),[(x+.025,y+.026),(x+.033,y+.028),(x+.029,y-.025),(x+.022,y-.033)],.147,.004,3,shin,.0005)
        # Graphite plastrons and a small bright chrono key frame the titanium chest.
        for j in range(2):
            y=1.368-j*.052
            outline=[(sg*.037,y+.027),(sg*.095,y+.039),(sg*.163,y+.010),(sg*.178,y-.019),
                (sg*.143,y-.031),(sg*.080,y-.017),(sg*.038,y-.029)]
            plate('ThoracicLaminate_'+side+'_'+str(j),outline,.171-j*.004,.010,1,'Chest')
            stripe=[(sg*.054,y+.019),(sg*.098,y+.028),(sg*.143,y+.008),(sg*.141,y+.003),(sg*.097,y+.022),(sg*.054,y+.014)]
            plate('ThoracicTitaniumEdge_'+side+'_'+str(j),stripe,.184-j*.004,.003,2,'Chest',.0004)
        plate('CollarServicePlate_'+side,[(sg*.024,1.431),(sg*.082,1.450),(sg*.134,1.421),(sg*.113,1.397),(sg*.063,1.409)],.126,.012,1,'Chest')
        # Built-in joint hardware and side vents use the existing material bank.
        upper='UpperArm'+side;fore='Forearm'+side
        x=sg*.213;y=1.399
        plate('PauldronFrontScale_'+side,[(x-sg*.057,y+.030),(x+sg*.044,y+.018),(x+sg*.058,y-.029),(x+sg*.030,y-.071),(x-sg*.038,y-.053)],.103,.012,1,upper)
        plate('PauldronBlueInset_'+side,[(x-sg*.035,y+.015),(x+sg*.019,y+.009),(x+sg*.034,y-.004),(x+sg*.029,y-.010),(x+sg*.017,y+.002),(x-sg*.035,y+.008)],.119,.003,5,upper,.0005)
        # Faceted crown and two overlapping lower scales cover the simple rotary dome.
        outline=[(sg*.148,1.538),(sg*.179,1.573),(sg*.249,1.570),(sg*.302,1.531),
            (sg*.316,1.484),(sg*.279,1.454),(sg*.215,1.460),(sg*.165,1.486)]
        plate('PauldronMachinedCrown_'+side,outline,.112,.015,1,upper,.0018)
        inset('PauldronCrownInlay_'+side,outline,.126,.009,1,upper,.80)
        for j in range(3):
            xx=sg*(.187+j*.024)
            plate('PauldronCrownStatus_'+side+'_'+str(j),[(xx,1.548),(xx+sg*.008,1.546),(xx+sg*.007,1.514),(xx,1.511)],.140,.003,5,upper,.0005)
        for j in range(2):
            yy=1.461-j*.038
            plate('PauldronArticulatedScale_'+side+'_'+str(j),[(sg*.190,yy+.021),(sg*.276,yy+.020),(sg*.302,yy-.004),(sg*.282,yy-.025),(sg*.213,yy-.016),(sg*.184,yy-.001)],.132-j*.003,.011,1,upper)
        for xx,yy in [(sg*.174,1.522),(sg*.281,1.484)]:fastener('PauldronRecessedBolt_'+side+str(yy),xx,yy,.143,upper)
        # Inset chest cooling module: separate dark pocket, raised slats and locks.
        cx=sg*.139;cy=1.297
        pocket=[(cx-.022,cy+.018),(cx+.022,cy+.018),(cx+.022,cy-.016),(cx-.022,cy-.016)]
        plate('ChestExtractorFrame_'+side,pocket,.180,.010,2,'Chest')
        inset('ChestExtractorPocket_'+side,pocket,.185,.004,4,'Chest',.80)
        for j in range(3):
            yy=cy-.009+j*.009
            plate('ChestExtractorSlat_'+side+'_'+str(j),[(cx-.014,yy),(cx+.014,yy),(cx+.014,yy+.003),(cx-.014,yy+.003)],.189,.003,1,'Chest',.0004)
        x=sg*.289;y=1.064
        plate('VambraceSplitCasing_'+side,[(x-.047,y+.052),(x-.025,y+.068),(x+.032,y+.055),(x+.044,y+.012),(x+.025,y-.066),(x-.025,y-.059)],.113,.012,1,fore)
        plate('VambraceTitaniumSpine_'+side,[(x-.012,y+.044),(x+.007,y+.039),(x+.010,y-.045),(x-.009,y-.052)],.131,.006,2,fore)
        for j in range(3):
            yy=y+.026-j*.021
            plate('VambraceCyanReadout_'+side+'_'+str(j),[(x+.019,yy),(x+.031,yy),(x+.031,yy-.005),(x+.019,yy-.005)],.132,.003,3,fore,.0004)
        # Rear reactor separators give the silhouette depth in back-facing gameplay.
        x=sg*.082
        plate('DorsalReactorCage_'+side,[(x-.034,1.365),(x+.034,1.365),(x+.039,1.267),(x+.017,1.242),(x-.032,1.258)],-.160,-.011,1,'Chest')
        for j in range(4):
            yy=1.275+j*.022
            plate('DorsalRadiator_'+side+'_'+str(j),[(x-.026,yy),(x+.027,yy),(x+.027,yy+.006),(x-.026,yy+.006)],-.176,-.005,2,'Chest',.0006)
    plate('SternalChronoKey',[(-.015,1.399),(0,1.412),(.015,1.399),(.014,1.340),(0,1.325),(-.014,1.340)],.185,.011,2,'Chest')
    plate('SternalChronoReadout',[(-.005,1.392),(.005,1.392),(.005,1.350),(0,1.341),(-.005,1.350)],.199,.003,3,'Chest',.0005)
    # Export exact Blender triangle corners, retaining hard machined normals and UVs.
    for obj in added:
        mesh=obj.data;mesh.calc_loop_triangles();slot=obj['material_slot'];b=data['boneNames'].index(obj['bone'])
        for triangle in mesh.loop_triangles:
            offset=len(data['boneIndices'])
            for vi,li in zip(triangle.vertices,triangle.loops):
                point=obj.matrix_world@mesh.vertices[vi].co
                normal=triangle.normal.normalized()
                data['vertices'].extend(float(v) for v in rv(point));data['normals'].extend(float(v) for v in rv(normal))
                data['boneIndices'].append(b);data['uvs'].extend(float(v) for v in mesh.uv_layers.active.data[li].uv)
            data['submeshes'][slot]['triangles'].extend((offset,offset+1,offset+2))
    data['reinforcedArmorRevision']=REVISION
    data['legEquipmentAuthored']=True
    total_triangles=sum(len(s['triangles'])//3 for s in data['submeshes'])
    assert len(data['boneIndices'])<65535 and total_triangles<=40000
    assert data['boneNames']==original['boneNames'] and data['anchors']==original['anchors']
    for i,b in enumerate(original['boneIndices']):
        if original['boneNames'][b].startswith('Thigh'):continue
        assert data['vertices'][3*i:3*i+3]==original['vertices'][3*i:3*i+3]
        assert data['normals'][3*i:3*i+3]==original['normals'][3*i:3*i+3]
    assert all(s['triangles'][:len(original['submeshes'][i]['triangles'])]==original['submeshes'][i]['triangles'] for i,s in enumerate(data['submeshes']) if i!=6)
    assert len(data['uvs'])==len(data['boneIndices'])*2
    assert np.isfinite(np.array(data['vertices'])).all() and np.isfinite(np.array(data['normals'])).all()
    for sub in data['submeshes']:
        assert len(sub['triangles'])%3==0 and all(0<=i<len(data['boneIndices']) for i in sub['triangles'])
    closed=0
    for obj in added:
        bm=bmesh.new();bm.from_mesh(obj.data);assert all(e.is_manifold for e in bm.edges),obj.name;bm.free();closed+=1
    TARGET.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))
    scene['runtime_resource']='ChronoTraveler/Model.bytes';scene['revision']=REVISION
    rig['runtime_anchors_unchanged']=True
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    report=dict(result='PASS',revision=REVISION,input_git_blob=EXPECTED,output_git_blob=blob(TARGET.read_bytes()),
        runtime_vertices=len(data['boneIndices']),runtime_triangles=total_triangles,materials=7,fitting_bones=16,
        new_armor_parts=len(added),closed_new_shells=closed,resized_thigh_vertices=len(changed),thigh_measurements=measurements,
        removed_narrow_carbon_triangles=removed_carbon_triangles,anchors_and_bone_names_unchanged=True,
        original_non_thigh_vertices_and_normals_unchanged=True,original_non_carbon_triangles_unchanged=True,
        old_phone_fallback_disabled=True,runtime_schema_version=data['version'],existing_FBX_and_animation_clips_unchanged=True,
        native_unity_and_android_visual_check=False)
    (ART/'reinforced-armor-validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('TRAVELER_REINFORCED',json.dumps(report),flush=True)

scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
world=bpy.data.worlds.new('Traveler neutral studio');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.13,.15,.19,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6;scene.world=world
for obj in list(scene.objects):
    if obj.type in ['CAMERA','LIGHT']:bpy.data.objects.remove(obj,do_unlink=True)
def aim(obj,p):obj.rotation_euler=(Vector(p)-obj.location).to_track_quat('-Z','Y').to_euler()
for loc,power,size in [((2,-4,4),600,3),((-3,-2,2.5),350,2),((1,3,4),850,2)]:
    light=bpy.data.lights.new('Traveler softbox','AREA');light.energy=power;light.size=size
    obj=bpy.data.objects.new(light.name,light);scene.collection.objects.link(obj);obj.location=loc;aim(obj,(0,0,1))
cam=bpy.data.cameras.new('Traveler review');camera=bpy.data.objects.new(cam.name,cam);scene.collection.objects.link(camera)
cam.type='ORTHO';scene.camera=camera;cam.ortho_scale=2.05
for name,position in [('reinforced-front',(0,-6,1.1)),('reinforced-angle',(2.7,-6,2.25)),('reinforced-back',(0,6,1.1))]:
    camera.location=position;aim(camera,(0,0,.91));scene.render.filepath=str(ART/(name+'.png'));bpy.ops.render.render(write_still=True)
print('TRAVELER_REINFORCED_STUDIO_RENDERS_COMPLETE',flush=True)
