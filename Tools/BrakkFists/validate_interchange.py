"""Check the actual supplementary FBX against the authored source and runtime resource."""
from pathlib import Path
import bpy,gzip,json,math
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Heroes/BrakkFists'
data=json.loads(gzip.decompress((ROOT/'Assets/Alterverse/Resources/BrakkFists/Model.bytes').read_bytes()))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ART/'BrakkFists.fbx'),use_anim=False)
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');arm.data.pose_position='REST';bpy.context.view_layer.update()
meshes=[o for o in bpy.data.objects if o.type=='MESH']
assert len(meshes)==68
conversion=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
checks={}
for side in ['L','R']:
    objects=[o for o in meshes if o.name.startswith('FGV2_'+side+'_')];assert len(objects)==34
    points=[conversion@(o.matrix_world@v.co) for o in objects for v in o.data.vertices]
    runtime=next(h for h in data['hands'] if h['side']==side)
    expected=[Vector(runtime['vertices'][i:i+3]) for i in range(0,len(runtime['vertices']),3)]
    for k in range(3):
        assert abs(min(p[k] for p in points)-min(p[k] for p in expected))<2e-5
        assert abs(max(p[k] for p in points)-max(p[k] for p in expected))<2e-5
    for o in objects:
        lookup={g.index:g.name for g in o.vertex_groups}
        assert all(len(v.groups)==1 and lookup[v.groups[0].group]=='Hand'+side and abs(v.groups[0].weight-1)<1e-6 for v in o.data.vertices)
    checks[side]={'meshParts':len(objects),'boundsMatchRuntime':True,'handWeightsRoundTrip':True}
left=next(h for h in data['hands'] if h['side']=='L')['vertices'];right=next(h for h in data['hands'] if h['side']=='R')['vertices']
l={tuple(round(x,5) for x in left[i:i+3]) for i in range(0,len(left),3)}
r={tuple(round(x,5) for x in (-right[i],right[i+1],right[i+2])) for i in range(0,len(right),3)}
assert l==r
report=json.loads((ART/'validation.json').read_text());report['checks']['fbxReimport']=checks
report['checks']['exactLeftRightMirrorAt10MicrometrePrecision']=True
(ART/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('FBX_ROUNDTRIP_VALIDATED',json.dumps(checks))
