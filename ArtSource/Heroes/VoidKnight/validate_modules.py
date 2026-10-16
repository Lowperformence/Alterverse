"""Open every standalone .blend module and check its native editable binding."""
from pathlib import Path
import bpy,json
HERE=Path(__file__).resolve().parent
manifest=json.loads((HERE/'Modules/manifest.json').read_text())
assert len(manifest)==17
count=0
for item in manifest:
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'Modules'/item['file']))
    rigs=[o for o in bpy.data.objects if o.type=='ARMATURE'];assert len(rigs)==1
    rig=rigs[0];assert len(rig.data.bones)==19 and rig.data.pose_position=='REST'
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    assert bpy.context.scene.get('assembly')==Path(item['file']).stem
    assert set(bpy.context.scene.objects)==set(meshes+[rig]),'Module opens on an empty or unrelated scene'
    assert len(meshes)==len(item['parts']),(item['file'],len(meshes),len(item['parts']))
    for mesh in meshes:
        assert mesh.parent==rig and mesh.get('bone') in rig.data.bones
        assert any(m.type=='ARMATURE' and m.object==rig for m in mesh.modifiers)
    assert all(image.packed_file is not None for image in bpy.data.images if image.type=='IMAGE')
    count+=len(meshes)
assert count==281,count
report={'result':'PASS','opened_blend_modules':len(manifest),'editable_parts':count,'rest_bones_per_module':19,'textures_packed':True}
(HERE/'Validation/modules-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('MODULE_VALIDATION',report,flush=True)
