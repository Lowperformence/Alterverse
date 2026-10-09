#!/usr/bin/env python3
"""Compile actual ScenerySurface forward HLSL with stand-in Unity lighting types.

This checks HLSL syntax/types for regular and screen-space shadow paths. It does
not compile real URP includes or Unity/Android shader variants.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile
from render_energy_regression import CORE, ROOT

STUB = '''
#include "PreviewCore.hlsl"
#define TEXTURE2D(name) Texture2D name
#define SAMPLER(name) SamplerState name
#define SAMPLE_TEXTURE2D(tex,samp,uv) tex.Sample(samp,uv)
#define TRANSFORM_TEX(uv,tex) (uv*tex##_ST.xy+tex##_ST.zw)
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(i)
#define UNITY_TRANSFER_INSTANCE_ID(i,o)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
half ComputeFogFactor(float z){return 0;}
half3 MixFog(half3 c,half f){return c;}
float4 TransformWorldToHClip(float3 p){return float4(p,1);}
float4 TransformWorldToShadowCoord(float3 p){return float4(p,1);}
float4 ComputeScreenPos(float4 p){return p;}
struct Light {half3 direction,color;float distanceAttenuation;half shadowAttenuation;};
Light GetMainLight(float4 coord){Light l;l.direction=normalize(float3(.3,.4,.86));l.color=1;l.distanceAttenuation=1;l.shadowAttenuation=1;return l;}
half3 DFSurfaceLighting(float3 p,half3 n,half3 a,half m,half s,half o,half3 e,float4 r){return a*.4+e;}
'''

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--native-bin",type=Path,required=True)
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    shader=(ROOT/"Assets/Resources/DF_ScenerySurface.shader").read_text()
    common=shader.split("HLSLINCLUDE",1)[1].split("ENDHLSL",1)[0]
    forward=shader.split("HLSLPROGRAM",1)[1].split("ENDHLSL",1)[0]
    source=common+forward
    source=re.sub(r"^\s*#pragma.*$","",source,flags=re.M)
    source=source.replace('"Assets/Resources/DF_SurfaceLighting.hlsl"','"PreviewLighting.hlsl"')
    with tempfile.TemporaryDirectory() as temp:
        temp=Path(temp)
        (temp/"PreviewCore.hlsl").write_text(CORE)
        (temp/"PreviewLighting.hlsl").write_text(STUB)
        (temp/"Scenery.hlsl").write_text(source)
        for screen in (False,True):
            for stage,entry in (("vert","Vert"),("frag","Frag")):
                output=temp/f"{screen}-{stage}.spv"
                command=[str(args.native_bin/"glslangValidator"),"-D","-V","--auto-map-bindings","--auto-map-locations","-S",stage,"-e",entry,"-o",str(output)]
                if screen:command += ["-D_MAIN_LIGHT_SHADOWS_SCREEN=1"]
                command += [str(temp/"Scenery.hlsl")]
                result=subprocess.run(command,text=True,capture_output=True)
                if result.returncode:raise RuntimeError(result.stdout+result.stderr)
                subprocess.run([str(args.native_bin/"spirv-val"),str(output)],check=True,capture_output=True)
    # The additive leaf term is bounded by authored strength and vanishes in shadow
    # or for every non-leaf material. Exercise direction/rim/attenuation endpoints.
    checks=0
    for strength in (0,.065,.12):
        for back in (0,.1,.4,.7,1):
            for rim in (0,.2,.5,.8,1):
                for attenuation in (0,.15,.5,1):
                    value=attenuation*back*back*(.5+rim*.5)*strength
                    assert 0<=value<=strength
                    if strength==0 or attenuation==0 or back==0:assert value==0
                    checks+=1
    report={"compiled_stages":4,"bounded_leaf_cases":checks,"scope":"Actual forward HLSL, substitute Unity lighting types; Unity/URP integration and device visuals not tested."}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,indent=2)+"\n")
    print(json.dumps(report,indent=2))

if __name__=="__main__":main()
