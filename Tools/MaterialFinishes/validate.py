"""Validate material assets, production C# and actual forward HLSL with engine fixtures.
Requires Pillow, numpy, Mono/Roslyn and glslangValidator/spirv-val. This is not a
Unity import, native URP shader compile, Android visual check or GPU benchmark.
"""
import argparse, hashlib, json, os, re, subprocess, tempfile
from pathlib import Path
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
CORE=r'''
#ifndef PREVIEW_UNITY_INCLUDED
#define PREVIEW_UNITY_INCLUDED
#define CBUFFER_START(n) cbuffer n {
#define CBUFFER_END };
#define TEXTURE2D(n) Texture2D n
#define SAMPLER(n) SamplerState n
#define TEXTURE2D_PARAM(t,s) Texture2D t,SamplerState s
#define TEXTURE2D_ARGS(t,s) t,s
#define SAMPLE_TEXTURE2D(t,s,u) t.Sample(s,u)
#define SAMPLE_TEXTURE2D_GRAD(t,s,u,x,y) t.SampleGrad(s,u,x,y)
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(i)
#define UNITY_TRANSFER_INSTANCE_ID(i,o)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
#define TRANSFORM_TEX(uv,t) (uv*t##_ST.xy+t##_ST.zw)
cbuffer Camera {float4x4 unity_ObjectToWorld,unity_WorldToObject,UNITY_MATRIX_V;float4 _Time;};
float3 SafeNormalize(float3 p){return p*rsqrt(max(dot(p,p),1e-8));}
float3 TransformObjectToWorld(float3 p){return mul(unity_ObjectToWorld,float4(p,1)).xyz;}
float3 TransformWorldToObject(float3 p){return mul(unity_WorldToObject,float4(p,1)).xyz;}
float3 TransformObjectToWorldNormal(float3 n,bool normalizeResult=true){float3 v=mul(n,(float3x3)unity_WorldToObject);return normalizeResult?SafeNormalize(v):v;}
float3 TransformObjectToWorldDir(float3 n){return SafeNormalize(mul((float3x3)unity_ObjectToWorld,n));}
float4 TransformObjectToHClip(float3 p){return float4(TransformObjectToWorld(p),1);}
float4 TransformWorldToHClip(float3 p){return float4(p,1);}
float4 TransformWorldToShadowCoord(float3 p){return float4(p,1);}
float3 GetWorldSpaceViewDir(float3 p){return float3(0,2,8)-p;}
float3 GetWorldSpaceNormalizeViewDir(float3 p){return SafeNormalize(GetWorldSpaceViewDir(p));}
float4 ComputeScreenPos(float4 p){return p;}
float2 GetNormalizedScreenSpaceUV(float4 p){return p.xy;}
float GetOddNegativeScale(){return 1;}
half ComputeFogFactor(float z){return 0;}
half3 MixFog(half3 c,half f){return c;}
half3 SampleSH(half3 n){return .3;}
half3 UnpackNormalScale(half4 n,half s){return SafeNormalize(half3((n.xy*2-1)*s,n.z*2-1));}
struct VertexPositionInputs {float3 positionWS;float4 positionCS;};
VertexPositionInputs GetVertexPositionInputs(float3 p){VertexPositionInputs v;v.positionWS=TransformObjectToWorld(p);v.positionCS=TransformObjectToHClip(p);return v;}
float4 GetShadowCoord(VertexPositionInputs v){return v.positionCS;}
struct Light {half3 direction,color;float distanceAttenuation;half shadowAttenuation;};
Light GetMainLight(float4 r){Light l;l.direction=SafeNormalize(float3(.3,.4,.8));l.color=1;l.distanceAttenuation=1;l.shadowAttenuation=1;return l;}
struct InputData {float3 positionWS;half3 normalWS,viewDirectionWS;float4 shadowCoord;half3 bakedGI;float2 normalizedScreenSpaceUV;half4 shadowMask;};
struct SurfaceData {half3 albedo,specular,normalTS,emission;half metallic,smoothness,occlusion,alpha,clearCoatMask,clearCoatSmoothness;};
half4 UniversalFragmentPBR(InputData i,SurfaceData s){return half4(s.albedo*.4+s.emission,s.alpha);}
half3 DFSurfaceLighting(float3 p,half3 n,half3 a,half m,half s,half o,half3 e,float4 r){return a*.4+e;}
#endif
'''

def assets():
    report=json.loads((ROOT/'ArtSource/MaterialFinishes/bake-report.json').read_text())
    out=ROOT/'Assets/Alterverse/Resources/MaterialFinishes'
    results={};memory=0
    for f in ('Metal','Stone','Concrete','Soil','Sand','Bark','Leaf','Ice','Fabric'):
        p=out/(f+'.png');a=np.asarray(Image.open(p).convert('RGBA'))/255
        n=np.asarray(Image.open(out/(f+'_Normal.png')).convert('RGB'))/255*2-1
        assert a.shape==(256,256,4) and np.isfinite(a).all()
        assert hashlib.sha256(p.read_bytes()).hexdigest()==report[f]['sha256']
        assert np.std(n[...,:2])>.0008 and np.max(abs(np.linalg.norm(n,axis=2)-1))<.017
        assert .01<a[...,2].min()<=a[...,2].max()<.99 and a[...,3].std()>.001
        # Periodic tiles may have a normal derivative jump at their bake boundary;
        # reject a large absolute jump, not harmless quantisation in nearly flat data.
        seam=max(np.mean(abs(a[:,0]-a[:,-1])),np.mean(abs(a[0]-a[-1])))
        assert seam<.11,(f,seam)
        results[f]={'seamMean':round(float(seam),4),'normalXYStd':round(float(n[...,:2].std()),4)}
        for suffix in ('','_Normal'):
            meta=(out/(f+suffix+'.png.meta')).read_text()
            for contract in ('sRGBTexture: 0','enableMipMap: 1','isReadable: 0','wrapU: 0','wrapV: 0','flipGreenChannel: 0'):
                assert contract in meta
            assert 'textureType: '+('1' if suffix else '0') in meta
            memory+=sum(max(1,256//2**i)**2*4 for i in range(9))
    p=ROOT/'Assets/Alterverse/Resources/CombatVfx/FlowNoise.png';a=np.asarray(Image.open(p).convert('RGBA'))/255
    assert a.shape==(128,128,4) and min(a[...,:2].std(axis=(0,1)))>.025
    assert max(np.mean(abs(a[:,0]-a[:,-1])),np.mean(abs(a[0]-a[-1])))<.02
    memory+=sum(max(1,128//2**i)**2*4 for i in range(8))
    return {'textures':19,'rgba8MipMiB':round(memory/1024**2,3),'finishes':results}

def shaders(native):
    paths=['Assets/Resources/DF_RuntimeLit.shader','Assets/Resources/DF_RuntimeGlass.shader',
        'Assets/Resources/DF_EnergyTrail.shader','Assets/Alterverse/Resources/HeroArt/HeroSurface.shader',
        'Assets/Alterverse/Resources/HeroArt/HeroUndersuitFabric.shader','Assets/Alterverse/Resources/HeroArt/SkinOrnaments.shader',
        'Assets/Alterverse/Resources/HeroArt/WeaponEnergyOverlay.shader','Assets/Alterverse/Resources/CombatVfx/HeroEnergy.shader']
    count=0
    with tempfile.TemporaryDirectory() as folder:
        folder=Path(folder);(folder/'UnityFixture.hlsl').write_text(CORE)
        for p in paths:
            shader=(ROOT/p).read_text()
            forward=shader.split('HLSLPROGRAM',1)[1].split('ENDHLSL',1)[0]
            entries=[re.search(r'#pragma\s+'+stage+r'\s+(\w+)',forward)[1] for stage in ('vertex','fragment')]
            source=(shader.split('HLSLINCLUDE',1)[1].split('ENDHLSL',1)[0] if 'HLSLINCLUDE' in shader else '')+forward
            source=re.sub(r'^\s*#pragma.*$','',source,flags=re.M)
            source=re.sub(r'#include "(?:Packages/[^\"]+|Assets/Resources/DF_SurfaceLighting.hlsl)"','#include "UnityFixture.hlsl"',source)
            file=folder/(Path(p).stem+'.hlsl');file.write_text(source)
            for screen in (False,True):
                for stage,entry in zip(('vert','frag'),entries):
                    out=folder/'stage.spv'
                    cmd=[str(native/'glslangValidator'),'-D','-V','--auto-map-bindings','--auto-map-locations','-I'+str(ROOT),'-S',stage,'-e',entry,'-o',str(out)]
                    if screen:cmd+=['-D_MAIN_LIGHT_SHADOWS_SCREEN=1']
                    result=subprocess.run(cmd+[str(file)],capture_output=True,text=True)
                    assert result.returncode==0,(p,stage,result.stdout+result.stderr)
                    subprocess.run([str(native/'spirv-val'),str(out)],check=True,capture_output=True)
                    count+=1
    return count

def main():
    p=argparse.ArgumentParser();p.add_argument('--native-bin',type=Path,required=True);p.add_argument('--mono-root',type=Path,required=True);p.add_argument('--roslyn',type=Path,required=True);p.add_argument('--output',type=Path,required=True);args=p.parse_args()
    report=assets();report['compiledForwardStages']=shaders(args.native_bin)
    mono=args.mono_root/'usr/bin/mono-sgen'
    env=dict(os.environ,MONO_PATH=str(args.mono_root/'usr/lib/mono/4.5')+':'+str(args.mono_root/'usr/lib/mono/4.5/Facades'),MONO_CFG_DIR=str(args.mono_root/'etc'))
    with tempfile.TemporaryDirectory() as folder:
        binary=Path(folder)/'Regression.exe'
        sources=[ROOT/'Tools/MaterialFinishes/Regression.cs',ROOT/'Assets/Alterverse/Scripts/Visuals/GameMaterialFinishes.cs',ROOT/'Assets/Alterverse/Scripts/Visuals/VisualEffectResources.cs',ROOT/'Assets/Alterverse/Scripts/Visuals/HeroIdleBreath.cs']
        subprocess.run([str(mono),str(args.roslyn),'-nologo','-noconfig','-langversion:9','-r:'+str(args.mono_root/'usr/lib/mono/4.5/System.Core.dll'),'-out:'+str(binary),*[str(s) for s in sources]],check=True,env=env)
        result=subprocess.run([str(mono),str(binary)],check=True,env=env,capture_output=True,text=True)
        report['csharp']=result.stdout.strip()
    report['scope']='Baked assets, production C# with API fixtures, actual forward HLSL with substitute lighting types. Unity/URP integration, screenshots and Android GPU/performance remain untested.'
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
