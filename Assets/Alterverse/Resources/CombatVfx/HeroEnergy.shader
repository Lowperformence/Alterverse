Shader "DimensionsFront/HeroEnergy"
{
 Properties { _Tint("Tint",Color)=(1,1,1,1) _Polish("Energy core",Range(0,1))=0 _Life("Normalized age",Range(0,1))=0 _SurfaceStyle("Surface response",Float)=0 _DetailQuality("Surface detail budget",Range(0,1))=1 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off ZTest LEqual Cull Off
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Assets/Alterverse/Resources/CombatVfx/HeroEnergySurface.hlsl"
   struct A { float4 position:POSITION; half4 color:COLOR; float3 normal:NORMAL; };
   struct V { float4 position:SV_POSITION; half4 color:COLOR; half heat:TEXCOORD0; float3 local:TEXCOORD1; float2 surfaceUV:TEXCOORD2; half facing:TEXCOORD3; };
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   half _Polish;
   half _Life;
   half _SurfaceStyle;
   half _DetailQuality;
   CBUFFER_END
   V vert(A i)
   {
    V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color*_Tint;
    o.heat=saturate((max(i.color.r,max(i.color.g,i.color.b))-1.0h)*2.5h);o.local=i.position.xyz;
    float3 normalOS=SafeNormalize(i.normal);
    float3 axis=abs(normalOS);
    o.surfaceUV=axis.y>=max(axis.x,axis.z)?i.position.xz:axis.x>=axis.z?i.position.zy:i.position.xy;
    // Orbs use their radial normal, including under nonuniform visual scale.
    if(_SurfaceStyle==1)normalOS=SafeNormalize(i.position.xyz);
    half3 normalWS=TransformObjectToWorldNormal(normalOS);
    half3 view=GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(i.position.xyz));
    o.facing=saturate(abs(dot(normalWS,view)));return o;
   }
   half4 frag(V i):SV_Target
   {
    // Only authored hot vertices approach white; silhouettes retain their hero hue.
    // Warning slots opt out, retaining their exact established alpha and shape.
    half core=i.heat*_Polish;
    half3 color=lerp(i.color.rgb,min(i.color.rgb+half3(.55h,.55h,.55h),1.8h),core*.62h);
    // Uniform warning branch bypasses every new surface response. Alpha is inherited
    // from the original mesh, so small contours, dark portal cores and contact timing survive.
    if(_Polish>0 && _SurfaceStyle>0)
     color+=DFEnergySurface(i.local,i.surfaceUV,i.facing,i.heat,min(i.color.rgb,1.0h),
       _Life,_SurfaceStyle,_DetailQuality)*_Polish;
    return half4(color,i.color.a*DFEnergyOriginalOpacity(i.local,_Life,_SurfaceStyle,_Polish));
   }
   ENDHLSL
  }
 }
}

