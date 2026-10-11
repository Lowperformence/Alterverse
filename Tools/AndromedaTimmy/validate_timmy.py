"""Blender source/FBX contract checks; does not substitute a Unity import/device test.
Run: blender --background --python-exit-code 1 --python Tools/AndromedaTimmy/validate_timmy.py
"""
from pathlib import Path
import json, math
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/AndromedaTimmy'
EXPECTED={'Root','Hips','Spine','Chest','Head','Tail'}|{b+s for s in ('L','R') for b in ('UpperArm','Forearm','Hand','WeaponGrip','Thigh','Shin','Foot')}

def check(label):
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1,(label,'one armature')
    rig=rigs[0];assert set(rig.data.bones.keys())==EXPECTED,(label,'19 shared rig bones + tail')
    assert rig.data.bones['Tail'].parent.name=='Hips',(label,'tail attached to hips')
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert {o.name for o in meshes}=={'DF_Anatomy','DF_WeaponL','DF_WeaponR','DF_Visor'},(label,'four skinned renderers')
    vertices=[];triangles=0
    for ob in meshes:
        assert any(m.type=='ARMATURE' and m.object==rig for m in ob.modifiers),(label,ob.name,'skin modifier')
        assert len(ob.data.materials)>0,(label,ob.name,'materials')
        groups={g.index:g.name for g in ob.vertex_groups}
        for v in ob.data.vertices:
            assert len(v.groups)>0 and abs(sum(g.weight for g in v.groups)-1)<1e-5,(label,ob.name,'normalized weights')
            if ob.name.startswith('DF_Weapon'):
                assert len(v.groups)==1 and groups[v.groups[0].group]=='WeaponGrip'+ob.name[-1],(label,'rigid claw weights')
            if ob.name=='DF_Visor':
                assert len(v.groups)==1 and groups[v.groups[0].group]=='Head',(label,'visor follows only the head')
            p=ob.matrix_world@v.co;assert all(math.isfinite(c) for c in p),(label,'finite vertices');vertices.append(p)
        for f in ob.data.polygons:
            assert f.area>1e-12,(label,ob.name,'nondegenerate polygon')
            triangles+=len(f.vertices)-2
    assert 0<triangles<=18000,(label,'mobile triangle budget',triangles)
    for side in ('L','R'):
        palm=bpy.data.objects['DF_PalmGrip'+side];contact=bpy.data.objects['DF_HandleContact'+side]
        assert (palm.matrix_world.translation-contact.matrix_world.translation).length<.001,(label,'palm/claw contact')
        jet=bpy.data.objects['DF_TimmyJet'+side]
        assert jet.parent==rig and jet.parent_bone=='Chest',(label,'jet nozzle follows backpack')
        assert abs(jet.matrix_world.translation.x-(-.13 if side=='L' else .13))<.001,(label,'jet nozzle position')
    lo=Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
    hi=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
    height=max(hi-lo);assert 1.4<height<2.2,(label,'meter scale',height)
    visor=next(o for o in meshes if o.name=='DF_Visor')
    assert len(visor.data.materials)==1,(label,'one glass material')
    glass=visor.data.materials[0]
    assert glass.name.startswith('Timmy light gray visor glass'),(label,'glass material identity')
    alpha=glass.node_tree.nodes.get('Principled BSDF').inputs['Alpha'].default_value
    assert abs(alpha-.50)<1e-5,(label,'50% glass transparency',alpha)
    if label=='Blender source':
        assert abs(glass.diffuse_color[3]-.50)<1e-5,(label,'viewport glass transparency')
    assert max(glass.diffuse_color[:3])-min(glass.diffuse_color[:3])<.06,(label,'neutral light gray tint')
    points=[visor.matrix_world@v.co for v in visor.data.vertices]
    cx=(min(p.x for p in points)+max(p.x for p in points))/2
    cz=(min(p.z for p in points)+max(p.z for p in points))/2
    diameter_x=max(p.x for p in points)-min(p.x for p in points)
    diameter_z=max(p.z for p in points)-min(p.z for p in points)
    assert abs(diameter_x-diameter_z)<1e-5,(label,'round visor silhouette')
    radius=diameter_x/2;rim_y=max(p.y for p in points);depth=rim_y-min(p.y for p in points)
    assert .18<depth<.23,(label,'curved glass dome')
    normals=visor.matrix_world.to_3x3().inverted().transposed()
    assert all((normals@f.normal).normalized().y<-.2 for f in visor.data.polygons),(label,'one outward-facing glass surface')
    # Verify actual muzzle/eyes/whisker vertices fit behind the dome, not just its center.
    sphere=(radius*radius+depth*depth)/(2*depth);sphere_y=rim_y+sphere-depth
    anatomy=next(o for o in meshes if o.name=='DF_Anatomy')
    assert anatomy.data.uv_layers.active is not None,(label,'surface texture UVs')
    groups={g.index:g.name for g in anatomy.vertex_groups}
    paws={}
    for side in ('L','R'):
        hand=rig.data.bones['Hand'+side]
        wrist=rig.matrix_world@hand.head_local
        forward=(rig.matrix_world.to_3x3()@(hand.tail_local-hand.head_local)).normalized()
        across=(rig.matrix_world.to_3x3()@Vector((1,0,0))).normalized()
        depth=across.cross(forward).normalized()
        paw={v.index:anatomy.matrix_world@v.co-wrist for v in anatomy.data.vertices
             if any(groups[g.group]=='Hand'+side and g.weight>.99 for g in v.groups)}
        assert len(paw)>100,(label,'authored forepaw',side)
        lengths=[p.dot(forward) for p in paw.values()];widths=[p.dot(across) for p in paw.values()]
        depths=[p.dot(depth) for p in paw.values()]
        length=max(lengths)-min(lengths);width=max(widths)-min(widths);thickness=max(depths)-min(depths)
        assert length/width>1.3 and thickness/width<.65,(label,'long flattened feline paw',side,length,width,thickness)
        wrist_width=max(abs(p.dot(across)) for p in paw.values() if p.dot(forward)<0)*2
        assert wrist_width<width*.80,(label,'paw narrows into wrist',side)
        # The joined anatomy must retain one closed paw surface, not four
        # disconnected round finger balls or an open seam at the wrist.
        edges={};neighbors={i:set() for i in paw}
        for f in anatomy.data.polygons:
            if not all(i in paw for i in f.vertices):continue
            indices=list(f.vertices)
            for i,j in zip(indices,indices[1:]+indices[:1]):
                key=tuple(sorted((i,j)));edges[key]=edges.get(key,0)+1
                neighbors[i].add(j);neighbors[j].add(i)
        assert edges and all(count==2 for count in edges.values()),(label,'closed manifold paw',side)
        seen=set();pending=[next(iter(paw))]
        while pending:
            i=pending.pop()
            if i in seen:continue
            seen.add(i);pending.extend(neighbors[i]-seen)
        assert len(seen)==len(paw),(label,'continuous paw surface',side)
        rim={i:p for i,p in paw.items() if abs(p.dot(depth))<.00001}
        tips=[i for i,p in rim.items() if p.dot(forward)>.12 and
              all(p.dot(forward)>rim[j].dot(forward)+.000001 for j in neighbors[i] if j in rim)]
        assert len(tips)==4,(label,'four scalloped feline toes',side,len(tips))
        weapon=next(o for o in meshes if o.name=='DF_Weapon'+side)
        assert min((weapon.matrix_world@v.co-(p+wrist)).length for v in weapon.data.vertices for p in paw.values())<.008,(label,'energy claws meet the paw',side)
        paws[side]={'length':round(length,4),'width':round(width,4),'thickness':round(thickness,4),'toe_tips':len(tips),'closed_surface':True}
    assert paws['L']==paws['R'],(label,'matching left/right paw proportions')
    tail_vertices=[anatomy.matrix_world@v.co for v in anatomy.data.vertices if any(groups[g.group]=='Tail' and g.weight>.99 for g in v.groups)]
    assert len(tail_vertices)>100 and max(abs(p.x) for p in tail_vertices)<.05,(label,'tail centered behind body')
    assert max(p.y for p in tail_vertices)>.68,(label,'tail extends backwards')
    for mat in anatomy.data.materials:
        if not mat.name.startswith(('Timmy orange tabby fur','Timmy cinnamon tabby fur','Timmy muzzle cream','Timmy woven astronaut fabric')):continue
        bs=mat.node_tree.nodes.get('Principled BSDF')
        assert bs.inputs['Base Color'].is_linked and bs.inputs['Normal'].is_linked,(label,mat.name,'baked albedo + normal')
        albedo=bs.inputs['Base Color'].links[0].from_node
        assert albedo.type=='TEX_IMAGE' and albedo.image is not None,(label,mat.name,'baked albedo binding')
        assert tuple(albedo.image.size)==(1024,1024),(label,'loaded surface atlas')
        if label=='Blender source':assert albedo.image.packed_file is not None,(label,'self-contained packed texture')
    face_names=('Timmy orange tabby fur','Timmy muzzle cream','Timmy graphite seals','Timmy emerald eyes','Timmy warm feline nose')
    face_vertices={i for f in anatomy.data.polygons if anatomy.data.materials[f.material_index].name.startswith(face_names) for i in f.vertices}
    groups={g.index:g.name for g in anatomy.vertex_groups};covered=0
    for i in face_vertices:
        v=anatomy.data.vertices[i]
        if not any(groups[g.group]=='Head' and g.weight>.99 for g in v.groups):continue
        p=anatomy.matrix_world@v.co;r2=(p.x-cx)**2+(p.z-cz)**2
        # Graphite is also used on the external chin guard, below the cat's face.
        if p.z<1.40:continue
        if p.y>=rim_y or r2>=radius*radius:continue
        glass_y=sphere_y-math.sqrt(max(0,sphere*sphere-r2))
        assert p.y-glass_y>.001,(label,'face remains behind visor',tuple(p))
        covered+=1
    assert covered>100,(label,'visor covers the face')
    material_points={}
    for f in anatomy.data.polygons:
        name=anatomy.data.materials[f.material_index].name
        for i in f.vertices:
            v=anatomy.data.vertices[i]
            if any(groups[g.group]=='Head' and g.weight>.99 for g in v.groups):
                material_points.setdefault(name,[]).append(anatomy.matrix_world@v.co)
    def points_for(prefix):return [p for name,ps in material_points.items() if name.startswith(prefix) for p in ps]
    eyes=points_for('Timmy emerald eyes');nose_points=points_for('Timmy warm feline nose')
    head_fur=points_for('Timmy orange tabby fur')
    assert head_fur and max(p.z for p in head_fur)<1.716,(label,'no ears protrude above the enclosed cat head')
    stripe_materials=[(i,m) for i,m in enumerate(anatomy.data.materials) if m.name.startswith('Timmy blue back stripe energy')]
    assert len(stripe_materials)==1,(label,'dedicated blue back stripe material')
    stripe_index,stripe_material=stripe_materials[0]
    stripe_vertices={i for f in anatomy.data.polygons if f.material_index==stripe_index for i in f.vertices}
    stripe_points=[anatomy.matrix_world@anatomy.data.vertices[i].co for i in stripe_vertices]
    assert stripe_points and max(abs(p.x) for p in stripe_points)<.015 and min(p.y for p in stripe_points)>.265,(label,'stripe exposed on back of backpack')
    assert max(p.z for p in stripe_points)-min(p.z for p in stripe_points)>.16,(label,'vertical blue back stripe')
    for i in stripe_vertices:
        v=anatomy.data.vertices[i]
        assert len(v.groups)==1 and groups[v.groups[0].group]=='Chest',(label,'back stripe follows backpack')
    stripe_bs=stripe_material.node_tree.nodes.get('Principled BSDF')
    emission=stripe_bs.inputs.get('Emission Color',stripe_bs.inputs.get('Emission')).default_value
    assert emission[2]>.3 and emission[2]>emission[0]*3 and stripe_bs.inputs['Emission Strength'].default_value>0,(label,'blue stripe emits light')
    for sign in (-1,1):
        eye_points=[p for p in eyes if sign*p.x>0]
        assert eye_points,(label,'two feline eyes')
        ew=max(p.x for p in eye_points)-min(p.x for p in eye_points)
        eh=max(p.z for p in eye_points)-min(p.z for p in eye_points)
        assert ew/eh>1.6,(label,'almond eye proportions',ew,eh)
    assert nose_points and max(p.z for p in nose_points)<min(p.z for p in eyes),(label,'small nose below the eyes')
    muzzle=[p for p in points_for('Timmy muzzle cream') if abs(p.x)<.09 and 1.46<p.z<1.53]
    assert min(p.y for p in muzzle)<min(p.y for p in eyes)-.025,(label,'projecting feline whisker pads')
    return {'bones':len(rig.data.bones),'shared_hero_bones':19,'paws':paws,'feline_face_proportions':True,'external_ears':False,'blue_emissive_back_stripe':True,'tail_centered':True,'tail_vertices':len(tail_vertices),'jet_nozzle_sockets':2,'triangles':triangles,'renderers':len(meshes),'bounds_min':list(lo),'bounds_max':list(hi),'visor_opacity':round(alpha,2),'visor_head_bound':True,'covered_face_vertices':covered}

bpy.ops.wm.open_mainfile(filepath=str(ART/'AndromedaTimmy.blend'))
source=check('Blender source')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Alterverse/Resources/AuthoredHeroes/Timmy.fbx'))
fbx=check('FBX round trip')
assert source['triangles']==fbx['triangles'],'round-trip triangle count'
for kind in ('Albedo','Normal'):
    img=bpy.data.images.load(str(ROOT/f'Assets/Alterverse/Resources/AuthoredHeroes/TimmyTextures/TimmySurface_{kind}.png'),check_existing=False)
    assert tuple(img.size)==(1024,1024),('mobile surface texture',kind)
    bpy.data.images.remove(img)
for i in range(5):
    img=bpy.data.images.load(str(ROOT/f'Assets/Alterverse/Resources/AndromedaTimmy/Icons/{i}.png'),check_existing=False)
    assert tuple(img.size)==(1254,1254),('painted icon source resolution',i)
    bpy.data.images.remove(img)
report={'source':source,'fbx_round_trip':fbx,'icons':5,'icon_resolution':1254,'runtime_icon_import_max_size':256,'native_unity_import_tested':False}
(ART/'validation-report.json').write_text(json.dumps(report,indent=2)+'\n')
print('TIMMY ART PASS: source + FBX round trip, 19 shared bones + tail, four weighted renderers, 50% transparent round visor, centered tail, nozzle/contact sockets, baked fur/fabric, triangle budget, five painted icons.')
