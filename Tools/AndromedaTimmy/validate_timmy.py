"""Blender source/FBX contract checks; does not substitute a Unity import/device test.
Run: blender --background --python-exit-code 1 --python Tools/AndromedaTimmy/validate_timmy.py
"""
from pathlib import Path
import json, math
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/AndromedaTimmy'
EXPECTED={'Root','Hips','Spine','Chest','Head'}|{b+s for s in ('L','R') for b in ('UpperArm','Forearm','Hand','WeaponGrip','Thigh','Shin','Foot')}

def check(label):
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1,(label,'one armature')
    rig=rigs[0];assert set(rig.data.bones.keys())==EXPECTED,(label,'19 named rig bones')
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert {o.name for o in meshes}=={'DF_Anatomy','DF_WeaponL','DF_WeaponR'},(label,'three skinned renderers')
    vertices=[];triangles=0
    for ob in meshes:
        assert any(m.type=='ARMATURE' and m.object==rig for m in ob.modifiers),(label,ob.name,'skin modifier')
        assert len(ob.data.materials)>0,(label,ob.name,'materials')
        groups={g.index:g.name for g in ob.vertex_groups}
        for v in ob.data.vertices:
            assert len(v.groups)>0 and abs(sum(g.weight for g in v.groups)-1)<1e-5,(label,ob.name,'normalized weights')
            if ob.name.startswith('DF_Weapon'):
                assert len(v.groups)==1 and groups[v.groups[0].group]=='WeaponGrip'+ob.name[-1],(label,'rigid claw weights')
            p=ob.matrix_world@v.co;assert all(math.isfinite(c) for c in p),(label,'finite vertices');vertices.append(p)
        for f in ob.data.polygons:
            assert f.area>1e-12,(label,ob.name,'nondegenerate polygon')
            triangles+=len(f.vertices)-2
    assert 0<triangles<=12000,(label,'mobile triangle budget',triangles)
    for side in ('L','R'):
        palm=bpy.data.objects['DF_PalmGrip'+side];contact=bpy.data.objects['DF_HandleContact'+side]
        assert (palm.matrix_world.translation-contact.matrix_world.translation).length<.001,(label,'palm/claw contact')
    lo=Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
    hi=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
    height=max(hi-lo);assert 1.4<height<2.2,(label,'meter scale',height)
    return {'bones':len(rig.data.bones),'triangles':triangles,'renderers':len(meshes),'bounds_min':list(lo),'bounds_max':list(hi)}

bpy.ops.wm.open_mainfile(filepath=str(ART/'AndromedaTimmy.blend'))
source=check('Blender source')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Alterverse/Resources/AuthoredHeroes/Timmy.fbx'))
fbx=check('FBX round trip')
assert source['triangles']==fbx['triangles'],'round-trip triangle count'
for i in range(5):
    img=bpy.data.images.load(str(ROOT/f'Assets/Alterverse/Resources/AndromedaTimmy/Icons/{i}.png'),check_existing=False)
    assert tuple(img.size)==(512,512),('icon resolution',i)
    bpy.data.images.remove(img)
report={'source':source,'fbx_round_trip':fbx,'icons':5,'icon_resolution':512,'native_unity_import_tested':False}
(ART/'validation-report.json').write_text(json.dumps(report,indent=2)+'\n')
print('TIMMY ART PASS: source + FBX round trip, 19 bones, three weighted renderers, contact sockets, triangle budget, five 512px icons.')
