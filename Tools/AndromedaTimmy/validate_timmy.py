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
    assert abs(alpha-.20)<1e-5,(label,'80% glass transparency',alpha)
    if label=='Blender source':
        assert abs(glass.diffuse_color[3]-.20)<1e-5,(label,'viewport glass transparency')
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
    face_names=('Timmy orange tabby fur','Timmy muzzle cream','Timmy graphite seals','Timmy emerald eyes')
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
    return {'bones':len(rig.data.bones),'shared_hero_bones':19,'tail_centered':True,'tail_vertices':len(tail_vertices),'jet_nozzle_sockets':2,'triangles':triangles,'renderers':len(meshes),'bounds_min':list(lo),'bounds_max':list(hi),'visor_opacity':round(alpha,2),'visor_head_bound':True,'covered_face_vertices':covered}

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
print('TIMMY ART PASS: source + FBX round trip, 19 shared bones + tail, four weighted renderers, 80% transparent round visor, centered tail, nozzle/contact sockets, baked fur/fabric, triangle budget, five painted icons.')
