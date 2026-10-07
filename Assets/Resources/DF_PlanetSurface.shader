Shader "Alterverse/PlanetSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Existing surface colour", 2D) = "white" {}
        [MainColor] _BaseColor("Existing planet tint", Color) = (1,1,1,1)
        _GroundMap("Existing arena colour field", 2D) = "white" {}
        _BumpMap("RGB macro normal", 2D) = "bump" {}
        _BumpScale("Macro relief", Float) = 0
        _SandData("Sand slope / roughness / value", 2D) = "gray" {}
        _StoneData("Stone slope / roughness / value", 2D) = "gray" {}
        _MacroData("Blender geological value / cavity / roughness / sediment", 2D) = "gray" {}
        _MacroScale("Geological patch frequency", Float) = 0.047
        _MacroStrength("Geological contrast", Float) = 0.52
        _SurfaceMode("Ground 0, crater 1, stone 2", Float) = 0
        _DetailScale("World detail frequency", Float) = 0.23
        _SandStrength("Sand relief", Float) = 0.22
        _RockStrength("Stone relief", Float) = 0.28
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.12
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 200
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/Resources/DF_SurfaceLighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor;
            float _DetailScale, _MacroScale;
            half _MacroStrength;
            half _BumpScale, _SurfaceMode, _SandStrength, _RockStrength, _Metallic, _Smoothness;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_GroundMap); SAMPLER(sampler_GroundMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_SandData); SAMPLER(sampler_SandData);
        TEXTURE2D(_StoneData); SAMPLER(sampler_StoneData);
        TEXTURE2D(_MacroData); SAMPLER(sampler_MacroData);
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
            half edgeBlend : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes i)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_TRANSFER_INSTANCE_ID(i,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);
            o.positionCS=p.positionCS; o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(i.normalOS);
            o.uv=TRANSFORM_TEX(i.uv,_BaseMap);
            o.fog=ComputeFogFactor(p.positionCS.z);
            o.edgeBlend=i.color.a;
            return o;
        }
        half Luma(half3 c) { return dot(c,half3(.2126h,.7152h,.0722h)); }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normalWS);
                float3 p=i.positionWS;
                half3 albedo=_BaseColor.rgb;
                half4 data;
                half3 gradient;
                if(_SurfaceMode>1.5h)
                {
                    // World triplanar detail works on UV-less, rotated and nonuniformly scaled rocks/cliffs.
                    half3 weights=pow(abs(n),4.0h);
                    weights/=max(dot(weights,half3(1,1,1)),.001h);
                    half4 x=SAMPLE_TEXTURE2D(_StoneData,sampler_StoneData,p.zy*_DetailScale);
                    half4 y=SAMPLE_TEXTURE2D(_StoneData,sampler_StoneData,p.xz*_DetailScale);
                    half4 z=SAMPLE_TEXTURE2D(_StoneData,sampler_StoneData,p.xy*_DetailScale);
                    data=x*weights.x+y*weights.y+z*weights.z;
                    gradient=(half3(0,x.g-.5h,x.r-.5h)*weights.x+
                              half3(y.r-.5h,0,y.g-.5h)*weights.y+
                              half3(z.r-.5h,z.g-.5h,0)*weights.z)*2*_RockStrength;
                }
                else
                {
                    float2 uv=p.xz*_DetailScale;
                    uv+=.24*sin(p.zx*.117+sin(p.xz*.071));
                    half4 sand=SAMPLE_TEXTURE2D(_SandData,sampler_SandData,uv);
                    half4 grain=SAMPLE_TEXTURE2D(_SandData,sampler_SandData,uv*3.173+float2(.37,.61));
                    sand.rg=lerp(sand.rg,grain.rg,.18h);
                    sand.b=lerp(sand.b,grain.b,.15h);
                    sand.a=lerp(sand.a,grain.a,.12h);
                    // Incommensurate rotated second layer masks repeats without extra draw calls.
                    float2 stoneUV=float2(uv.x*.8-uv.y*.6,uv.x*.6+uv.y*.8)*.713+float2(.31,.73);
                    half4 rock=SAMPLE_TEXTURE2D(_StoneData,sampler_StoneData,stoneUV);
                    half patch=saturate(.45h+(rock.a-.5h)*1.7h+(1-abs(n.y))*2);
                    data=lerp(sand,rock,patch*.7h);
                    half2 rg=(rock.rg-.5h)*2;
                    rg=half2(.8h*rg.x+.6h*rg.y,-.6h*rg.x+.8h*rg.y);
                    half2 slope=lerp((sand.rg-.5h)*2*_SandStrength,rg*_RockStrength,patch*.7h);
                    // Broad combat court and base approaches remain quiet; no navigation change.
                    half quiet=1-smoothstep(8.0,19.0,length(p.xz));
                    quiet=max(quiet,(1-smoothstep(3.0,8.0,abs(p.z)))*.65h);
                    slope*=lerp(1.0h,.4h,quiet);
                    gradient=half3(slope.x,0,slope.y);
                    if(_SurfaceMode<.5h)
                    {
                        albedo*=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                        half3 macro=SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv).rgb*2-1;
                        gradient+=half3(-macro.x,0,-macro.y)*_BumpScale;
                    }
                    else
                    {
                        // Vertex alpha only on existing crater rims: fully blend into the live colour field at the skirt.
                        half3 ground=SAMPLE_TEXTURE2D(_GroundMap,sampler_GroundMap,(p.xz+float2(48,42))/float2(96,84)).rgb;
                        albedo=lerp(albedo,ground,saturate(i.edgeBlend));
                    }
                    data.a=lerp(data.a,.5h,quiet*.6h);
                }
                // A separate Blender-authored geological layer reads at the gameplay camera distance.
                float2 geologyUV=p.xz*_MacroScale+.07*sin(p.zx*.09);
                half4 geology=SAMPLE_TEXTURE2D(_MacroData,sampler_MacroData,geologyUV);
                half quietGeology=1-smoothstep(7.0,17.0,length(p.xz));
                half geologicalContrast=_MacroStrength*lerp(1.0h,.45h,quietGeology);
                albedo*=1+(geology.r-.5h)*geologicalContrast;
                data.b=lerp(data.b,geology.b,.35h);
                // Surface-gradient perturbation avoids tangent seams and missing UVs on generated meshes.
                n=normalize(n-(gradient-n*dot(n,gradient)));
                albedo*=1+(data.a-.5h)*.34h; // scalar variation cannot rotate the planet hue.
                half cavity=lerp(.93h,1.0h,saturate(data.a*2))*lerp(.94h,1.0h,geology.g);
                half smooth=saturate(lerp(_Smoothness,1-data.b,.58h));
                half3 rgb=DFSurfaceLighting(p,n,albedo,_Metallic,smooth,cavity,_EmissionColor.rgb);
                return half4(MixFog(rgb,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float4 ShadowVert(Attributes i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(i.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-p);
                #else
                    float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
                #if UNITY_REVERSED_Z
                    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 DepthFrag() : SV_Target {return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings i) : SV_Target {return i.positionCS.z;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFNormalVert
            #pragma fragment DFNormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}


