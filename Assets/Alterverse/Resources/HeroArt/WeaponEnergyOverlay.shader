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
   float Line(float distance,float width)
   {
    float aa=max(fwidth(distance),.0008);
    return 1-smoothstep(width,width+aa,distance);
   }
   float Engraving(float2 p)
   {
    p=p*5.5+_PatternOffset;
    float2 cell=frac(p)-.5;
    float2 id=floor(p);
    float select=frac(sin(dot(id,float2(12.9898,78.233)))*43758.5453);
    float rivet=Line(length(cell-float2(.32,.32)),.045)*step(.65,select);
    if(_PatternStyle<.5)
    {
     // Short circuit traces and small terminals.
     float trace=Line(abs(cell.x+.16),.030)*step(abs(cell.y),.30);
     trace=max(trace,Line(abs(cell.y-.28),.030)*step(abs(cell.x),.18));
     return max(rivet,trace*step(.52,select));
    }
    if(_PatternStyle<1.5)
    {
     // Sparse diamond runes.
     float rune=Line(abs(abs(cell.x)+abs(cell.y)-.23),.030);
     return max(rivet,rune*step(.63,select));
    }
    if(_PatternStyle<2.5)
    {
     // Panel corners and restrained fastener heads.
     float corner=max(Line(abs(cell.x+.30),.028)*step(abs(cell.y+.18),.12),
                      Line(abs(cell.y+.30),.028)*step(abs(cell.x+.18),.12));
     return max(rivet,corner*step(.45,select));
    }
    // Paired diagonal cuts on the assassin's blades.
    float cuts=max(Line(abs(cell.x+cell.y-.08),.030),
                   Line(abs(cell.x+cell.y+.08),.030))*step(abs(cell.x-cell.y),.24);
    return max(rivet,cuts*step(.65,select));
   }
   half4 frag(Varyings i):SV_Target
   {
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
