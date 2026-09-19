Shader "DimensionsFront/HeroEnergy"
{
 Properties { _Tint("Tint",Color)=(1,1,1,1) }
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
   struct V { float4 position:SV_POSITION; half4 color:COLOR; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   CBUFFER_END
   V vert(A i) { V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color*_Tint;return o; }
   half4 frag(V i):SV_Target { return i.color; }
   ENDHLSL
  }
 }
}
