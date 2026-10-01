Shader "DimensionsFront/HeroEnergy"
{
 Properties { _Tint("Tint",Color)=(1,1,1,1) _Polish("Energy core",Range(0,1))=0 _Life("Normalized age",Range(0,1))=0 _SurfaceStyle("Surface response",Float)=0 }
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
   struct V { float4 position:SV_POSITION; half4 color:COLOR; half heat:TEXCOORD0; float3 local:TEXCOORD1; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   half _Polish;
   half _Life;
   half _SurfaceStyle;
   CBUFFER_END
   V vert(A i) { V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color*_Tint;o.heat=saturate((max(i.color.r,max(i.color.g,i.color.b))-1.0h)*2.5h);o.local=i.position.xyz;return o; }
   half4 frag(V i):SV_Target
   {
    // Only authored hot vertices approach white; silhouettes retain their hero hue.
    // Warning slots opt out, retaining their exact established alpha and shape.
    half core=i.heat*_Polish;
    half3 color=lerp(i.color.rgb,min(i.color.rgb+half3(.55h,.55h,.55h),1.8h),core*.62h);
    // Object-space flowing filaments: no textures, screen distortion or additional lights.
    // Age is supplied per pooled effect; paused gameplay freezes the surface too.
    float flow=dot(i.local,float3(2.7,1.9,3.1))-_Life*1.7;
    float stripe=abs(frac(flow)-.5)*2.0;
    float width=max(fwidth(flow)*2.0,.055);
    half filament=1.0-smoothstep(.10,.10+width,stripe);
    half body=saturate(1.0-length(i.local)*.62);
    half volume=_SurfaceStyle==1?body*body:0;
    half shell=_SurfaceStyle==2?filament*.30h:0;
    half release=1.0h-smoothstep(.0h,.24h,_Life);
    half detail=_Polish*saturate(_SurfaceStyle);
    color*=1.0h+detail*(filament*.16h+volume*.27h+shell);
    color+=min(i.color.rgb,1.0h)*detail*(volume*.12h+core*release*.18h);
    half alpha=i.color.a*lerp(1.0h,.86h+filament*.14h,detail);
    return half4(color,alpha);
   }
   ENDHLSL
  }
 }
}

