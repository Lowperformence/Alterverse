Shader "DimensionsFront/HeroSurface"
{
 Properties { _BaseColor("Hit flash override",Color)=(1,1,1,0) _EnergyBoost("Attack energy",Float)=0 _SurfaceFinish("Surface finish",Float)=1 }
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
   half _SurfaceFinish;
   CBUFFER_END
   V vert(A i) { V o;o.position=TransformObjectToHClip(i.position.xyz);o.world=TransformObjectToWorld(i.position.xyz);o.normal=TransformObjectToWorldNormal(i.normal);o.color=i.color;return o; }
   half4 frag(V i):SV_Target
   {
    half3 n=normalize(i.normal);Light key=GetMainLight();half3 view=SafeNormalize(GetWorldSpaceViewDir(i.world));
    half3 albedo=lerp(i.color.rgb,_BaseColor.rgb,_BaseColor.a);
    // Character-only grade. UI and arena colors are untouched: increase chroma and local
    // contrast before lighting so the hero does not wash into grey/pastel midtones.
    half luma=dot(albedo,half3(.2126h,.7152h,.0722h));
    albedo=saturate(lerp(luma.xxx,albedo,1.10h));
    albedo=saturate((albedo-.5h)*1.06h+.5h);
    half diffuse=.22h+.78h*saturate(dot(n,key.direction));
    half3 ambient=max(SampleSH(n),half3(.18h,.20h,.24h));
    half finish=saturate(_SurfaceFinish);
    half rim=pow(1-saturate(dot(n,view)),3)*lerp(.075h,.12h,finish);
    half specPower=lerp(10h,28h,finish);
    half specStrength=lerp(.018h,.070h,finish);
    half spec=pow(saturate(dot(n,SafeNormalize(key.direction+view))),specPower)*specStrength;
    // Keep enough floor light for mobile readability, but avoid the old bright midpoint wash.
    half3 illumination=clamp(ambient*.40h+key.color*diffuse,half3(.43h,.43h,.43h),half3(1.02h,1.02h,1.02h));
    half3 lit=albedo*illumination+spec*min(key.color,1.0h)*.30h+rim*albedo;
    return half4(lerp(lit,albedo*(1.42h+_EnergyBoost*.55h),saturate(i.color.a)),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}

