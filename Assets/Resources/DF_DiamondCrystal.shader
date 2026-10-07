Shader "Alterverse/DiamondCrystal"
{
    Properties
    {
        [MainColor] _BaseColor("Crystal Tint", Color) = (0.65,0.9,1,0.35)
        [HDR] _EmissionColor("Emission", Color) = (0.08,0.35,0.55,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.92
        _FresnelPower("Fresnel Power", Range(0.5,8)) = 3.2
        _FresnelStrength("Fresnel Strength", Range(0,2)) = 0.68
        _CoreAlpha("Core Alpha", Range(0,1)) = 0.22
        _FacetDetail("Cut edge and glint detail", Range(0,1)) = 0
        _MacroData("Blender geological value cavity roughness sediment", 2D) = "gray" {}
        _SurfaceData("Linear surface slope roughness height", 2D) = "gray" {}
        _SurfaceRelief("Surface micro relief", Range(0,1)) = 0
        _SurfaceScale("Surface world frequency", Float) = .25
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DiamondCrystalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Smoothness;
                half _FresnelPower;
                half _FresnelStrength;
                half _CoreAlpha;
                half _FacetDetail;
                half _SurfaceRelief;
                float _SurfaceScale;
            CBUFFER_END

            TEXTURE2D(_MacroData); SAMPLER(sampler_MacroData);
            TEXTURE2D(_SurfaceData); SAMPLER(sampler_SurfaceData);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewDirWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                half3 cutCoordinates : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.cutCoordinates = input.color.rgb;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positions.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half surfaceSmoothness=_Smoothness;
                half surfaceValue=1;
                if(_SurfaceRelief>.001h)
                {
                    half4 detail=SAMPLE_TEXTURE2D(_SurfaceData,sampler_SurfaceData,input.positionWS.xz*_SurfaceScale);
                    half3 gradient=half3(detail.r-.5h,0,detail.g-.5h)*2*_SurfaceRelief;
                    n=normalize(n-(gradient-n*dot(n,gradient)));
                    half4 geology=SAMPLE_TEXTURE2D(_MacroData,sampler_MacroData,input.positionWS.xz*.047);
                    surfaceSmoothness=saturate(_Smoothness-(lerp(detail.b,geology.b,.35h)-.5h)*.16h*_SurfaceRelief);
                    surfaceValue=(1+(geology.r-.5h)*.34h)*lerp(.96h,1.0h,geology.g);
                }
                half3 v = normalize(input.viewDirWS);
                surfaceSmoothness=saturate(surfaceSmoothness-min(.18h,length(fwidth(n))*.16h));
                half ndv = saturate(abs(dot(n,v)));
                half fresnel = pow(1.0h-ndv,max(0.5h,_FresnelPower));

                // Cheap prismatic variation: view/normal dependent RGB splitting without refraction textures.
                half prism = saturate(n.x*0.5h+n.z*0.5h+0.5h);
                half3 prismTint = lerp(half3(0.78h,0.94h,1.0h),half3(1.0h,0.72h,0.96h),prism);
                half3 rgb = _BaseColor.rgb*lerp(0.72h,1.12h,surfaceSmoothness)*surfaceValue;
                rgb = lerp(rgb,rgb*prismTint,saturate(fresnel*0.42h));
                rgb += _EmissionColor.rgb;
                rgb += fresnel*_FresnelStrength*half3(0.62h,0.88h,1.0h);
                // Actual sky/probe reflections follow each physical cut while the original prism tint stays intact.
                half3 reflected=GlossyEnvironmentReflection(reflect(-v,n),input.positionWS,
                    1-surfaceSmoothness,1,GetNormalizedScreenSpaceUV(input.positionHCS));
                rgb+=reflected*lerp(.08h,.28h,fresnel)*lerp(.35h,1.0h,surfaceSmoothness);
                // Flat face normals create separate reflections on each physical cut.
                // Edge highlights use actual triangle boundaries and a fixed pixel width.
                Light key=GetMainLight();
                half3 halfway=SafeNormalize(key.direction+v);
                half specular=pow(saturate(dot(n,halfway)),lerp(36.0h,140.0h,surfaceSmoothness));
                half facetShade=0.48h+0.52h*saturate(dot(n,key.direction));
                float3 edgeWidth=max(fwidth(input.cutCoordinates)*1.15,0.0001);
                half3 inside=smoothstep(0.0,edgeWidth,input.cutCoordinates);
                half edge=1.0h-min(inside.x,min(inside.y,inside.z));
                half glint=pow(saturate(dot(n,SafeNormalize(half3(-.45h,.8h,.35h)+v))),96.0h);
                rgb=lerp(rgb,rgb*facetShade+key.color*specular*.95h+
                    prismTint*edge*.20h+half3(.75h,.9h,1.0h)*glint*.45h,_FacetDetail);
                rgb = MixFog(rgb,input.fogFactor);

                half alpha = saturate(_CoreAlpha + _BaseColor.a*0.55h + fresnel*_FresnelStrength*0.34h);
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

