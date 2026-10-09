#!/usr/bin/env python3
"""Compile actual map forward HLSL and render numerical material fixtures outside Unity.

Uses actual packed textures, material parameters and shader vertex/fragment code.
Core transforms, main light and URP PBR are controlled substitutes. This is not an
in-game screenshot, Unity import/build, Android GPU timing or reflection-probe test.
Requires NumPy, Pillow, ModernGL/EGL, glslangValidator, spirv-val, spirv-cross.
"""
import argparse, json, math, re, struct, subprocess, tempfile
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
import moderngl
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Visuals'))
from render_energy_regression import CORE,ROOT
from compile_scenery_regression import STUB

# A controlled roughness-sensitive lighting fixture; never represented as real URP.
LIGHTING=STUB.replace('half3 DFSurfaceLighting(float3 p,half3 n,half3 a,half m,half s,half o,half3 e,float4 r){return a*.4+e;}',r'''
cbuffer PreviewLighting {float4 _Time;float4 _KeyColor;};
struct VertexPositionInputs {float3 positionWS;float4 positionCS;};
VertexPositionInputs GetVertexPositionInputs(float3 p){VertexPositionInputs v;v.positionWS=TransformObjectToWorld(p);v.positionCS=TransformObjectToHClip(p);return v;}
half3 DFSurfaceLighting(float3 p,half3 n,half3 a,half m,half s,half o,half3 e,float4 r)
{
    float3 v=GetWorldSpaceNormalizeViewDir(p);
    float3 l=normalize(float3(-.35,.8,.48));
    float diffuse=saturate(dot(n,l));
    float3 h=SafeNormalize(v+l);
    float variance=min(.22,length(fwidth(n))*.20);
    s=saturate(s-variance);
    float exponent=exp2(3+s*8);
    float3 f0=lerp(float3(.04,.04,.04),a,m);
    float3 fresnel=f0+(1-f0)*pow(1-saturate(dot(n,v)),5);
    float3 spec=pow(saturate(dot(n,h)),exponent)*(exponent+2)*.04*f0*_KeyColor.rgb;
    float3 sky=lerp(float3(.13,.20,.27),float3(.50,.61,.72),saturate(n.y));
    return (a*(.24+diffuse*.76*_KeyColor.rgb)*(1-m)+sky*fresnel*s+spec)*o+e;
}
''').replace('float4 TransformWorldToHClip(float3 p){return float4(p,1);}', 'float4 TransformWorldToHClip(float3 p){return TransformObjectToHClip(mul(_PreviewWorldToObject,float4(p,1)).xyz);}')

THEMES=['Moon','Mars','GreenPlanet','Noxara','Kryon','Earth','Pyraxis','Neptune','Dark00','DiamondCore','GJ504b']
TINTS=[(.19,.21,.24),(.43,.19,.10),(.19,.29,.12),(.08,.12,.30),(.50,.72,.80),(.27,.28,.13),(.22,.11,.07),(.13,.30,.38),(.09,.09,.12),(.38,.46,.57),(.46,.15,.32)]
DEFAULTS={'_BaseMap_ST':(1,1,0,0),'_BaseColor':(.3,.3,.3,1),'_EmissionColor':(0,0,0,0),
          '_DetailScale':.27,'_MacroScale':.047,'_MacroStrength':.52,'_BumpScale':0,
          '_SurfaceMode':0,'_SandStrength':.27,'_RockStrength':.34,'_Metallic':.02,'_Smoothness':.12,
          '_RoughnessBias':.08,'_CavityStrength':.32,'_DetailContrast':.27,'_FineScale':0,'_FineRelief':0,'_FineContrast':0,
          '_Relief':.28,'_ValueContrast':.25,'_RoughnessWeight':.35,'_Sheen':0,'_Transmission':0,
          '_ShallowColor':(.07,.34,.38,1),'_WaveStrength':.14,'_ShoreStrength':.065}

def compile_shader(ctx,path,native,temp,tag,screen=False):
    shader=path.read_text();source=(shader.split('HLSLINCLUDE',1)[1].split('ENDHLSL',1)[0] if 'HLSLINCLUDE' in shader else '')+shader.split('HLSLPROGRAM',1)[1].split('ENDHLSL',1)[0]
    source=re.sub(r'^\s*#pragma.*$','',source,flags=re.M)
    source=source.replace('"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"','"PreviewCore.hlsl"')
    source=source.replace('"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"','"PreviewLighting.hlsl"')
    source=source.replace('"Assets/Resources/DF_SurfaceLighting.hlsl"','"PreviewLighting.hlsl"').replace('"Assets/Resources/DF_SurfaceMicro.hlsl"','"SurfaceMicro.hlsl"')
    if 'VertexPositionInputs' in source and 'PreviewLighting.hlsl' not in source:source='#include \"PreviewLighting.hlsl\"\n'+source
    hlsl=temp/(tag+'.hlsl');hlsl.write_text(source)
    stages=[];members=None
    for stage,entry in [('vert','Vert'),('frag','Frag')]:
        spv=temp/(tag+stage+'.spv')
        cmd=[str(native/'glslangValidator'),'-D','-V','--hlsl-iomap','--auto-map-locations','--auto-map-bindings','-S',stage,'-e',entry,'-o',str(spv)]
        if screen:cmd+=['-D_MAIN_LIGHT_SHADOWS_SCREEN=1']
        result=subprocess.run(cmd+[str(hlsl)],text=True,capture_output=True)
        if result.returncode:raise RuntimeError(result.stdout+result.stderr)
        subprocess.run([str(native/'spirv-val'),str(spv)],check=True,capture_output=True)
        reflection=json.loads(subprocess.check_output([str(native/'spirv-cross'),str(spv),'--reflect'],text=True))
        block=next((x for x in reflection.get('ubos',[]) if x['name']=='UnityPerMaterial'),None)
        if block:members=(block['block_size'],reflection['types'][block['type']]['members'])
        glsl=subprocess.check_output([str(native/'spirv-cross'),str(spv),'--version','330'],text=True)
        for name,binding in [('UnityPerMaterial',0),('PreviewCamera',1),('PreviewLighting',2)]:
            glsl=re.sub(r'layout\(binding = \d+, std140\)(\s*uniform '+name+r')',r'layout(binding = '+str(binding)+r', std140)\1',glsl)
        if stage=='vert':outputs=re.findall(r'(?:layout\(location = (\d+)\) )?out (\w+) (\w+);',glsl)
        else:
            inputs=re.findall(r'(?:layout\(location = (\d+)\) )?in (\w+) (\w+);',glsl)
            output_names={name.rsplit('_',1)[-1]:(kind,name) for _,kind,name in outputs}
            for _,kind,name in inputs:
                field=name.rsplit('_',1)[-1];assert field in output_names and output_names[field][0]==kind,(tag,field)
                glsl=re.sub(r'\b'+re.escape(name)+r'\b',output_names[field][1],glsl)
        stages.append(glsl)
    program=ctx.program(vertex_shader=stages[0],fragment_shader=stages[1])
    for name,binding in [('UnityPerMaterial',0),('PreviewCamera',1),('PreviewLighting',2)]:
        if name in program:program[name].binding=binding
    return program,members

def geometry(kind):
    data=[]
    def quad(points,n):
        for k in (0,1,2,0,2,3):
            p=points[k];data.append((*p,*n,(p[0]+.5),(p[2]+.5),1,1,1,.3))
    if kind=='pool':
        for i in range(64):
            a=i*math.tau/64;b=(i+1)*math.tau/64
            for p in ((0,0,0),(math.cos(a)*.5,0,math.sin(a)*.5),(math.cos(b)*.5,0,math.sin(b)*.5)):
                data.append((*p,0,1,0,p[0]+.5,p[2]+.5,1,1,1,1))
    elif kind=='cube':
        for n,points in [((0,1,0),[(-.5,.35,-.5),(-.5,.35,.5),(.5,.35,.5),(.5,.35,-.5)]),
                         ((0,0,1),[(-.5,-.3,.5),(.5,-.3,.5),(.5,.35,.5),(-.5,.35,.5)]),
                         ((1,0,0),[(.5,-.3,-.5),(.5,.35,-.5),(.5,.35,.5),(.5,-.3,.5)])]:quad(points,n)
    else:quad([(-.5,0,-.5),(-.5,0,.5),(.5,0,.5),(.5,0,-.5)],(0,1,0))
    return np.asarray(data,dtype='f4')

def camera(scale,angle,span):
    # Compare nonuniform/scaled/rotated fixtures in the same metre-based world.
    c,s=math.cos(angle),math.sin(angle)
    model=np.array([[c,0,s,12],[0,1,0,0],[-s,0,c,18],[0,0,0,1]],dtype='f4')@np.diag([*scale,1])
    eye=np.array([12+span*.60,span*.88,18+span*.88]);target=np.array([12,0,18])
    f=(target-eye);f/=np.linalg.norm(f);right=np.cross(f,[0,1,0]);right/=np.linalg.norm(right);up=np.cross(right,f)
    view=np.eye(4);view[:3,:3]=[right,up,-f];view[:3,3]=-view[:3,:3]@eye
    ortho=np.diag([2/span,2/span,-2/(span*6),1]);clip=ortho@view@model
    return b''.join(np.asarray(m.T,dtype='f4').tobytes() for m in (clip,model,np.linalg.inv(model)))+struct.pack('4f',*eye,0)

def material_buffer(members,settings):
    size,fields=members;data=bytearray((size+15)//16*16)
    for field in fields:
        value=settings.get(field['name'],DEFAULTS.get(field['name'],0))
        if not isinstance(value,(list,tuple)):value=[value]
        struct.pack_into(str(len(value))+'f',data,field['offset'],*value)
    return bytes(data)

def crystal_lighting():
    return LIGHTING+"""
Light GetMainLight(){return GetMainLight(float4(0,0,0,1));}
float2 GetNormalizedScreenSpaceUV(float4 p){return p.xy/256;}
half3 GlossyEnvironmentReflection(half3 r,float3 p,half rough,half o,float2 uv){return lerp(half3(.72,.84,.96),half3(.22,.32,.45),rough)*o;}
"""

class Renderer:
    def __init__(self,ctx):self.ctx=ctx;self.textures={}
    def texture(self,path):
        key=str(path)
        if key in self.textures:return self.textures[key]
        if path=='bump':a=np.array([[[128,128,255,255]]],dtype='u1')
        elif path=='white':a=np.full((1,1,4),255,dtype='u1')
        elif path=='gray':a=np.full((1,1,4),128,dtype='u1')
        else:a=np.asarray(Image.open(path).convert('RGBA'))[::-1].copy()
        tex=self.ctx.texture((a.shape[1],a.shape[0]),4,a.tobytes());tex.build_mipmaps();tex.filter=(moderngl.LINEAR_MIPMAP_LINEAR,moderngl.LINEAR);tex.repeat_x=True;tex.repeat_y=True
        self.textures[key]=tex;return tex
    def render(self,compiled,settings,textures,kind='plane',scale=(4,1,4),angle=0,span=6,size=256,time=0,light=1,opaque=True):
        ctx=self.ctx;program,members=compiled
        material=ctx.buffer(material_buffer(members,settings));material.bind_to_uniform_block(0)
        matrices=ctx.buffer(camera(scale,angle,span));matrices.bind_to_uniform_block(1)
        lighting=ctx.buffer(struct.pack('8f',time/20,time,time*2,time*3,light,light,light,1));lighting.bind_to_uniform_block(2)
        buffer=ctx.buffer(geometry(kind).tobytes());names=[];formats=[]
        for suffix,width in [('positionOS',3),('normalOS',3),('uv',2),('color',4)]:
            name=next((n for n in program if n.endswith(suffix)),None)
            if name:names.append(name);formats.append(str(width)+'f')
            else:formats.append(str(width*4)+'x')
        vao=ctx.vertex_array(program,[(buffer,' '.join(formats),*names)])
        unit=0
        for name in program:
            if name.startswith('SPIRV_Cross_Combined'):
                key=next((k for k in textures if name.startswith('SPIRV_Cross_Combined'+k+'sampler_')),None)
                self.texture(textures.get(key,'gray')).use(unit);program[name].value=unit;unit+=1
        target=ctx.texture((size,size),4,dtype='f4');depth=ctx.depth_renderbuffer((size,size));fb=ctx.framebuffer([target],depth);fb.use();fb.clear(.025,.033,.042,0,depth=1);ctx.enable(moderngl.DEPTH_TEST);ctx.disable(moderngl.CULL_FACE);ctx.disable(moderngl.BLEND)
        vao.render();pixels=np.frombuffer(fb.read(components=4,dtype='f4'),dtype='f4').reshape(size,size,4)[::-1].copy()
        assert np.count_nonzero(pixels[:,:,3])>=10,(kind,size,"empty fixture")
        assert np.isfinite(pixels).all() and pixels[:,:,:3].min()>=0
        if opaque:assert np.all((pixels[:,:,3]==0)|(pixels[:,:,3]==1))
        else:assert pixels[:,:,3].min()>=0 and pixels[:,:,3].max()<=1
        for obj in (vao,buffer,material,matrices,lighting,fb,depth,target):obj.release()
        return pixels

def picture(pixels):
    rgb=np.clip(pixels[:,:,:3],0,1)**(1/2.2)
    return Image.fromarray(np.rint(rgb*255).astype('u1'))

def main():
    p=argparse.ArgumentParser();p.add_argument('--native-bin',type=Path,required=True);p.add_argument('--baseline',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
    ctx=moderngl.create_standalone_context(backend='egl');renderer=Renderer(ctx);count=0;disabled=[];cards=[]
    # Profiles are emitted by the separate portable regression from real C# production mappings.
    profiles=json.loads((a.output/'material-profiles.json').read_text())
    with tempfile.TemporaryDirectory() as folder:
        temp=Path(folder);(temp/'PreviewCore.hlsl').write_text('#ifndef PREVIEW_CORE\n#define PREVIEW_CORE\n'+CORE+'\n#endif\n');(temp/'PreviewLighting.hlsl').write_text(LIGHTING);(temp/'SurfaceMicro.hlsl').write_text((ROOT/'Assets/Resources/DF_SurfaceMicro.hlsl').read_text())
        compiled={};stages=0
        for name in ('DF_PlanetSurface','DF_ScenerySurface','DF_MapWater'):
            for screen in (False,True):
                result=compile_shader(ctx,ROOT/'Assets/Resources'/(name+'.shader'),a.native_bin,temp,name+str(screen),screen);stages+=2
                if not screen:compiled[name]=result
        old={name:compile_shader(ctx,a.baseline/'Assets/Resources'/(name+'.shader'),a.native_bin,temp,'old'+name) for name in ('DF_PlanetSurface','DF_ScenerySurface')};stages+=4
        # Added-detail-off is pixel-identical to both original shaders, including rotated stone sides.
        for name in old:
            for mode,kind,angle in [(0,'plane',0),(1,'plane',.63),(2,'cube',0),(2,'cube',.63),(2,'cube',3.77)]:
                textures={'_SandData':ROOT/'Assets/Alterverse/Resources/PlanetSurfaces/Mars/Ground.png','_StoneData':ROOT/'Assets/Alterverse/Resources/PlanetSurfaces/Mars/Stone.png','_MacroData':ROOT/'Assets/Alterverse/Resources/PlanetSurfaces/Mars/Macro.png','_DetailData':ROOT/'Assets/Alterverse/Resources/PlanetSurfaces/Mars/Stone.png','_BaseMap':'white','_GroundMap':'white','_BumpMap':'bump'}
                settings={'_SurfaceMode':mode}
                before=renderer.render(old[name],settings,textures,kind=kind,angle=angle)
                after=renderer.render(compiled[name],settings,textures,kind=kind,angle=angle)
                delta=float(abs(before-after).max());disabled.append(delta);assert delta<1e-6,(name,mode,delta);count+=2
        for index,theme in enumerate(THEMES):
            profile=profiles['ground'][theme];folder=ROOT/'Assets/Alterverse/Resources/PlanetSurfaces'/theme
            textures={'_BaseMap':'white','_GroundMap':'white','_BumpMap':'bump','_SandData':folder/'Ground.png','_StoneData':folder/'Stone.png','_MacroData':folder/'Macro.png','_FineData':ROOT/'Assets/Alterverse/Resources/SurfaceMicro'/(profile['texture']+'.png') if profile['texture'] else 'gray'}
            settings={'_BaseColor':(*TINTS[index],1),'_FineScale':profile['scale'],'_FineRelief':profile['relief'],'_FineContrast':profile['contrast']}
            for size,span in [(256,6),(96,18)]:
                after=renderer.render(compiled['DF_PlanetSurface'],settings,textures,size=size,span=span);count+=1
                if theme in ('Mars','Earth') and size==256:
                    before=renderer.render(old['DF_PlanetSurface'],settings,textures);count+=1;cards.append((theme+' ground',picture(before),picture(after)))
        # Every scenery profile, including disabled organics/metal/ice and wet stone.
        for finish,profile in profiles['scenery'].items():
            textures={'_BaseMap':'white','_DetailData':ROOT/'Assets/Alterverse/Resources/SurfaceMicro/Concrete.png' if finish=='Concrete' else ROOT/('Assets/Alterverse/Resources/PlanetSurfaces/Earth/Ground.png' if finish=='Soil' else 'Assets/Alterverse/Resources/PlanetSurfaces/Earth/Stone.png'),'_FineData':ROOT/'Assets/Alterverse/Resources/SurfaceMicro'/(profile['texture']+'.png') if profile['texture'] else 'gray'}
            settings={'_BaseColor':(.19,.205,.22,1) if finish=='Concrete' else (.34,.125,.030,1) if finish=='Soil' else (.29,.27,.23,1),'_FineScale':profile['scale'],'_FineRelief':profile['relief'],'_FineContrast':profile['contrast'],'_DetailScale':.65 if finish=='Concrete' else .42,'_Relief':.10 if finish=='Concrete' else .28,'_Smoothness':.18}
            after=renderer.render(compiled['DF_ScenerySurface'],settings,textures,kind='cube',angle=.41);count+=1
            if finish in ('Stone','Concrete','Soil'):
                # Concrete old plinth had plain colour; emulate zero authored detail in the old shader.
                oldtextures=dict(textures)
                oldsettings=dict(settings)
                if finish=='Concrete':oldtextures['_DetailData']='gray';oldsettings['_Relief']=0;oldsettings['_ValueContrast']=0;oldsettings['_RoughnessWeight']=0
                before=renderer.render(old['DF_ScenerySurface'],oldsettings,oldtextures,kind='cube',angle=.41);count+=1;cards.append((finish,picture(before),picture(after)))
        # Four existing ellipse dimensions, rotations, time samples and resolutions; zero direct light included.
        animation=[]
        for index,scale in enumerate([(7.8,.09,4.6),(6.6,.09,4.2),(5.6,.08,3.4),(5.2,.08,3.2)]):
            for size in (256,96):
                samples=[]
                for time,light in [(0,1),(.8,1),(4.3,1),(4.3,0)]:
                    sample=renderer.render(compiled['DF_MapWater'],{'_BaseColor':(.02,.25,.62,1),'_Smoothness':.90},{},kind='pool',scale=scale,angle=index*29*math.pi/180,span=10,size=size,time=time,light=light);samples.append(sample);count+=1
                delta=float(abs(samples[0]-samples[1]).max());assert delta>.001,('water not animated',index,size);animation.append(delta)
                if index==0 and size==256:
                    # Explicit colour swatch control, not a rendering of Unity's old Lit shader.
                    flat=samples[0].copy();mask=flat[:,:,3]>0;flat[mask,:3]=np.array([.02,.25,.62])*.7
                    cards.append(('Earth water (flat-colour control)',picture(flat),picture(samples[0])))
                    cards.append(('Earth water (t=0 / t=0.8)',picture(samples[0]),picture(samples[1])))
        # Restore the floor against its original September transparent shader, not a recreated formula.
        (temp/'PreviewLighting.hlsl').write_text('#ifndef PREVIEW_LIGHTING\n#define PREVIEW_LIGHTING\n'+crystal_lighting()+'\n#endif\n')
        legacy=compile_shader(ctx,ROOT/'Tools/PlanetSurfaces/Fixtures/original-diamond-floor.shader',a.native_bin,temp,'crystalOriginal')
        previous=compile_shader(ctx,a.baseline/'Assets/Resources/DF_DiamondCrystal.shader',a.native_bin,temp,'crystalPrevious')
        restored=compile_shader(ctx,ROOT/'Assets/Resources/DF_DiamondCrystal.shader',a.native_bin,temp,'crystalRestored');stages+=6
        settings={'_BaseColor':(.64,.91,1,.32),'_EmissionColor':(.16*.22,.72*.22,1*.22,1),'_Smoothness':.96,
                  '_FresnelPower':2.4+(3.8-2.4)*.96,'_FresnelStrength':.52+(.82-.52)*(.08+.96*.5),'_CoreAlpha':.32*.34,
                  '_FacetDetail':0,'_SurfaceRelief':0,'_SurfaceScale':.25,'_ProbeReflection':0}
        restored_errors=[];crystal_control_errors=[];transmissions=[]
        for angle in (0,.63,1.49):
            for span in (6,9,18):
                first=renderer.render(legacy,settings,{},angle=angle,span=span,opaque=False)
                clear=renderer.render(restored,settings,{},angle=angle,span=span,opaque=False)
                delta=float(abs(first-clear).max());assert delta<1e-6,('original crystal floor changed',delta);restored_errors.append(delta);count+=2
                transmission=1-float(clear[:,:,3].max());assert transmission>.45,('crystal floor not transparent',transmission);transmissions.append(transmission)
                default=dict(settings,_ProbeReflection=1)
                old_detail=renderer.render(previous,default,{},angle=angle,span=span,opaque=False)
                new_detail=renderer.render(restored,default,{},angle=angle,span=span,opaque=False)
                delta=float(abs(old_detail-new_detail).max());assert delta<1e-6,('other crystal reflection changed',delta);crystal_control_errors.append(delta);count+=2
                if angle==0 and span==6:
                    yy,xx=np.mgrid[:clear.shape[0],:clear.shape[1]];checker=((xx//24+yy//24)%2)[...,None]
                    background=np.where(checker,np.array([.18,.23,.31]),np.array([.035,.055,.09]))
                    def composite(layer):
                        mixed=layer.copy();alpha=layer[:,:,3:4];mixed[:,:,:3]=layer[:,:,:3]*alpha+background*(1-alpha);return picture(mixed)
                    cards.append(('PSR J1719 (original / restored, checker behind)',composite(first),composite(clear)))
        shader=(ROOT/'Assets/Resources/DF_DiamondCrystal.shader').read_text()
        assert 'ZWrite Off' in shader and 'Blend SrcAlpha OneMinusSrcAlpha' in shader and '"RenderType"="Transparent"' in shader.replace(' ','')
        sheet=Image.new('RGB',(560,len(cards)*290+52),'#111820');draw=ImageDraw.Draw(sheet);draw.text((14,10),'MATERIAL FIXTURES OUTSIDE UNITY | BEFORE / AFTER',(220,230,240));draw.text((14,29),'Substitute lighting; actual HLSL and packed surface data',(170,185,200))
        for row,(title,before,after) in enumerate(cards):
            y=52+row*290;draw.text((14,y),title,(225,232,235));sheet.paste(before,(14,y+20));sheet.paste(after,(290,y+20))
        sheet.save(a.output/'material-comparison.png')
    report={'renderCases':count,'compiledForwardStages':stages,'disabledDetailMaximumError':max(disabled),'originalCrystalFloorMaximumError':max(restored_errors),'unchangedOtherCrystalMaximumError':max(crystal_control_errors),'minimumCrystalBackgroundTransmission':min(transmissions),'waterAnimationMinimumDelta':min(animation),'renderer':ctx.info['GL_RENDERER'],'scope':__doc__.strip()}
    (a.output/'shader-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
