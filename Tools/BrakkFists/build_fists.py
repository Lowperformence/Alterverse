"""Forge Grip V2: editable solid parts, exact mesh export, source-derived exploded views.
Run with Blender 4.x: blender -b --python build_fists.py -- --source Rook.blend
Runtime coordinates are project model-space metres, X right / Y up / Z forward.
"""
import argparse, gzip, hashlib, json, math, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Matrix, Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/BrakkFists'
RES=ROOT/'Assets/Alterverse/Resources/BrakkFists'
ART.mkdir(parents=True,exist_ok=True); RES.mkdir(parents=True,exist_ok=True)
args=argparse.ArgumentParser();args.add_argument('--source',required=True)
args.add_argument('--skip-hero-render',action='store_true')
opts=args.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
source=Path(opts.source).resolve()
bpy.ops.wm.open_mainfile(filepath=str(source))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
arm.data.pose_position='REST';bpy.context.view_layer.update()
originals={s:bpy.data.objects['DF_Gauntlet'+s] for s in ['L','R']}
bind={s:arm.matrix_world@arm.data.bones['Hand'+s].matrix_local for s in ['L','R']}
FIST_SIZE=2.0
body=bpy.data.objects['DF_Anatomy']
handGroup=body.vertex_groups['HandR'].index
handPoints=[bind['R'].inverted()@(body.matrix_world@v.co) for v in body.data.vertices
            if any(g.group==handGroup and g.weight>.5 for g in v.groups)]
assert handPoints
palmR=Vector(tuple((min(p[k] for p in handPoints)+max(p[k] for p in handPoints))*.5 for k in range(3)))
palms={'R':palmR,'L':Vector((-palmR.x,palmR.y,palmR.z))}
contract={'sourcePath':'ArtSource/Heroes/Rook.blend','sourceGitBlob':'98629c674d44af3832bfb51cda004d1575c18c14',
          'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
          'sourceRevision':'954f9cecb201260da40b507f93216f3f91b909d6',
          'handBindMatrices':{s:[list(r) for r in bind[s]] for s in bind},'runtimeHandScale':.8,
          'gauntletSizeRelativeToInitialV2':FIST_SIZE,'handPalmLocal':{s:list(palms[s]) for s in palms},
          'runtimeAttachment':'mesh centre aligned to baked visible anatomy palm in the actual hand space after hand sizing'}
(ART/'fit-contract.json').write_text(json.dumps(contract,indent=2)+'\n')
conversion=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
palette=[('Graphite',(.027,.033,.038),.32,.42),('Forge gold',(.82,.56,.18),.78,.29),
         ('Dark steel',(.105,.128,.145),.72,.32),('Amber channel',(1,.34,.025),.20,.32),
         ('Recess liner',(.007,.011,.016),.05,.66)]
materials=[]
for name,color,metal,rough in palette:
    mat=bpy.data.materials.new('FGV2 '+name);mat.diffuse_color=(*color,1);mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Metallic'].default_value=metal;bsdf.inputs['Roughness'].default_value=rough
    if name=='Amber channel':
        bsdf.inputs['Emission Color'].default_value=(*color,1);bsdf.inputs['Emission Strength'].default_value=.65
    materials.append(mat)

parts=[]
def finish(obj,name,mat,category,side):
    obj.name='FGV2_'+side+'_'+name;obj.data.materials.append(materials[mat])
    bpy.context.view_layer.objects.active=obj
    for modifier in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=modifier.name)
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    obj['part_id']=name;obj['material_slot']=mat;obj['assembly_group']=category;obj['side']=side
    local=[v.co.copy() for v in obj.data.vertices]
    if side=='L':
        for v in obj.data.vertices:v.co.x=-v.co.x
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    for v in obj.data.vertices:v.co=bind[side]@v.co
    for p in obj.data.polygons:p.use_smooth=False
    obj.data.update()
    for g in list(obj.vertex_groups):obj.vertex_groups.remove(g)
    group=obj.vertex_groups.new(name='Hand'+side);group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    skin=obj.modifiers.new('Rigid hand binding','ARMATURE');skin.object=arm
    obj.parent=arm;obj.matrix_parent_inverse=arm.matrix_world.inverted()
    parts.append(obj);return obj

def box(name,c,size,mat,category,side,bevel=.004,rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj=bpy.context.object;obj.name=name
    for v in obj.data.vertices:
        p=Vector((v.co.x*size[0],v.co.y*size[1],v.co.z*size[2]))
        if rotation:p=Matrix.Rotation(rotation,3,'Z')@p
        v.co=p+Vector(c)
    mod=obj.modifiers.new('Manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=2;mod.affect='EDGES'
    return finish(obj,name,mat,category,side)

def shell(name,outline,z0,z1,mat,category,side,bevel=.003):
    # Closed chamfered prism. Named solids remain separately editable.
    vertices=[(x,y,z) for z in [z0,z1] for x,y in outline];n=len(outline)
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    mod=obj.modifiers.new('Shell edge radius','BEVEL');mod.width=bevel;mod.segments=2
    return finish(obj,name,mat,category,side)

def collar(name,inner,outer,y0,y1,mat,category,side):
    # Hollow wrist collar, elliptical X/Z cross section; it never bridges the forearm.
    vertices=[];faces=[];n=16;cx=-.003;cz=-.030
    for y in [y0,y1]:
        for radii in [outer,inner]:
            for i in range(n):
                a=2*math.pi*i/n;vertices.append((cx+radii[0]*math.cos(a),y,cz+radii[1]*math.sin(a)))
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,2*n+j,2*n+i),(n+j,n+i,3*n+i,3*n+j),
                      (j,i,n+i,n+j),(2*n+i,2*n+j,3*n+j,3*n+i)])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    mod=ob.modifiers.new('Collar edge radius','BEVEL');mod.width=.0015;mod.segments=1
    return finish(ob,name,mat,category,side)

def fastener(name,x,y,z,side):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.0045,depth=.003,location=(0,0,0))
    obj=bpy.context.object
    for v in obj.data.vertices:v.co+=Vector((x,y,z))
    mod=obj.modifiers.new('Bolt chamfer','BEVEL');mod.width=.0007;mod.segments=1
    return finish(obj,name,2,'hardware',side)

def glove_core(side):
    outline=[(-.092,.015),(-.078,-.006),(.048,-.006),(.073,.015),(.081,.130),(.060,.167),(-.056,.180),(-.097,.146)]
    center=Vector((-.009,.086));n=len(outline);verts=[];faces=[]
    for z,scale in [(-.140,.79),(-.083,1.0),(.032,1.0),(.045,.91)]:
        for x,y in outline:
            p=center+(Vector((x,y))-center)*scale;verts.append((p.x,p.y,z))
    faces.extend([tuple(range(n-1,-1,-1)),tuple(range(3*n,4*n))])
    for ring in range(3):
        for i in range(n):faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
    mesh=bpy.data.meshes.new('Profiled graphite glove');mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new('Profiled graphite glove',mesh);bpy.context.collection.objects.link(ob)
    mod=ob.modifiers.new('Rounded glove edges','BEVEL');mod.width=.0045;mod.segments=2
    return finish(ob,'01_glove_core',0,'core',side)

def side_plate(name,outline,x0,x1,mat,category,side):
    n=len(outline);verts=[(x,y,z) for x in [x0,x1] for y,z in outline]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    mod=ob.modifiers.new('Side shell chamfer','BEVEL');mod.width=.002;mod.segments=2
    return finish(ob,name,mat,category,side)

for side in ['R','L']:
    glove_core(side)
    collar('02_open_wrist_liner',(.077,.076),(.083,.082),-.019,.015,4,'wrist',side)
    collar('03_open_wrist_collar',(.083,.082),(.090,.089),-.015,.010,1,'wrist',side)
    shell('04_dorsal_shield',[(-.077,.028),(-.060,.013),(.044,.013),(.066,.033),(.069,.122),(.045,.148),(-.052,.158),(-.082,.133)],.046,.066,1,'dorsal',side,.003)
    shell('05_dorsal_inset',[(-.047,.047),(-.031,.034),(.024,.034),(.045,.052),(.047,.112),(.027,.129),(-.035,.137),(-.052,.119)],.066,.069,2,'dorsal',side,.0015)
    for i,(x,y) in enumerate([(-.071,.174),(-.027,.186),(.018,.183),(.062,.164)],1):
        box('06_knuckle_'+str(i),(x,y,-.008),(.041,.057,.151),1,'knuckles',side,.005)
        box('07_knuckle_inset_'+str(i),(x,y+.028,-.012),(.023,.004,.074),2,'knuckles',side,.001)
        box('08_folded_finger_'+str(i),(x,.130,-.145),(.039,.061,.027),0,'fingers',side,.005)
        box('09_finger_guard_'+str(i),(x,.134,-.159),(.030,.040,.007),1,'fingers',side,.0015)
    box('10_thumb_core',(-.103,.081,-.066),(.055,.110,.093),0,'thumb',side,.011,rotation=-.28)
    box('11_thumb_guard',(-.123,.081,-.059),(.020,.088,.074),1,'thumb',side,.004,rotation=-.28)
    box('12_thumb_joint',(-.104,.122,-.103),(.040,.022,.041),2,'thumb',side,.003,rotation=-.28)
    for sign in [-1,1]:
        box('13_retaining_rail_'+str(sign),(sign*.078-.007,.083,.039),(.013,.105,.032),2,'rails',side,.002)
        box('14_recessed_amber_'+str(sign),(sign*.067-.007,.086,.066),(.006,.067,.003),3,'dorsal',side,.001)
    for i,(x,y) in enumerate([(-.057,.034),(.044,.035),(-.053,.137),(.044,.124)],1):fastener('15_flush_bolt_'+str(i),x,y,.068,side)
    side_plate('16_outer_wrap_shell',[(.032,-.062),(.051,.026),(.129,.026),(.157,-.041),(.137,-.108),(.048,-.108)],.074,.089,1,'rails',side)
    side_plate('17_outer_recess',[(.058,-.062),(.069,.002),(.112,.002),(.130,-.043),(.119,-.085),(.068,-.085)],.089,.092,2,'rails',side)

def mesh_data(objects):
    vertices=[];normals=[];uv=[];slots=[{'triangles':[]} for _ in materials];part_table=[]
    for ob in objects:
        mesh=ob.data;mesh.calc_loop_triangles();cache={};start=len(vertices)//3
        exportedTriangles=0
        for tri in mesh.loop_triangles:
            positions=[conversion@ob.data.vertices[index].co for index in tri.vertices]
            rounded=[Vector(tuple(round(x,8) for x in p)) for p in positions]
            if (rounded[1]-rounded[0]).cross(rounded[2]-rounded[0]).length<=2e-11:continue
            exportedTriangles+=1
            dst=[]
            normal=conversion.to_3x3()@mesh.polygons[tri.polygon_index].normal
            for index in tri.vertices:
                p=conversion@ob.data.vertices[index].co
                key=tuple(round(v,8) for v in (*p,*normal))
                if key not in cache:
                    cache[key]=len(vertices)//3;vertices.extend(round(v,8) for v in p);normals.extend(round(v,8) for v in normal)
                    # Repeatable planar coordinates per manufactured piece, measured in metres.
                    uv.extend([round(float(p.x)*4,7),round(float(p.y)*4,7)])
                dst.append(cache[key])
            slots[ob['material_slot']]['triangles'].extend(dst)
        part_table.append({'id':ob['part_id'],'group':ob['assembly_group'],'material':ob['material_slot'],
                           'firstVertex':start,'vertexCount':len(vertices)//3-start,'triangles':exportedTriangles})
    side=objects[0]['side'];pivot=conversion@(bind[side]@palms[side])
    return {'side':side,'vertices':vertices,'normals':normals,'uv':uv,'pivot':list(pivot),'submeshes':slots,'parts':part_table}

# Double all three dimensions about the fist centre and seat that centre on the palm.
# This removes the initial source-model offset instead of scaling it along with the fist.
sizeChecks={}
for side in ['L','R']:
    objects=[o for o in parts if o['side']==side];inverse=bind[side].inverted()
    before=[inverse@v.co for ob in objects for v in ob.data.vertices]
    low=Vector(tuple(min(p[k] for p in before) for k in range(3)))
    high=Vector(tuple(max(p[k] for p in before) for k in range(3)))
    center=(low+high)*.5;oldSize=high-low
    for ob in objects:
        for v in ob.data.vertices:v.co=bind[side]@(palms[side]+FIST_SIZE*(inverse@v.co-center))
        ob.data.update()
    after=[inverse@v.co for ob in objects for v in ob.data.vertices]
    newLow=Vector(tuple(min(p[k] for p in after) for k in range(3)))
    newHigh=Vector(tuple(max(p[k] for p in after) for k in range(3)))
    newSize=newHigh-newLow
    assert (newSize-oldSize*FIST_SIZE).length<2e-6
    assert ((newLow+newHigh)*.5-palms[side]).length<1e-6
    sizeChecks[side]={'previousDimensionsMetres':list(oldSize),'newDimensionsMetres':list(newSize),
                      'ratio':[newSize[k]/oldSize[k] for k in range(3)],'palmCentreErrorMetres':((newLow+newHigh)*.5-palms[side]).length}
hands=[mesh_data([o for o in parts if o['side']==side]) for side in ['L','R']]
data={'version':1,'revision':'brakk-forge-grip-v2-hand-fit-20261012','coordinateSpace':'authored-model-metres',
      'sizeRelativeToInitialV2':FIST_SIZE,
      'hands':hands,'materials':[{'name':x[0],'color':list(x[1]),'metallic':x[2],'smoothness':1-x[3]} for x in palette]}
raw=json.dumps(data,separators=(',',':')).encode();(RES/'Model.bytes').write_bytes(gzip.compress(raw,mtime=0))
localBounds={}
for side in ['L','R']:
    ps=[bind[side].inverted()@v.co for ob in parts if ob['side']==side for v in ob.data.vertices]
    localBounds[side]={'min':[min(p[k] for p in ps) for k in range(3)],'max':[max(p[k] for p in ps) for k in range(3)]}
(ART/'assembly.json').write_text(json.dumps({'revision':data['revision'],'hands':[{'side':h['side'],'parts':h['parts']} for h in hands],
                                          'dimensionsLocalMetres':localBounds},indent=2)+'\n')
# OBJ component export retains named solids and normals from the same mesh.
with (ART/'BrakkFists-parts.obj').open('w') as f:
    f.write('# Forge Grip V2 - Blender coordinates, metres; one object per editable solid\n')
    offset=1
    for ob in parts:
        f.write('o '+ob.name+'\n')
        for v in ob.data.vertices:f.write('v '+' '.join(f'{x:.8f}' for x in v.co)+'\n')
        ob.data.calc_loop_triangles()
        for t in ob.data.loop_triangles:f.write('f '+' '.join(str(offset+i) for i in t.vertices)+'\n')
        offset+=len(ob.data.vertices)

# Verify exact exported geometry, rigid hand binding, and sampled source animation attachment.
report={'revision':data['revision'],'blenderVersion':bpy.app.version_string,'sourceSha256':contract['sourceSha256'],
        'partsPerHand':len(parts)//2,'hands':{},'sourceActions':[a.name for a in bpy.data.actions],
        'unityEditorTested':False,'androidTested':False,'checks':{}}
for h in hands:
    v=h['vertices'];count=len(v)//3;tris=[s['triangles'] for s in h['submeshes']]
    assert all(math.isfinite(x) for x in v+h['normals']+h['uv'])
    assert all(0<=i<count for t in tris for i in t)
    assert all(len(t)%3==0 for t in tris)
    for t in tris:
        for i in range(0,len(t),3):
            a,b,c=[Vector(v[q*3:q*3+3]) for q in t[i:i+3]];assert (b-a).cross(c-a).length>1e-11
    report['hands'][h['side']]={'vertices':count,'triangles':sum(len(t)//3 for t in tris)}
    for ob in [p for p in parts if p['side']==h['side']]:
        assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in ob.data.vertices)
report['checks']['finiteGeometryValidIndicesNonzeroTriangles']=True
report['checks']['rigidHandWeights']=True
report['checks']['doubleSizeAndPalmFit']=sizeChecks
samples=0;arm.data.pose_position='POSE'
for action in bpy.data.actions:
    arm.animation_data.action=action
    for frame in [int(action.frame_range[0]),int(sum(action.frame_range)/2),int(action.frame_range[1])]:
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
        for ob in parts:
            evaluated=ob.evaluated_get(deps);mesh=evaluated.to_mesh()
            skinMatrix=arm.matrix_world@arm.pose.bones['Hand'+ob['side']].matrix@arm.data.bones['Hand'+ob['side']].matrix_local.inverted()
            for i in [0,len(mesh.vertices)//2,len(mesh.vertices)-1]:
                expected=skinMatrix@ob.data.vertices[i].co;actual=evaluated.matrix_world@mesh.vertices[i].co
                assert (expected-actual).length<2e-5
            evaluated.to_mesh_clear()
        samples+=1
report['checks']['sourceAnimationAttachmentSamples']=samples
arm.animation_data.action=bpy.data.actions.get('Idle');bpy.context.scene.frame_set(1)
report['checks']['legacyGauntletsExcludedFromNewAsset']=True

# Save just the new parts and inherited rig/actions. Original anatomy is used only for an unsaved fit render.
originalHidden={o:o.hide_render for o in originals.values()}
for o in originals.values():o.hide_render=True
for ob in parts:ob.hide_render=False
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=False
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.6
scene.use_nodes=False
for layer in scene.view_layers:layer.cycles.use_denoising=False
scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world.color=(.11,.11,.11)
# Simulate the established 0.8 hand scale without changing the inherited rig asset.
for side in ['L','R']:arm.pose.bones['Hand'+side].scale=(.8,.8,.8)
body=bpy.data.objects.get('DF_Anatomy')
for mat in body.data.materials:
    if mat and mat.use_nodes and mat.name.startswith('Signature enamel'):
        bsdf=mat.node_tree.nodes.get('Principled BSDF')
        if bsdf:bsdf.inputs['Base Color'].default_value=(.82,.56,.18,1)
if not opts.skip_hero_render:
    scene.render.filepath=str(ART/'BrakkFists-on-Brakk.png');bpy.ops.render.render(write_still=True)
for side in ['L','R']:arm.pose.bones['Hand'+side].scale=(1,1,1)

for ob in list(bpy.data.objects):
    if ob not in parts and ob!=arm:bpy.data.objects.remove(ob,do_unlink=True)
arm.data.pose_position='REST';arm.animation_data.action=None
for track in arm.animation_data.nla_tracks:track.mute=True
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'BrakkFists-assembly.blend'),compress=True)

# FBX is a supplementary interchange asset; runtime uses the exact Model.bytes geometry.
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(ART/'BrakkFists.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},
                         add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)

# Studio assembly and exploded rendering. Geometry stays identical; only view transforms change.
arm.hide_render=True;right=[o for o in parts if o['side']=='R']
for ob in parts:
    for mod in list(ob.modifiers):ob.modifiers.remove(mod)
    ob.parent=None
    if ob['side']=='L':ob.hide_render=True;continue
    for v in ob.data.vertices:v.co=bind['R'].inverted()@v.co
scene.world.color=(.24,.24,.24)
def aim(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.43,.57,.38));cam=bpy.context.object;aim(cam,(0,.09,-.035));cam.data.type='ORTHO';cam.data.ortho_scale=.47*FIST_SIZE;scene.camera=cam
for name,loc,energy,size in [('Key',(.2,.2,.6),16,.42),('Fill',(-.3,.1,.3),10,.36),('Rim',(.2,-.3,.2),12,.30)]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.name=name;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;aim(light,(0,.09,-.03))
scene.render.resolution_x=1100;scene.render.resolution_y=1000
scene.render.filepath=str(ART/'BrakkFists-assembled.png');bpy.ops.render.render(write_still=True)
shifts={'core':(0,0,0),'wrist':(0,-.14,0),'dorsal':(0,0,.115),'knuckles':(0,.115,0),
        'fingers':(0,0,-.09),'thumb':(-.125,0,0),'rails':(.125,0,0),'hardware':(0,0,.20)}
for ob in right:ob.location=Vector(shifts[ob['assembly_group']])*FIST_SIZE
cam.location=(.65,.80,.63);aim(cam,(0,.08,.006));cam.data.ortho_scale=.79*FIST_SIZE
scene.render.filepath=str(ART/'BrakkFists-exploded.png');bpy.ops.render.render(write_still=True)
for ob in list(parts):
    if ob['side']=='L':bpy.data.objects.remove(ob,do_unlink=True)
bpy.data.objects.remove(arm,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'BrakkFists-exploded.blend'),compress=True)

report['runtimeSha256']=hashlib.sha256((RES/'Model.bytes').read_bytes()).hexdigest()
(ART/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('FORGE_GRIP_VALIDATED',json.dumps(report),flush=True)
