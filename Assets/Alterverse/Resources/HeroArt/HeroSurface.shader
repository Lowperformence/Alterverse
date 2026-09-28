Shader "DimensionsFront/HeroSurface"
{
 Properties
 {
  _BaseColor("Hit flash override",Color)=(1,1,1,0)
  _EnergyBoost("Attack energy",Float)=0
  _ShowcaseLighting("Portrait studio lighting",Range(0,1))=0
 }
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
   struct A { float4 position:POSITION; float3 normal:NORMAL; half4 color:COLOR; half2 finish:TEXCOORD1; };
   struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:COLOR; half2 finish:TEXCOORD2; };
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   half _EnergyBoost;
   half _ShowcaseLighting;
   CBUFFER_END
   V vert(A i)
   {
    V o;o.position=TransformObjectToHClip(i.position.xyz);o.world=TransformObjectToWorld(i.position.xyz);
    o.normal=TransformObjectToWorldNormal(i.normal);o.color=i.color;o.finish=i.finish;return o;
   }
   half3 Lobe(half3 n,half3 view,half3 direction,half3 lightColor,half3 albedo,half metal,half smoothness)
   {
    half ndl=saturate(dot(n,direction));
    half diffuse=smoothstep(-.18h,.88h,dot(n,direction));
    half power=lerp(12.0h,96.0h,smoothness);
    half spec=pow(saturate(dot(n,SafeNormalize(direction+view))),power)*ndl;
    half3 specColor=lerp(half3(.16h,.18h,.21h),lerp(albedo,1.0h,.52h),metal);
    half broad=pow(saturate(dot(n,SafeNormalize(direction+view))),lerp(5.0h,18.0h,smoothness))*ndl;
    return (albedo*diffuse*(1.0h-metal*.28h)+specColor*(spec*(.65h+smoothness*.7h)+broad*.12h*smoothness))*lightColor;
   }
   half4 frag(V i):SV_Target
   {
    half3 n=normalize(i.normal),view=SafeNormalize(GetWorldSpaceViewDir(i.world));
    half3 albedo=lerp(i.color.rgb,_BaseColor.rgb,saturate(_BaseColor.a));
    half metal=saturate(i.finish.x),smoothness=saturate(i.finish.y);
    Light key=GetMainLight();
    half3 ambient=max(SampleSH(n),half3(.16h,.19h,.24h));
    half3 worldLight=albedo*ambient*.65h+Lobe(n,view,key.direction,min(key.color,1.6h),albedo,metal,smoothness);
    // Camera-space studio rig is isolated per portrait renderer with a property block.
    // It stays stable across planets and doesn't need extra per-pixel lights on Android.
    half3 nv=normalize(mul((float3x3)UNITY_MATRIX_V,n));
    half3 vv=normalize(mul((float3x3)UNITY_MATRIX_V,view));
    half3 studio=albedo*half3(.15h,.18h,.24h);
    studio+=Lobe(nv,vv,normalize(half3(-.45h,.65h,.80h)),half3(.98h,.96h,.92h),albedo,metal,smoothness);
    studio+=Lobe(nv,vv,normalize(half3(.75h,.10h,.55h)),half3(.18h,.28h,.40h),albedo,metal,smoothness);
    half fresnel=pow(1.0h-saturate(dot(n,view)),3.5h);
    half rimSide=smoothstep(-.35h,.75h,nv.x);
    half3 rimColor=lerp(half3(.30h,.42h,.65h),half3(.38h,.72h,.95h),rimSide);
    half upper=saturate(n.y*.5h+.5h);
    half3 bounce=albedo*half3(.08h,.11h,.16h)*(1.0h-upper);
    half silhouette=pow(1.0h-saturate(dot(n,view)),2.4h);
    half3 lit=lerp(worldLight,studio,_ShowcaseLighting)+bounce;
    lit+=rimColor*silhouette*lerp(.22h,.48h,_ShowcaseLighting)*(1.0h-metal*.18h);
    lit+=albedo*fresnel*metal*.13h;
    half3 energy=albedo*(1.42h+_EnergyBoost*.60h)+rimColor*fresnel*.30h;
    return half4(lerp(lit,energy,saturate(i.color.a)),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
