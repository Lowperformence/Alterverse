Shader "Alterverse/RGBWingEnergy"
{
    Properties
    {
        [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
        _HueShift("Hue cycle",Range(0,1))=0
        _Intensity("Glow",Range(0,4))=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Resources/DF_DeathFade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _HueShift;
                half _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; };
            struct Varyings { float4 positionHCS:SV_POSITION; half3 color:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS=TransformObjectToHClip(input.positionOS.xyz);
                output.color=input.color.rgb;
                output.fog=ComputeFogFactor(output.positionHCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                DFApplyDeathFade(input.positionHCS);
                // HSV hue rotation stays saturated through the whole seamless cycle.
                half4 K=half4(0,-1.0h/3.0h,2.0h/3.0h,-1);
                half4 p=lerp(half4(input.color.bg,K.wz),half4(input.color.gb,K.xy),step(input.color.b,input.color.g));
                half4 q=lerp(half4(p.xyw,input.color.r),half4(input.color.r,p.yzx),step(p.x,input.color.r));
                half d=q.x-min(q.w,q.y);
                half hue=frac(abs(q.z+(q.w-q.y)/(6*d+0.0001h))+_HueShift);
                half saturation=d/(q.x+0.0001h);
                half3 rgb=q.x*lerp(half3(1,1,1),saturate(abs(frac(hue+half3(0,2.0h/3.0h,1.0h/3.0h))*6-3)-1),saturation);
                return half4(MixFog(rgb*_BaseColor.rgb*_Intensity,input.fog),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
