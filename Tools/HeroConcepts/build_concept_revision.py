"""Build the nine dated design revisions on existing licensed/project geometry.

Preserves the original rigs, weapon groups, gameplay resource names and clips.
The added geometry is specified directly from the dated concept design briefs.
Run with Python 3.11 + bpy 4.5.14 on the authorized art branch.
"""
from pathlib import Path
import argparse, copy, gzip, hashlib, json, math, subprocess, sys, tempfile
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Heroes/ConceptRebuild'
BASE='396d3d0008d5557b5e014165594e1f9505bcb530'
REVISION='hero-concepts-20261012-v1'
sys.path.insert(0,str(ROOT/'Tools/HeroParts'))
from build_parts import Parts, amend, validate, obj_source, PALETTES

RUNTIME=[('CyberRanger','Sivra-Quell'),('UmbralMage','Ione-Vaska'),('SiegeTank','Auren-Kest'),('ChronoTraveler','The-Traveler'),('CyberShark','Nerok-Abyss')]
PALETTES['SiegeTank']=[(.028,.038,.031),(.18,.235,.135),(.38,.32,.19),(1,.32,.035),(.30,.36,.34),(.36,.16,.055),(1,.85,.015)]
PALETTES['ChronoTraveler']=[(.018,.024,.029),(.062,.082,.10),(.30,.38,.42),(.02,.65,.94),(.003,.008,.012),(.025,.18,1)]

def digest(raw):return hashlib.sha256(raw).hexdigest()

def original(path):
    rel=path.relative_to(ROOT).as_posix()
    return subprocess.run(['git','show',BASE+':'+rel],cwd=ROOT,check=True,capture_output=True).stdout

def preserve_signature(data):
    keys=['version','boneNames','anchors','markers','tailPivot','hookGrip']
    return {k:copy.deepcopy(data[k]) for k in keys if k in data}

def front_surface(data,x,y,back=False):
    """Ray intersect actual body triangles at a chosen torso x/y; no guessed depth."""
    v=np.asarray(data['vertices']).reshape(-1,3)
    groups=np.asarray(data.get('groups',[0]*len(v)))
    depths=[]
    for sub in data['submeshes']:
        f=np.asarray(sub['triangles'],int).reshape(-1,3)
        f=f[np.all(groups[f]==0,axis=1)]
        t=v[f];a=t[:,0,:2];b=t[:,1,:2];c=t[:,2,:2]
        den=(b[:,1]-c[:,1])*(a[:,0]-c[:,0])+(c[:,0]-b[:,0])*(a[:,1]-c[:,1])
        good=np.abs(den)>1e-10
        t=t[good];a=a[good];b=b[good];c=c[good];den=den[good]
        u=((b[:,1]-c[:,1])*(x-c[:,0])+(c[:,0]-b[:,0])*(y-c[:,1]))/den
        w=((c[:,1]-a[:,1])*(x-c[:,0])+(a[:,0]-c[:,0])*(y-c[:,1]))/den
        mask=(u>=-1e-5)&(w>=-1e-5)&(u+w<=1+1e-5)
        z=u*t[:,0,2]+w*t[:,1,2]+(1-u-w)*t[:,2,2]
        depths.extend(z[mask].tolist())
    assert depths,('No body surface at design attachment',x,y)
    return (min(depths)-.004) if back else (max(depths)+.004)

def chevron(p,name,x,y,z,width,height,material,bone):
    pts=[(x-width,y+height),(x,y-height),(x+width,y+height),(x+width*.72,y+height*1.34),(x,y-height*.35),(x-width*.72,y+height*1.34)]
    # A concave chevron is built from two convex halves, so no invalid fan fill.
    for side in [-1,1]:
        outline=[(x,y-height),(x+side*width,y+height),(x+side*width*.72,y+height*1.34),(x,y-height*.35)]
        p.plate(name+(' left' if side<0 else ' right'),outline,z,.002,material,bone)

def authored_runtime(data,kind):
    p=Parts();bi=data['boneNames'].index('Chest');ch=data['anchors'][bi]['head'][1]
    head=data['boneNames'].index('Head');hy=data['anchors'][head]['head'][1]
    # Fit center motifs below the neck, clear of the actual weapon/arm groups.
    top=min(hy-.08,ch+.22);center=top-.04
    if kind=='CyberRanger':
        z=front_surface(data,0,center)
        for side in [-1,1]:
            points=[(side*.010,top+.045,z+.001),(side*.010,center+.012,z+.002),(side*.035,center-.02,z+.002),(0,center-.064,z+.002)]
            p.tube('emerald split-arrow chest tracer',points,.0028,3,bi,10)
        for side in [-1,1]:
            ys=np.linspace(center-.10,center+.07,15)
            points=[(side*.059,float(y),front_surface(data,side*.059,float(y),True)) for y in ys]
            p.tube('paired rear circuit rail',points,.0028,3,bi,10)
    elif kind=='UmbralMage':
        for j in range(3):
            y=center+.035-j*.025;z=front_surface(data,0,y)
            chevron(p,'violet sternum sensor',0,y,z,.017,.007,3,bi)
        # Forehead is part of the already newly authored full head; avoid adding a second head.
        y=hy+.26;z=front_surface(data,0,y)
        p.plate('cyan crown diamond',[(0,y+.018),(.010,y),(0,y-.018),(-.010,y)],z,.002,4,head)
    elif kind=='SiegeTank':
        for x in [-.018,0,.018]:
            ys=np.linspace(center-.035,center+.047,10)
            points=[(x,float(y),front_surface(data,x,float(y))) for y in ys]
            p.tube('three amber sternum vent bars',points,.0034,3,bi,12)
    elif kind=='ChronoTraveler':
        z=front_surface(data,0,center)
        for radius in [.013,.029,.045]:p.ring('concentric cyan time lens',(0,center,z),radius,.0024,3,bi,normal=(0,0,1),segments=48)
        p.tube('split clock needle',[(0,center-.080,z),(0,center-.049,z),(-.006,center-.040,z)],.0028,3,bi,10)
        p.tube('split clock needle',[(0,center-.080,z),(0,center-.049,z),(.006,center-.040,z)],.0028,3,bi,10)
        for side in [-1,1]:
            points=[(side*.055,float(y),front_surface(data,side*.055,float(y),True)) for y in np.linspace(center-.105,center+.055,18)]
            p.tube('cyan rear timing rail',points,.003,3,bi,10)
    elif kind=='CyberShark':
        # Hardware sits on the existing authored jacket without replacing its tail opening.
        center-=.045
        for j in range(3):
            y=center-j*.049;z=front_surface(data,0,y)
            chevron(p,'bronze jacket clasp',0,y,z,.041,.014,4,bi)
            chevron(p,'cyan clasp inset',0,y,z+.0025,.032,.009,3,bi)
    assert p.v and p.f
    return p

def build_runtime():
    import blender_export as be
    for kind,slug in RUNTIME:
        resource=ROOT/f'Assets/Alterverse/Resources/{kind}/Model.bytes'
        raw=original(resource);data=json.loads(gzip.decompress(raw));before=preserve_signature(data)
        # The four previous replacements were committed in a distinct revision.
        # Read those exact resources for the three affected mechanical bodies.
        if kind in ['CyberRanger','UmbralMage','CyberShark']:
            raw=subprocess.run(['git','show','8fa75d02f0964e9df149ed3c073259fc50248d78:'+resource.relative_to(ROOT).as_posix()],cwd=ROOT,check=True,capture_output=True).stdout
            data=json.loads(gzip.decompress(raw));before=preserve_signature(data)
        p=authored_runtime(data,kind);remove=np.zeros(len(data['boneIndices']),bool)
        revised,checks=amend(data,remove,p)
        assert preserve_signature(revised)==before
        revised['designRevision']=REVISION;revised['conceptSource']=f'ArtSource/Heroes/Concepts/2026-10-12/{slug}.png'
        resource.write_bytes(gzip.compress(json.dumps(revised,separators=(',',':')).encode(),mtime=0))
        colors=PALETTES[kind]
        # SiegeTank's real export has six slots; derive fallback neutral swatches for unused slots.
        while len(colors)<len(revised['submeshes']):colors.append((.15,.18,.20))
        obj_source(OUT/(slug+'-authored-details.obj'),p,[f'Surface_{i}' for i in range(len(colors))],colors)
        body,arm=be.runtime_scene(kind,revised)
        # Palette in native previews is descriptive; the production shader/material routing is unchanged.
        be.export_fbx(OUT/(slug+'.fbx'),arm)
        be.studio();be.bpy.context.scene.cycles.samples=16
        be.bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(slug+'.blend')),compress=True)
        be.render(OUT/(slug+'-front.png'),(0,0,1.0),2.65 if kind=='CyberShark' else 2.45,18)
        be.render(OUT/(slug+'-rear.png'),(0,0,1.0),2.65 if kind=='CyberShark' else 2.45,162)
        checks.update({'concept':revised['conceptSource'],'sourceSha256':digest(raw),'runtimeOutput':resource.relative_to(ROOT).as_posix(),'outputSha256':digest(resource.read_bytes()),'authoredParts':[x['name'] for x in p.parts],'bodyReconstruction':'Existing silhouette/rig retained; new concept-specified chest and rear construction added.','UnityAndroid':'not run'})
        REPORT['heroes'][slug]=checks
        print('CONCEPT_RUNTIME_COMPLETE',slug,json.dumps(checks),flush=True)

def source_scene(slug,id):
    import bpy,bmesh
    import blender_export as be
    path=ROOT/f'ArtSource/Heroes/{id}.blend' if id!='Timmy' else ROOT/'ArtSource/Heroes/AndromedaTimmy/AndromedaTimmy.blend'
    raw=original(path)
    if id=='Mira':raw=subprocess.run(['git','show','8fa75d02f0964e9df149ed3c073259fc50248d78:ArtSource/Heroes/Mira.blend'],cwd=ROOT,check=True,capture_output=True).stdout
    with tempfile.TemporaryDirectory() as td:
        src=Path(td)/(id+'.blend');src.write_bytes(raw);bpy.ops.wm.open_mainfile(filepath=str(src))
    bpy.context.preferences.filepaths.save_version=0
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    body=bpy.data.objects.get('DF_Anatomy');assert body is not None
    before=be.rig_signature(arm);actions=be.action_signature()
    # Mirror the already-requested Brakk cleanup in editable source geometry.
    removed=0
    if id=='Rook':
        bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table();deform=bm.verts.layers.deform.active
        groups={g.index:g.name for g in body.vertex_groups};seen=set();kill=[]
        for first in bm.verts:
            if first in seen:continue
            stack=[first];seen.add(first);component=[]
            while stack:
                v=stack.pop();component.append(v)
                for edge in v.link_edges:
                    other=edge.other_vert(v)
                    if other not in seen:seen.add(other);stack.append(other)
            bound={groups.get(i,'?') for v in component for i,w in v[deform].items() if w>.05}
            if bound and all(n.startswith(('UpperArm','Forearm')) for n in bound):kill.extend(component)
        removed=len(kill)
        if kill:bmesh.ops.delete(bm,geom=kill,context='VERTS')
        # Keep eyebrows/beard; only remove dark hair faces at the top of the skull.
        hair=[f for f in bm.faces if 'hair' in body.data.materials[f.material_index].name.lower() and min(v.co.z for v in f.verts)>1.72]
        if hair:bmesh.ops.delete(bm,geom=hair,context='FACES')
        bm.to_mesh(body.data);bm.free();body.data.update()
    names=[b.name for b in arm.data.bones];chest=names.index('Chest');head=names.index('Head')
    vertices=np.asarray([[v.co.x,v.co.z,-v.co.y] for v in body.data.vertices]);faces=[]
    for poly in body.data.polygons:
        for i in range(1,len(poly.vertices)-1):faces.append([poly.vertices[0],poly.vertices[i],poly.vertices[i+1]])
    temp={'vertices':vertices.ravel().tolist(),'submeshes':[{'triangles':np.asarray(faces).ravel().tolist()}]}
    def front(x,y,back=False):return front_surface(temp,x,y,back)
    y=float(arm.data.bones['Chest'].head_local.z)+.20
    p=Parts();materials=[]
    colors=[(.03,.04,.05),(.39,.24,.085),(.045,.73,.50),(.57,.07,.91),(1,.45,.025),(1,.018,.008),(.015,.76,.91)]
    for i,c in enumerate(colors):materials.append(be.material('Concept '+slug+' '+str(i),c,.55 if i<2 else .12,.42,i>=2))
    if id=='Veyr':
        for x,height in [(-.025,.035),(0,.065),(.025,.035)]:
            points=[(x,float(z),front(x,float(z))) for z in np.linspace(y-height,y+.03,12)]
            p.tube('three-prong violet chest sensor',points,.003,3,chest,10)
    elif id=='Mira':
        z=front(0,y)
        p.plate('emerald leaf restoration inlay',[(0,y+.035),(.020,y),(0,y-.035),(-.020,y)],z,.003,2,chest)
        for s in [-1,1]:
            points=[(s*.004,y+.030,z+.005),(s*.013,y,z+.005),(0,y-.028,z+.005)]
            p.tube('gold leaf chest bezel',points,.0017,1,chest,10)
    elif id=='Rook':
        # Do not add anything to his cleared arms; new details stay on Chest.
        for j in range(3):
            level=y+.045-j*.043;z=front(0,level)
            chevron(p,'staggered orange torso plate',0,level,z,.062,.018,4,chest)
        for j in range(5):
            level=y+.07-j*.045;z=front(0,level,True)
            p.plate('red rear spinal reactor cell',[(-.022,level+.013),(.022,level+.013),(.022,level-.013),(-.022,level-.013)],z,-.003,5,chest)
    else:
        # Existing Timmy suit/control module remains intact; add the three new level indicators.
        y=float(arm.data.bones['Chest'].head_local.z)+.13
        for x in [-.024,0,.024]:
            z=front(x,y)
            p.plate('cyan chest level indicator',[(x-.006,y+.006),(x+.006,y+.006),(x+.006,y-.006),(x-.006,y-.006)],z,.002,6,chest)
    obj=be.mesh_object('Concept construction '+slug,[be.xyz(v) for v in p.v],p.f,p.m,materials,[be.xyz(n) for n in p.normals()])
    be.skin(obj,arm,names,p.b)
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
    assert be.rig_signature(arm)==before and be.action_signature()==actions
    body['concept_revision']=REVISION;body['concept_source']=f'ArtSource/Heroes/Concepts/2026-10-12/{slug}.png'
    resource=ROOT/f'Assets/Alterverse/Resources/AuthoredHeroes/{id}.fbx'
    be.export_fbx(resource,arm,True)
    be.export_fbx(OUT/(slug+'.fbx'),arm,True)
    # Editable shipping source is the reconstructed source scene, not an AI image.
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    for o in bpy.data.objects:
        if o.type in ['LIGHT','CAMERA']:o.hide_render=True
    arm.data.pose_position='REST';be.studio();bpy.context.scene.cycles.samples=16
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(slug+'.blend')),compress=True)
    scale=1.8 if id=='Timmy' else 2.4;target=(0,0,.70 if id=='Timmy' else 1.0)
    be.render(OUT/(slug+'-front.png'),target,scale,18)
    be.render(OUT/(slug+'-rear.png'),target,scale,162)
    REPORT['heroes'][slug]={'concept':body['concept_source'],'sourceSha256':digest(raw),'runtimeOutput':resource.relative_to(ROOT).as_posix(),'outputSha256':digest(resource.read_bytes()),'rigExact':True,'animationActionDigestsExact':actions,'newVertices':len(p.v),'newTriangles':len(p.f),'newParts':[x['name'] for x in p.parts],'removedBrakkArmAttachmentVertices':removed,'bodyReconstruction':'Existing licensed/project anatomy retained; concept-specified geometry built and source/FBX re-exported.','UnityAndroid':'not run'}
    obj_source(OUT/(slug+'-authored-details.obj'),p,[f'Concept_{i}' for i in range(len(colors))],colors)
    print('CONCEPT_SOURCE_COMPLETE',slug,flush=True)

def verify_fbx():
    import bpy
    for slug,entry in REPORT['heroes'].items():
        path=OUT/(slug+'.fbx');bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
        arms=[o for o in bpy.data.objects if o.type=='ARMATURE'];meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(arms)==1 and meshes
        body=next(o for o in meshes if o.name=='DF_Anatomy');assert all(v.groups for v in body.data.vertices)
        assert {'Hips','Chest','Head','HandL','HandR'}<={b.name for b in arms[0].data.bones}
        for o in meshes:
            assert all(np.isfinite(tuple(v.co)).all() for v in o.data.vertices)
        entry['fbxRoundTrip']={'bones':len(arms[0].data.bones),'bodyVertices':len(body.data.vertices),'bodyFaces':len(body.data.polygons),'skinningImported':True}
        entry['modelSha256']=digest(path.read_bytes());entry['sourceBlendSha256']=digest((OUT/(slug+'.blend')).read_bytes())
        entry['conceptSha256']=digest((ROOT/entry['concept']).read_bytes())
        print('CONCEPT_FBX_VERIFIED',slug,flush=True)

REPORT={'revision':REVISION,'inputCommit':BASE,'previousPartRevision':'8fa75d02f0964e9df149ed3c073259fc50248d78','heroes':{},'scope':'Geometry authoring and native FBX import checks; not Unity/Android acceptance'}
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--runtime-only',action='store_true');args=parser.parse_args()
    OUT.mkdir(parents=True,exist_ok=True)
    build_runtime()
    if not args.runtime_only:
        for slug,id in [('Zhaev-Null','Veyr'),('Tahla-Venn','Mira'),('Brakk-Orun','Rook'),('Andromeda-Timmy','Timmy')]:source_scene(slug,id)
        verify_fbx();assert len(REPORT['heroes'])==9
    (OUT/'validation-report.json').write_text(json.dumps(REPORT,indent=2)+'\n')
    print('NINE_CONCEPT_MODELS_COMPLETE',json.dumps(REPORT),flush=True)
if __name__=='__main__':main()
