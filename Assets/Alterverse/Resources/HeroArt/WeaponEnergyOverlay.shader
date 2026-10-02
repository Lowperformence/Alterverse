Shader "DimensionsFront/WeaponEnergyOverlay"
{
 Properties
 {
  _GlowColor("Translucent color",Color)=(.3,.5,1,.24)
  _RimOpacity("Edge opacity",Range(0,1))=.35
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   ZTest LEqual
   Cull Back
   Offset -1,-1
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 position:POSITION; float3 normal:NORMAL; };
   struct Varyings { float4 position:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; };
   CBUFFER_START(UnityPerMaterial)
   half4 _GlowColor;
   half _RimOpacity;
   CBUFFER_END
   Varyings vert(Attributes i)
   {
    Varyings o;
    o.position=TransformObjectToHClip(i.position.xyz);
    o.world=TransformObjectToWorld(i.position.xyz);
    o.normal=TransformObjectToWorldNormal(i.normal);
    return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    half3 n=normalize(i.normal);
    half3 view=SafeNormalize(GetWorldSpaceViewDir(i.world));
    half rim=pow(1-saturate(dot(n,view)),2.8h);
    // Transparent faces reveal the original metal, engraving and shading.
    // Only grazing edges carry a brighter fluorescent highlight.
    half alpha=lerp(_GlowColor.a,_RimOpacity,rim);
    return half4(_GlowColor.rgb*lerp(1.05h,2.0h,rim),alpha);
   }
   ENDHLSL
  }
 }
}
