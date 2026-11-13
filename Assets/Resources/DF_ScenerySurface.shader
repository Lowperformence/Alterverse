Shader "Alterverse/ScenerySurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Existing colour map",2D)="white" {}
        [MainColor] _BaseColor("Existing scenery colour",Color)=(1,1,1,1)
        _Metallic("Metallic",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=.2
        [HDR] _EmissionColor("Existing emission",Color)=(0,0,0,0)
        _FineData("Material-specific fine grain",2D)="gray" {}
        _FineScale("Fine grain frequency",Float)=0
        _FineRelief("Fine grain relief",Range(0,.2))=0
        _FineContrast("Fine grain value",Range(0,.5))=0
        _DetailData("Linear slope roughness height",2D)="gray" {}
        _DetailScale("World detail frequency",Float)=.5
        _Relief("Micro surface relief",Range(0,1))=.2
        _ValueContrast("Neutral surface contrast",Range(0,1))=.2
        _RoughnessWeight("Texture finish weight",Range(0,1))=.3
        _RoughnessBias("Planet stone roughness adjustment",Range(-.3,.3))=0
        _Sheen("Leaf edge sheen",Range(0,.2))=0
        _Transmission("Leaf backlight",Range(0,.12))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Back ZWrite On
        HLSLINCLUDE
        #include "Assets/Resources/DF_SurfaceLighting.hlsl"
        #include "Assets/Resources/DF_SurfaceMicro.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        TEXTURE2D(_FineData);SAMPLER(sampler_FineData);
        TEXTURE2D(_DetailData);SAMPLER(sampler_DetailData);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor,_EmissionColor;
            float _DetailScale,_FineScale;
            half _FineRelief,_FineContrast;
            half _Metallic,_Smoothness,_Relief,_ValueContrast,_RoughnessWeight,_Sheen;
            half _RoughnessBias;
            half _Transmission;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float2 uv:TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            float2 uv:TEXCOORD2;
            half fog:TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
            output.fog=ComputeFogFactor(output.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n=normalize(input.normalWS);
                half3 weights=pow(abs(n),4);
                weights/=max(dot(weights,half3(1,1,1)),.001h);
                float3 p=input.positionWS*_DetailScale;
                // Both vertical projections keep bark grain aligned with the world-up axis.
                half4 x=SAMPLE_TEXTURE2D(_DetailData,sampler_DetailData,p.zy);
                half4 y=SAMPLE_TEXTURE2D(_DetailData,sampler_DetailData,p.xz);
                half4 z=SAMPLE_TEXTURE2D(_DetailData,sampler_DetailData,p.xy);
                half4 detail=x*weights.x+y*weights.y+z*weights.z;
                half3 gradient=(half3(0,x.g-.5h,x.r-.5h)*weights.x+
                    half3(y.r-.5h,0,y.g-.5h)*weights.y+
                    half3(z.r-.5h,z.g-.5h,0)*weights.z)*2*_Relief;
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
                half visibility=DFMicroVisibility(input.positionWS,_FineScale);
                float3 fineDx=ddx(input.positionWS)*_FineScale,fineDy=ddy(input.positionWS)*_FineScale;
                if(_FineRelief>0 && visibility>0)
                {
                    half3 fineGradient;
                    half4 fine=DFMicroTriplanar(TEXTURE2D_ARGS(_FineData,sampler_FineData),input.positionWS*_FineScale,n,fineDx,fineDy,fineGradient);
                    gradient+=fineGradient*(_FineRelief*visibility);
                    albedo*=1+(fine.a-.5h)*(_FineContrast*visibility);
                    detail.b+=(fine.b-.82h)*(.18h*visibility);
                }
                n=normalize(n-(gradient-n*dot(n,gradient)));
                albedo*=1+(detail.a-.5h)*_ValueContrast;
                // Correlate the finish with recesses instead of adding an unrelated gloss
                // layer. Metal plates, bark and wet stone retain their authored roughness.
                half recess=saturate((.5h-detail.a)*2);
                half crest=saturate((detail.a-.5h)*2);
                half roughness=clamp(detail.b+_RoughnessBias+recess*(.095h*_RoughnessWeight)-crest*(.045h*_RoughnessWeight),.03h,.97h);
                half smooth=saturate(lerp(_Smoothness,1-roughness,_RoughnessWeight));
                // Deeper contact response follows actual texture relief. Leaf transmission
                // and sheen stay independent, with no extra texture sample or light.
                half cavity=1-recess*lerp(.16h,.34h,saturate(_Relief));
                half3 rgb=DFSurfaceLighting(input.positionWS,n,albedo,_Metallic,smooth,cavity,_EmissionColor.rgb,input.positionCS);
                half rim=pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(input.positionWS))),4);
                rgb+=albedo*rim*_Sheen;
                if(_Transmission>0)
                {
                    // Thin foliage receives a restrained coloured backlight from the
                    // existing main light. Keep its real shadow/distance attenuation.
                    // Stone, bark, metal, ice and wet surfaces leave this branch disabled.
                    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                        float4 shadow=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                    #else
                        float4 shadow=TransformWorldToShadowCoord(input.positionWS);
                    #endif
                    Light key=GetMainLight(shadow);
                    half back=saturate(dot(-n,key.direction));
                    rgb+=albedo*key.color*(key.distanceAttenuation*key.shadowAttenuation)*
                        (back*back)*(.5h+rim*.5h)*_Transmission;
                }
                return half4(MixFog(rgb,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFShadowVert
            #pragma fragment DFShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFDepthVert
            #pragma fragment DFDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
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

