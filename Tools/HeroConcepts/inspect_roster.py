"""Read existing hero contracts and collect the declared vendor anatomy notice."""
from pathlib import Path
import gzip,hashlib,io,json,urllib.request,zipfile
import bpy,bmesh

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Heroes/Concepts';OUT.mkdir(parents=True,exist_ok=True)
report={'sourceRevision':'a605c0635eaccd57dc48e0e60a6a344170153a1e','runtime':{},'native':{}}
for kind in ['CyberRanger','UmbralMage','CyberShark','SiegeTank','ChronoTraveler']:
    path=ROOT/'Assets/Alterverse/Resources'/kind/'Model.bytes'
    raw=path.read_bytes();d=json.loads(gzip.decompress(raw));v=d['vertices']
    entry={'sha256':hashlib.sha256(raw).hexdigest(),'keys':list(d),'vertices':len(v)//3,
           'materials':len(d['submeshes']),'bones':dict(zip(d['boneNames'],d['anchors'])),
           'triangles':sum(len(s['triangles'])//3 for s in d['submeshes'])}
    report['runtime'][kind]=entry
    print('RUNTIME_CONTRACT',kind,json.dumps(entry),flush=True)
for hero,path in [('Veyr','ArtSource/Heroes/Veyr.blend'),('Mira','ArtSource/Heroes/Mira.blend'),
                  ('Rook','ArtSource/Heroes/Rook.blend'),('Timmy','ArtSource/Heroes/AndromedaTimmy/AndromedaTimmy.blend'),
                  ('Nyra','ArtSource/Heroes/Nyra.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/path));body=bpy.data.objects['DF_Anatomy']
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    groups={g.index:g.name for g in body.vertex_groups}
    entry={'source':path,'sha256':hashlib.sha256((ROOT/path).read_bytes()).hexdigest(),
           'bones':{b.name:{'head':list(b.head_local),'tail':list(b.tail_local)} for b in arm.data.bones},
           'materials':[m.name for m in body.data.materials],'actions':[a.name for a in bpy.data.actions],
           'objects':[o.name for o in bpy.data.objects if o.name.startswith('DF_')],'headComponents':[]}
    bm=bmesh.new();bm.from_mesh(body.data);deform=bm.verts.layers.deform.active;seen=set()
    for first in bm.verts:
        if first in seen:continue
        stack=[first];seen.add(first);component=[]
        while stack:
            v=stack.pop();component.append(v)
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other not in seen:seen.add(other);stack.append(other)
        weighted={groups.get(i,'?') for v in component for i,w in v[deform].items() if w>.5}
        if weighted=={'Head'}:
            entry['headComponents'].append({'vertices':len(component),
                'lo':[min(v.co[k] for v in component) for k in range(3)],
                'hi':[max(v.co[k] for v in component) for k in range(3)],
                'materials':sorted({body.data.materials[f.material_index].name for v in component for f in v.link_faces})})
    bm.free()
    if hero=='Nyra':
        for name in ['DF_Muzzle','DF_HandleContactR','DF_SupportGrip']:
            obj=bpy.data.objects.get(name)
            if obj:entry[name]=list(obj.matrix_world.translation)
    report['native'][hero]=entry;print('NATIVE_CONTRACT',hero,json.dumps(entry),flush=True)
(OUT/'input-contracts.json').write_text(json.dumps(report,indent=2)+'\n')
licenses=OUT/'Licenses';licenses.mkdir(exist_ok=True)
vendor={'declaredSource':'Blender Human Base Meshes 1.4.1','declarationFile':'Tools/HeroRebuild/README.md',
        'status':'source notice retrieval pending','attempts':[]}
urls=[
 'https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip',
 'https://mirror.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip']
for url in urls:
    try:
        with urllib.request.urlopen(url,timeout=45) as response:raw=response.read()
        bundle=zipfile.ZipFile(io.BytesIO(raw));notices=[n for n in bundle.namelist() if n.lower().endswith(('.txt','.md')) and any(s in n.lower() for s in ['readme','license','licence','copying'])]
        vendor.update({'status':'vendor notice downloaded','url':url,'bundleSha256':hashlib.sha256(raw).hexdigest(),'noticeEntries':notices})
        for i,name in enumerate(notices):
            contents=bundle.read(name);(licenses/f'Blender-Human-Base-Meshes-{i+1}-{Path(name).name}').write_bytes(contents)
            print('VENDOR_LICENSE_NOTICE',name,contents.decode(errors='replace')[:10000],flush=True)
        if not notices:vendor['status']='bundle fetched; no separate notice located'
        break
    except Exception as error:vendor['attempts'].append({'url':url,'error':str(error)})
(licenses/'blender-base-source.json').write_text(json.dumps(vendor,indent=2)+'\n')
print('VENDOR_SOURCE',json.dumps(vendor),flush=True)
