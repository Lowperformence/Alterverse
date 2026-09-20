Shader "DimensionsFront/HeroSurface"
{
 Properties { _BaseColor("Hit flash override",Color)=(1,1,1,0) _EnergyBoost("Attack energy",Float)=0 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   ZWrite On Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct A { float4 position:POSITION; float3 normal:NORMAL; half4 color:COLOR; };
   struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:COLOR; };
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   half _EnergyBoost;
   CBUFFER_END
   V vert(A i) { V o;o.position=TransformObjectToHClip(i.position.xyz);o.world=TransformObjectToWorld(i.position.xyz);o.normal=TransformObjectToWorldNormal(i.normal);o.color=i.color;return o; }
   half4 frag(V i):SV_Target
   {
    half3 n=normalize(i.normal);Light key=GetMainLight();half3 view=SafeNormalize(GetWorldSpaceViewDir(i.world));

    // Global hero saturation pass. Keep each authored hue/palette intact and only increase
    // chroma uniformly so pale character surfaces read more clearly in combat.
    half3 authoredColor=i.color.rgb;
    half authoredLuma=dot(authoredColor,half3(.2126h,.7152h,.0722h));
    authoredColor=saturate(lerp(authoredLuma.xxx,authoredColor,1.15h));
    half3 albedo=lerp(authoredColor,_BaseColor.rgb,_BaseColor.a);

    half diffuse=.25h+.75h*saturate(dot(n,key.direction));
    half3 ambient=max(SampleSH(n),half3(.20h,.22h,.26h));
    half rim=pow(1-saturate(dot(n,view)),3)*.13h;
    half spec=pow(saturate(dot(n,SafeNormalize(key.direction+view))),20)*.09h;

    // Contrast pass remains independent from the saturation pass above:
    // only LIGHTING luminance is expanded here, preserving scene-light chromaticity.
    // The previous per-channel 0.48 floor lifted shadows into pale midtones and made bright
    // heroes look washed out. This curve restores darker shade separation and clearer highlights.
    half3 rawIllumination=ambient*.45h+key.color*diffuse;
    half rawLuma=max(dot(rawIllumination,half3(.2126h,.7152h,.0722h)),.001h);
    half contrastLuma=lerp(.36h,1.12h,smoothstep(.34h,.96h,rawLuma));
    half3 illumination=rawIllumination*(contrastLuma/rawLuma);

    half3 lit=albedo*illumination+spec*min(key.color,1.0h)*.4h+rim*albedo;
    return half4(lerp(lit,albedo*(1.35h+_EnergyBoost*.55h),saturate(i.color.a)),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}

