"""Native Blender sources, skinned FBX exports and geometry renders (Blender 4.5)."""
from pathlib import Path
import hashlib, json, math, tempfile
import bpy, bmesh
import numpy as np
from mathutils import Vector
from build_parts import Parts, PALETTES, REVISION, baseline, obj_source, unit

def xyz(point):
    x,y,z=point
    return (float(x),float(-z),float(y))

def material(name,color,metal=.0,roughness=.45,emission=False,fabric=False):
    mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
    nodes=mat.node_tree.nodes;bsdf=nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Metallic'].default_value=metal;bsdf.inputs['Roughness'].default_value=roughness
    if emission:
        bsdf.inputs['Emission Color'].default_value=(*color,1)
        bsdf.inputs['Emission Strength'].default_value=1.1
    if fabric:
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=470
        noise.inputs['Detail'].default_value=2
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.17;bump.inputs['Distance'].default_value=.0007
        mat.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height'])
        mat.node_tree.links.new(bump.outputs['Normal'],bsdf.inputs['Normal'])
        bsdf.inputs['Sheen Weight'].default_value=.10
    return mat

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0

def mesh_object(name,vertices,faces,mat_indices,materials,normals=None):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    for mat in materials:mesh.materials.append(mat)
    mesh.polygons.foreach_set('material_index',mat_indices)
    mesh.polygons.foreach_set('use_smooth',[True]*len(mesh.polygons))
    if normals is not None:mesh.normals_split_custom_set_from_vertices(normals)
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    return obj

def parent_name(name):
    if name=='Spine':return 'Hips'
    if name=='Chest':return 'Spine'
    if name=='Head' or name.startswith('UpperArm'):return 'Chest'
    if name.startswith('Thigh'):return 'Hips'
    for child,parent in [('Forearm','UpperArm'),('Hand','Forearm'),('Shin','Thigh'),('Foot','Shin'),('WeaponGrip','Hand')]:
        if name.startswith(child):return parent+name[-1]
    return None

def skin(obj,arm,names,primary,secondary=None,secondary_weight=None):
    groups=[obj.vertex_groups.new(name=name) for name in names]
    if secondary is None:
        for i,group in enumerate(groups):
            selected=np.flatnonzero(np.asarray(primary)==i).tolist()
            if selected:group.add(selected,1,'REPLACE')
    else:
        buckets={}
        for i,(a,b,w) in enumerate(zip(primary,secondary,secondary_weight)):
            if a==b or w==0:buckets.setdefault((int(a),1.0),[]).append(i)
            else:
                if w<1:buckets.setdefault((int(a),round(1-float(w),6)),[]).append(i)
                if w>0:buckets.setdefault((int(b),round(float(w),6)),[]).append(i)
        for (index,weight),ids in buckets.items():groups[index].add(ids,weight,'REPLACE')
    modifier=obj.modifiers.new('Existing hero bone contract','ARMATURE');modifier.object=arm
    obj.parent=arm

def runtime_scene(kind,data):
    reset();colors=PALETTES[kind];mats=[]
    for i,color in enumerate(colors):
        mats.append(material(f'{kind}_Surface_{i}',color,.64 if i==2 and kind!='CyberShark' else .20,
                             .69 if kind=='CyberShark' and i==2 else .36,
                             i==3 or kind=='UmbralMage' and i in (4,5),kind=='CyberShark' and i==2))
    if kind=='CyberShark':
        # Slot 2 contains both garments and metal; use the same explicit tags as Unity.
        mats[2]=material('Existing naval armor',(.035,.065,.078),.55,.40)
        mats.append(material('Nerok woven brown outfit',(.22,.12,.065),0,.78,fabric=True))
        mats.extend([material('Black shark eyes',(.003,.004,.006),0,.19),
                     material('Slate-grey head',(.22,.255,.28),0,.44),
                     material('Shark gums',(.10,.028,.032),0,.58),
                     material('Bronze iris',(.12,.068,.027),0,.30),
                     material('Ivory teeth',(.87,.865,.79),0,.38)])
    vertices=np.asarray(data['vertices']).reshape(-1,3);normals=np.asarray(data['normals']).reshape(-1,3)
    faces=[];indices=[];cloth=set(data.get('clothingVertices',[]));mouth=set(data.get('mouthVertices',[]))
    names=data['boneNames'];bones=data['boneIndices'];groups=data.get('groups',[0]*len(vertices))
    for m,sub in enumerate(data['submeshes']):
        for face in np.asarray(sub['triangles']).reshape(-1,3):
            target=m
            if kind=='CyberShark' and groups[face[0]]==0:
                bound=[names[bones[i]] for i in face];tagged=all(i in cloth for i in face)
                if all(n.startswith('UpperArm') for n in bound) and m in (2,4) and not tagged:continue
                if all(n.startswith('Forearm') for n in bound) and m!=1 and not tagged:continue
                if all(n=='Head' for n in bound):
                    target={0:9 if all(i in mouth for i in face) else 7,1:8,4:10,5:11}.get(m,m)
                elif m==2 and tagged:target=6
                elif m==2 and all(n.startswith('Hand') for n in bound):target=1
            faces.append(face.tolist());indices.append(target)
    body=mesh_object('DF_Anatomy',[xyz(v) for v in vertices],faces,indices,mats,[xyz(n) for n in normals])
    arm_data=bpy.data.armatures.new('DF_Skeleton');arm=bpy.data.objects.new('DF_Skeleton',arm_data)
    bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for name,anchor in zip(names,data['anchors']):
        bone=arm_data.edit_bones.new(name);bone.head=xyz(anchor['head']);bone.tail=xyz(anchor['tail'])
        if (bone.tail-bone.head).length<.0001:bone.tail=bone.head+Vector((0,0,.01))
    for name in names:
        parent=parent_name(name)
        if parent:arm_data.edit_bones[name].parent=arm_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    skin(body,arm,names,bones,data.get('boneIndices2'),data.get('boneWeights2'))
    body['shipping_resource']=f'Assets/Alterverse/Resources/{kind}/Model.bytes'
    body['design_revision']=REVISION
    # The runtime fits the mesh to the original hero's animated rig. These source
    # bones express its unchanged authored anchors, without creating new animations.
    return body,arm

def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

def studio():
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU'
    scene.cycles.samples=20;scene.cycles.use_denoising=True
    scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB'
    scene.render.image_settings.compression=70
    scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.095,.115,.135,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
    scene.view_settings.view_transform='AgX'
    for name,position,power,size,color in [
        ('Key',(-2.2,-3.4,3.5),500,3.0,(.86,.93,1)),
        ('Fill',(2.5,-1.0,2.8),360,2.4,(1,.86,.70)),
        ('Rim',(1.0,2.0,2.9),650,2.2,(.72,.83,1))]:
        lamp=bpy.data.lights.new(name,'AREA');lamp.energy=power;lamp.shape='DISK';lamp.size=size;lamp.color=color
        obj=bpy.data.objects.new(name,lamp);bpy.context.collection.objects.link(obj);obj.location=position;aim(obj,(0,0,1))
    camera=bpy.data.cameras.new('Geometry review camera');obj=bpy.data.objects.new('Geometry review camera',camera)
    bpy.context.collection.objects.link(obj);camera.type='ORTHO';scene.camera=obj
    return obj

def render(path,target,scale,angle=24):
    scene=bpy.context.scene;cam=scene.camera;a=math.radians(angle)
    cam.location=Vector(target)+Vector((4*math.sin(a),-4*math.cos(a),1.05))
    cam.data.ortho_scale=scale;aim(cam,target);scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)
    print('NATIVE_PREVIEW',path.name,flush=True)

def export_fbx(path,arm,animated=False):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.data.objects:
        if obj.name.startswith('DF_'):obj.select_set(True)
    bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=animated,
        bake_anim_use_all_actions=animated,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.5,path_mode='AUTO')
    assert path.exists() and path.stat().st_size>10000

def build_shoulders(arm):
    p=Parts();names=[b.name for b in arm.data.bones]
    for side,sign in [('L',-1),('R',1)]:
        bone=names.index('UpperArm'+side)
        for layer in range(3):
            c=np.array([sign*(.195+.008*layer),1.470-.034*layer,.063])
            r=np.array([.103-.008*layer,.058,.096-.006*layer]);rows=9;columns=20
            def sample(a,b,offset):
                radial=np.array([sign*math.sin(a)*math.sin(b),math.cos(a),math.sin(a)*math.cos(b)])
                return c+(r+offset)*radial
            angles=np.linspace(.18,1.92,rows+1);azimuths=np.linspace(.16,2.83,columns+1)
            outer=np.array([sample(a,b,0) for a in angles for b in azimuths]);inner=np.array([sample(a,b,-.0032) for a in angles for b in azimuths])
            verts=np.vstack([outer,inner]);n=len(outer);faces=[]
            for row in range(rows):
                for col in range(columns):
                    a=row*(columns+1)+col;b=a+1;d=a+columns+1;e=d+1
                    faces.extend([[a,b,e],[a,e,d],[a+n,e+n,b+n],[a+n,d+n,e+n]])
            perimeter=list(range(columns+1))+[row*(columns+1)+columns for row in range(1,rows+1)]+list(range(n-2,n-columns-2,-1))+[row*(columns+1) for row in range(rows-1,0,-1)]
            for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):faces.extend([[a,a+n,b+n],[a,b+n,b]])
            # Orient each external face out of the fitted shoulder shell.
            for face in faces:
                tri=verts[face];normal=np.cross(tri[1]-tri[0],tri[2]-tri[0]);direction=tri.mean(0)-c
                is_inner=all(i>=n for i in face)
                if np.dot(normal,direction)*( -1 if is_inner else 1)<0:face.reverse()
            p.add(f'formed gold shoulder lame {side}-{layer+1}',verts,faces,0,bone)
            # Smaller ivory leaves follow the same compound curve, inset from the gold rim.
            a_values=np.linspace(.29,1.79,rows+1);b_values=np.linspace(.27,2.72,columns+1)
            inset=[sample(a,b,.001) for a in a_values for b in b_values];inset_faces=[]
            for row in range(rows):
                for col in range(columns):
                    a=row*(columns+1)+col;b=a+1;d=a+columns+1;e=d+1
                    inset_faces.extend([[a,b,e],[a,e,d]])
            if sign>0:inset_faces=[f[::-1] for f in inset_faces]
            p.add(f'ivory enamel leaf inlay {side}-{layer+1}',inset,inset_faces,1,bone)
            for edge in [list(outer[:columns+1]),list(outer[-columns-1:])]:
                p.tube('rounded gold shoulder rim',edge,.0017,0,bone,8)
            # Raised branching filigree runs over the physical curved surface.
            for offset in [-.30,0,.30]:
                line=[sample(a,1.49+offset*math.sin(a*1.7),.0024) for a in np.linspace(.35,1.65,15)]
                p.tube('engraved botanical gold vein',line,.0010,0,bone,6)
            for b in [.48,2.49]:
                loc=sample(.82,b,.004)
                p.ellipsoid('shoulder gold recessed rivet',loc,[.0034,.0034,.0034],0,bone,10,6)
            if layer==0:
                loc=sample(.85,.92,.006)
                p.ellipsoid('gold healing gem bezel',loc,[.008,.009,.008],0,bone,16,10)
                p.ellipsoid('emerald shoulder resonance stone',loc+[0,.002,.005],[.0055,.007,.005],2,bone,18,10)
                p.ellipsoid('small healing light',loc+[0,.003,.009],[.0023,.003,.0018],3,bone,12,8)
    return p,names

def rig_signature(arm):
    return {bone.name:{'head':list(bone.head_local),'tail':list(bone.tail_local),'parent':bone.parent.name if bone.parent else None} for bone in arm.data.bones}

def action_signature():
    result={}
    for action in bpy.data.actions:
        curves=[]
        try:collection=action.fcurves
        except AttributeError:collection=[]
        for curve in collection:
            curves.append([curve.data_path,curve.array_index,[[*k.co,k.interpolation] for k in curve.keyframe_points]])
        result[action.name]=hashlib.sha256(json.dumps(curves,sort_keys=True).encode()).hexdigest()
    return result

def tahla(root,art,report):
    source=root/'ArtSource/Heroes/Mira.blend'
    with tempfile.TemporaryDirectory() as scratch:
        original=Path(scratch)/'Mira-original.blend';original.write_bytes(baseline(source))
        bpy.ops.wm.open_mainfile(filepath=str(original))
    bpy.context.preferences.filepaths.save_version=0
    arm=bpy.data.objects['DF_Skeleton'];body=bpy.data.objects['DF_Anatomy']
    rig_before=rig_signature(arm);actions_before=action_signature()
    assert len(actions_before)>=9,('Expected original hero animations',list(actions_before))
    bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table()
    deform=bm.verts.layers.deform.active;groups={g.index:g.name for g in body.vertex_groups};seen=set();remove=[];islands=[]
    for first in bm.verts:
        if first in seen:continue
        stack=[first];seen.add(first);component=[]
        while stack:
            v=stack.pop();component.append(v)
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other not in seen:seen.add(other);stack.append(other)
        hi=max(v.co.z for v in component)
        bound={groups.get(i,'?') for v in component for i,w in v[deform].items() if w>.5}
        mats={body.data.materials[f.material_index].name for v in component for f in v.link_faces}
        if len(component)<2000 and hi>1.3 and len(bound)==1 and next(iter(bound)).startswith('UpperArm') and mats<={'Tahla engraved warm gold','Tahla warm ivory silk'}:
            remove.extend(component);islands.append(len(component))
    assert len(islands)==14 and len(remove)==990,('Unexpected Tahla shoulder selection',islands)
    removed=set(remove);preserved=[tuple(v.co) for v in bm.verts if v not in removed]
    bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(body.data);bm.free()
    assert [tuple(v.co) for v in body.data.vertices]==preserved
    p,names=build_shoulders(arm)
    gold=bpy.data.materials.get('Tahla engraved warm gold');ivory=bpy.data.materials.get('Tahla warm ivory silk')
    mats=[gold,ivory,material('Tahla inset emerald',(.04,.31,.13),.12,.22),material('Tahla healing glints',(.18,.82,.37),.1,.30,True)]
    obj=mesh_object('Tahla_new_shoulder_plates',[xyz(v) for v in p.v],p.f,p.m,mats,[xyz(n) for n in p.normals()])
    skin(obj,arm,names,p.b)
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);obj.select_set(True)
    bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
    assert [tuple(v.co) for v in body.data.vertices[:len(preserved)]]==preserved
    assert rig_signature(arm)==rig_before and action_signature()==actions_before
    body['shoulder_redesign']=REVISION
    obj_source(art/'Tahla-shoulders.obj',p,['Gold','Ivory','Emerald','HealingLight'],PALETTES['Tahla'])
    export_fbx(root/'Assets/Alterverse/Resources/AuthoredHeroes/Mira.fbx',arm,True)
    report['models']['Tahla']={'removedShoulderIslands':14,'removedVertices':990,'preservedVertices':len(preserved),
        'bodyPositionsExact':True,'rigExact':True,'originalActionsExact':list(actions_before),
        'newVertices':len(p.v),'newTriangles':len(p.f),'newParts':len(p.parts)}
    # Preserve the authored scene and camera; generate a separate neutral review scene setup.
    bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    for obj in bpy.data.objects:
        if obj.type=='LIGHT' or obj.type=='CAMERA':obj.hide_render=True
    arm.data.pose_position='REST';studio()
    render(art/'Tahla-body.png',(0,0,.95),2.30,22)
    render(art/'Tahla-shoulders.png',(0,-.02,1.43),.77,25)
    print('TAHLA_COMPLETE',json.dumps(report['models']['Tahla']),flush=True)

def export_all(root,results,report):
    art=root/'ArtSource/Heroes/PartsRedesign';art.mkdir(parents=True,exist_ok=True)
    for kind,label in [('CyberRanger','Sivra'),('UmbralMage','Ione'),('CyberShark','Nerok')]:
        data,p=results[kind];body,arm=runtime_scene(kind,data)
        export_fbx(art/(label+'-model.fbx'),arm)
        studio();bpy.ops.wm.save_as_mainfile(filepath=str(art/(label+'-model.blend')),compress=True)
        render(art/(label+'-body.png'),(0,0,1.0),2.65 if label=='Nerok' else 2.35,22)
        if label=='Nerok':
            render(art/'Nerok-outfit-front.png',(0,0,1.12),1.75,12)
            render(art/'Nerok-outfit-back.png',(0,.02,1.12),1.75,165)
        else:
            cy=float(data['anchors'][data['boneNames'].index('Head')]['head'][1])+.205
            render(art/(label+'-head.png'),(0,-.025,cy),.48 if label=='Sivra' else .51,22)
        report['models'][label]['blenderSourceAndSkinnedFbx']=True
        print('BLENDER_MODEL_COMPLETE',label,flush=True)
    tahla(root,art,report)
