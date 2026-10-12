"""Read the generated FBX files back through Blender's importer."""
from pathlib import Path
import json
import bpy

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'ArtSource/Heroes/PartsRedesign'
report=json.loads((ART/'validation-report.json').read_text())
for label in ['Sivra','Ione','Nerok','Tahla']:
    path=ROOT/'Assets/Alterverse/Resources/AuthoredHeroes/Mira.fbx' if label=='Tahla' else ART/(label+'-model.fbx')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    arms=[obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE']
    assert meshes and len(arms)==1,(label,'FBX lacks a skinned model')
    arm=arms[0];names={bone.name for bone in arm.data.bones}
    assert {'Hips','Spine','Chest','Head','UpperArmL','UpperArmR','ThighL','ThighR'}<=names
    body=next(obj for obj in meshes if obj.name=='DF_Anatomy')
    assert any(mod.type=='ARMATURE' and mod.object==arm for mod in body.modifiers)
    assert all(vertex.groups for vertex in body.data.vertices),(label,'Unbound body vertex')
    check={'bones':len(names),'bodyVertices':len(body.data.vertices),'bodyFaces':len(body.data.polygons),'skinningImported':True}
    if label=='Tahla':
        action_names=[action.name for action in bpy.data.actions]
        originals=report['models'][label]['originalActionsExact']
        assert len(action_names)>=len(originals),(originals,action_names)
        assert all(any(name in action for action in action_names) for name in originals),(originals,action_names)
        check['animationClipsImported']=action_names
    report['models'][label]['fbxRoundTrip']=check
    print('FBX_ROUND_TRIP',label,json.dumps(check),flush=True)
report['validation']={'runtimePayload':'passed','nativeFbxImport':'passed','UnityEditor':'not run in authoring runner'}
(ART/'validation-report.json').write_text(json.dumps(report,indent=2)+'\n')
