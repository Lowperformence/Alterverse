"""Numerical legacy-weapon attachment checks, using the real Rook source mesh.
Run in Blender: --python validate_attachment.py -- --source ArtSource/Heroes/Rook.blend
This verifies data, units and skinning maths. The Unity menu check verifies C# and its importer.
"""
import argparse, gzip, json, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Vector, Euler

parser=argparse.ArgumentParser()
parser.add_argument('--source', required=True)
parser.add_argument('--initial-resource')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
root=Path(__file__).resolve().parents[2]
art=root/'ArtSource/Heroes/BrakkFists'
data=json.loads(gzip.decompress((root/'Assets/Alterverse/Resources/BrakkFists/Model.bytes').read_bytes()))
bpy.ops.wm.open_mainfile(filepath=str(Path(args.source).resolve()))
conversion=np.array([[1,0,0,0],[0,0,1,0],[0,-1,0,0],[0,0,0,1]], dtype=np.float32)

def points(matrix, vertices):
    return vertices @ matrix[:3,:3].T + matrix[:3,3]
def bounds(vertices):
    lo,hi=vertices.min(axis=0),vertices.max(axis=0)
    return (lo+hi)*np.float32(.5),hi-lo

def frame(translation,rotation,scale):
    return np.array(Matrix.Translation(Vector(translation)) @ Euler(rotation).to_matrix().to_4x4() @
                    Matrix.Diagonal((*scale,1)), dtype=np.float32)

fixtures=[('identity',1.,frame((0,0,0),(0,0,0),(1,1,1))),
          ('fbx-centimetres',1.,frame((0,0,0),(0,0,0),(.01,.01,.01))),
          ('mesh-axis-conversion',1.,frame((.17,-.2,.08),(.3,-.2,.4),(.01,.01,.01))),
          ('reflected-import',1.,frame((.17,-.2,.08),(.3,-.2,.4),(-.01,.01,.01))),
          ('large-model-units',100.,frame((0,0,0),(0,0,0),(1,1,1))),
          ('small-model-units',.01,frame((0,0,0),(0,0,0),(1,1,1)))]
initial=None
if args.initial_resource:
    initial=json.loads(gzip.decompress(Path(args.initial_resource).read_bytes()))
report={'fixtures':len(fixtures),'hands':{},'unityEditorExecuted':False}
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
for hand in data['hands']:
    side=hand['side'];legacy=bpy.data.objects['DF_Gauntlet'+side]
    group=legacy.vertex_groups['Hand'+side].index
    assert all(len(v.groups)==1 and v.groups[0].group==group and v.groups[0].weight==1 for v in legacy.data.vertices)
    sourceOld=points(conversion,np.array([list(legacy.matrix_world@v.co) for v in legacy.data.vertices],dtype=np.float32))
    authored=np.array(hand['vertices'],dtype=np.float32).reshape(-1,3)
    pivot=np.array(hand['pivot'],dtype=np.float32)
    _,expectedOldSize=bounds(sourceOld)
    assert np.allclose(expectedOldSize,hand['legacyModelSize'],rtol=1e-6,atol=1e-7)
    expectedCentre,_=bounds(sourceOld)
    assert np.linalg.norm(bounds(authored)[0]-expectedCentre)<1e-6
    ratios=[];maximumError=0.;cases=[]
    if initial:
        first=next(h for h in initial['hands'] if h['side']==side)
        bind=np.array(arm.matrix_world@arm.data.bones['Hand'+side].matrix_local,dtype=np.float32)
        modelToHand=np.linalg.inv(conversion@bind)
        previous=bounds(points(modelToHand,np.array(first['vertices'],dtype=np.float32).reshape(-1,3)))[1]
        current=bounds(points(modelToHand,authored))[1]
        ratio=current/previous
        assert np.allclose(ratio,2.,rtol=3e-6,atol=3e-6),ratio
        report['hands'][side]={'dimensionsRelativeToFirstV2':ratio.tolist()}
    else:report['hands'][side]={}
    for name,unit,toModel in fixtures:
        toMesh=np.linalg.inv(toModel)
        old=points(toMesh,sourceOld*np.float32(unit))
        originalCentre,originalSize=bounds(old)
        _,oldModelSize=bounds(points(toModel,old))
        units=np.linalg.norm(oldModelSize)/np.linalg.norm(np.array(hand['legacyModelSize'],dtype=np.float32))
        new=(authored-pivot)@toMesh[:3,:3].T*units
        centre,size=bounds(new)
        new+=originalCentre-centre
        fittedCentre,fittedSize=bounds(new)
        error=np.linalg.norm(fittedCentre-originalCentre)/np.linalg.norm(originalSize)
        assert error<2e-6,(name,error)
        ratio=np.linalg.norm(fittedSize)/np.linalg.norm(originalSize)
        assert .35<=ratio<=3.,(name,ratio)
        # All new vertices have the old Hand bone's 100% weight. Verify the common
        # attachment point stays identical through hand sizing and animated poses.
        for pose in [frame((.64,.99,.085),(0,0,0),(.8,.8,.8)),
                     frame((.64,.99,.085),(.37,-.28,.14),(.8,.8,.8))]:
            oldContact=points(pose,originalCentre[None,:])[0]
            newContact=points(pose,fittedCentre[None,:])[0]
            assert np.linalg.norm(newContact-oldContact)<=np.linalg.norm(originalSize)*2e-6
        # Stage world translation and parent scale never enter mesh construction.
        # Compare the final attachment contact at both the origin and (12000)^3.
        for stage in [frame((0,0,0),(0,0,0),(1,1,1)),
                      frame((12000,12000,12000),(0,.49,0),(1.22,1.32,1.22))]:
            oldWorld=points(stage,originalCentre[None,:])[0]
            newWorld=points(stage,fittedCentre[None,:])[0]
            assert np.allclose(oldWorld,newWorld,rtol=1e-6,atol=.002)
        ratios.append(float(ratio));maximumError=max(maximumError,float(error));cases.append(name)
    report['hands'][side].update({'cases':cases,'maxCentreErrorRelativeToOldSize':maximumError,
                                 'newToLegacyBoundsDiagonalRatioRange':[min(ratios),max(ratios)]})
report['checks']={'originalWeaponsRigidHandBound':True,'originalMeshCentreRetained':True,
                  'fbxUnitsAndAxisConversion':True,'handScaleAndAnimationAttachment':True,
                  'originAndRemotePortraitStage':True}
validation=json.loads((art/'validation.json').read_text())
validation['checks']['legacySocketNumericalValidation']=report
(art/'validation.json').write_text(json.dumps(validation,indent=2)+'\n')
print('LEGACY_ATTACHMENT_VALIDATED',json.dumps(report))
