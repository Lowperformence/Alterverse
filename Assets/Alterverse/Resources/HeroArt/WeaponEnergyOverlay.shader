Shader "DimensionsFront/WeaponEnergyOverlay"
{
 Properties
 {
  _GlowColor("Translucent color",Color)=(.3,.5,1,.24)
  _RimOpacity("Edge opacity",Range(0,1))=.5
  _PatternStrength("Small detail contrast",Range(0,1))=0
  _PatternStyle("Hero engraving style",Float)=0
  _PatternOffset("Colorway engraving offset",Float)=0
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
   #include "Assets/Resources/DF_DeathFade.hlsl"
   struct Attributes { float4 position:POSITION; float3 normal:NORMAL; float3 pattern:TEXCOORD2; };
   struct Varyings { float4 position:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; float3 pattern:TEXCOORD2; };
   CBUFFER_START(UnityPerMaterial)
   half4 _GlowColor;
   half _RimOpacity;
   half _PatternStrength;
   float _PatternStyle;
   float _PatternOffset;
   CBUFFER_END
   Varyings vert(Attributes i)
   {
    Varyings o;
    o.position=TransformObjectToHClip(i.position.xyz);
    o.world=TransformObjectToWorld(i.position.xyz);
    o.normal=TransformObjectToWorldNormal(i.normal);
    o.pattern=i.pattern;
    return o;
   }
float OrnamentLine(float d,float width)
{
 float aa=max(fwidth(d),.001);
 return 1-smoothstep(width,width+aa,d);
}
float Ornament(float2 p)
{
 float2 q=frac(p)-.5;
 float chain=OrnamentLine(abs(q.x),.009);
 float beads=OrnamentLine(length(float2(q.x,frac((q.y+.5)*6)-.5)),.065);
 float moon=OrnamentLine(max(length(q-float2(.0,.08))-.19,-(length(q-float2(.075,.12))-.16)),.01);
 moon*=step(.03,q.y)*step(q.y,.30);
 float2 f=q-float2(0,-.27);
 float flower=0;
 for(int k=0;k<6;k++)
 {
  float a=k*1.04719755;
  float2 center=float2(cos(a),sin(a))*.085;
  flower=max(flower,OrnamentLine(length(f-center),.031));
 }
 return saturate(max(chain,max(beads,max(moon,flower))));
}
   float Engraving(float2 p){return Ornament(p*2.4+_PatternOffset);}
   half4 frag(Varyings i):SV_Target
   {
    DFApplyDeathFade(i.position);
    half3 n=normalize(i.normal);
    half3 view=SafeNormalize(GetWorldSpaceViewDir(i.world));
    half rim=pow(1-saturate(dot(n,view)),2.8h);
    // Transparent faces reveal the original metal, engraving and shading.
    // Only grazing edges carry a brighter fluorescent highlight.
    half alpha=lerp(_GlowColor.a,_RimOpacity,rim);
    // Coordinates come from the undeformed mesh, so patterns follow the weapon.
    float marks=max(Engraving(i.pattern.xy),max(Engraving(i.pattern.yz),Engraving(i.pattern.zx)));
    half detail=saturate(marks)*_PatternStrength;
    half3 color=_GlowColor.rgb*lerp(1.05h,2.0h,rim);
    color=lerp(color,color*2.35h+half3(.28h,.30h,.34h),detail);
    // Detail changes brightness only; it never increases opacity.
    return half4(color,alpha);
   }
   ENDHLSL
  }
 }
}

