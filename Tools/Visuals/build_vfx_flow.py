"""Bake a small, periodic two-band flow texture with Node Wrangler and TexTools.
Run with Blender 4.5 --background --python Tools/Visuals/build_vfx_flow.py.
This supplies colour variation, never effect coverage, alpha or gameplay bounds.
"""
from pathlib import Path
import json, tempfile, uuid, hashlib
import addon_utils, bpy, numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Alterverse/Resources/CombatVfx'
SIZE=128
for addon in ('node_wrangler','textools'):
    assert addon_utils.check(addon)[1], 'Enable '+addon
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_plane_add(size=1)
obj=bpy.context.object;obj.name='Flow_low'
mat=bpy.data.materials.new('DF_VfxFlowBake');mat.use_nodes=True;obj.data.materials.append(mat)
bpy.context.scene.render.engine='CYCLES'
bpy.context.scene.texToolsSettings.size=(SIZE,SIZE)
bpy.context.scene.texToolsSettings.bake_samples=8
bpy.context.preferences.addons['textools'].preferences.bake_color_space_def='PBR'
bpy.context.preferences.addons['textools'].preferences.bake_device='CPU'
area=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D')
from textools import settings,utilities_bake
v,u=np.mgrid[:SIZE,:SIZE]/SIZE

def field(seed):
    rng=np.random.default_rng(seed);result=np.zeros_like(u)
    for frequency,amplitude in ((2,.18),(4,.11),(8,.065),(16,.035)):
        for _ in range(4):
            x,y=rng.integers(-frequency,frequency+1,2)
            result+=amplitude/4*np.sin(2*np.pi*(u*x+v*y)+rng.uniform(0,2*np.pi))
    return np.clip(.5+result,.04,.96)

channels=[]
with tempfile.TemporaryDirectory() as folder:
    for seed in (311,719):
        source=bpy.data.images.new('FlowSource',SIZE,SIZE,float_buffer=True)
        values=field(seed)
        source.colorspace_settings.name='Non-Color'
        source.pixels.foreach_set(np.dstack((values,values,values,np.ones_like(values))).astype(np.float32).ravel())
        source.filepath_raw=str(Path(folder)/'Flow_roughness.exr');source.file_format='OPEN_EXR';source.save()
        nodes=mat.node_tree.nodes;bsdf=nodes.get('Principled BSDF');nodes.active=bsdf
        for n in nodes:n.select=n==bsdf
        area.type='NODE_EDITOR';area.ui_type='ShaderNodeTree';area.spaces.active.shader_type='OBJECT'
        with bpy.context.temp_override(area=area,region=next(r for r in area.regions if r.type=='WINDOW')):
            assert bpy.ops.node.nw_add_textures_for_principled(directory=folder+'/',files=[{'name':'Flow_roughness.exr'}],relative_path=False)=={'FINISHED'}
        # Re-import the rewritten EXR for the second bake, rather than reuse Blender's image cache.
        for n in nodes:
            if n.type=='TEX_IMAGE' and n.image:n.image.reload()
        settings.sets=utilities_bake.get_bake_sets()
        area.type='IMAGE_EDITOR';area.ui_type='UV';bpy.context.scene.TT_bake_mode='roughness.bip'
        with bpy.context.temp_override(area=area,region=next(r for r in area.regions if r.type=='WINDOW')):
            assert bpy.ops.uv.textools_bake()=={'FINISHED'}
        baked=bpy.data.images.get('flow_roughness')
        data=np.asarray(baked.pixels[:],dtype=np.float32).reshape(SIZE,SIZE,4)[...,0]
        assert np.isfinite(data).all() and data.std()>.025
        channels.append(data.copy())
        for n in list(nodes):
            if n.type=='TEX_IMAGE':nodes.remove(n)
        bpy.data.images.remove(source)
image=bpy.data.images.new('FlowNoise',SIZE,SIZE,alpha=True)
image.colorspace_settings.name='Non-Color'
image.pixels.foreach_set(np.dstack((*channels,np.full_like(u,.5),np.ones_like(u))).astype(np.float32).ravel())
image.filepath_raw=str(OUT/'FlowNoise.png');image.file_format='PNG';image.save()
guid=uuid.uuid5(uuid.NAMESPACE_URL,'dimensions-front/vfx/FlowNoise').hex
meta=(ROOT/'Assets/Alterverse/Resources/MaterialFinishes/Metal.png.meta').read_text()
import re
meta=re.sub(r'guid: \w+', 'guid: '+guid,meta).replace('maxTextureSize: 256','maxTextureSize: 128')
meta=re.sub(r'  userData:.*','  userData: TexTools baked periodic flow RG. Linear data. Alpha never controls effect opacity.',meta)
(OUT/'FlowNoise.png.meta').write_text(meta)
report={'size':[SIZE,SIZE],'bakes':2,'addons':['Node Wrangler 3.55','TexTools 1.6.2'],
        'sha256':hashlib.sha256((OUT/'FlowNoise.png').read_bytes()).hexdigest(),
        'channelStd':[float(c.std()) for c in channels],
        'scope':'Offline colour-detail texture; not a Unity/Android render or performance test.'}
path=ROOT/'ArtSource/MaterialFinishes/vfx-flow-report.json';path.write_text(json.dumps(report,indent=2)+'\n')
print('VFX_FLOW_BAKED',json.dumps(report))
