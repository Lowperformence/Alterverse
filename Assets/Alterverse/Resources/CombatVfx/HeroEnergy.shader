Shader "DimensionsFront/HeroEnergy"
{
 Properties { _Tint("Tint",Color)=(1,1,1,1) _Polish("Energy core",Range(0,1))=0 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off ZTest LEqual Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 position:POSITION; half4 color:COLOR; };
   struct V { float4 position:SV_POSITION; half4 color:COLOR; half heat:TEXCOORD0; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   half _Polish;
   CBUFFER_END
   V vert(A i) { V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color*_Tint;o.heat=saturate((max(i.color.r,max(i.color.g,i.color.b))-1.0h)*2.5h);return o; }
   half4 frag(V i):SV_Target
   {
    // Only authored hot vertices approach white; silhouettes retain their hero hue.
    // Warning slots opt out, retaining their exact established alpha and shape.
    half core=i.heat*_Polish;
    half3 color=lerp(i.color.rgb,min(i.color.rgb+half3(.55h,.55h,.55h),1.8h),core*.62h);
    return half4(color,i.color.a);
   }
   ENDHLSL
  }
 }
}

