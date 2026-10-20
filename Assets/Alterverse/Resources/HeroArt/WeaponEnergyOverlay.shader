Shader "DimensionsFront/WeaponEnergyOverlay"
{
 Properties
 {
  [HDR] _GlowColor("Fluorescent rune color and finish opacity",Color)=(.3,.5,1,.24)
  _MetalColor("Brushed alloy",Color)=(.18,.20,.24,1)
  _Metallic("Metallic",Range(0,1))=.88
  _Smoothness("Polish",Range(0,1))=.72
  _RimOpacity("Edge opacity",Range(0,1))=.55
  _PatternStrength("Rune strength",Range(0,1))=0
  _PatternStyle("Hero rune style",Float)=0
  _PatternOffset("Colorway rune offset",Float)=0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   ZTest LEqual
   Cull Back
   Offset -1,-1
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #include "Assets/Resources/DF_SurfaceLighting.hlsl"
   struct Attributes
   {
    float4 position:POSITION;
    float3 normal:NORMAL;
    float3 pattern:TEXCOORD2;
    float3 restNormal:TEXCOORD3;
   };
   struct Varyings
   {
    float4 position:SV_POSITION;
    float3 world:TEXCOORD0;
    half3 normal:TEXCOORD1;
    float3 pattern:TEXCOORD2;
    half3 projection:TEXCOORD3;
   };
   CBUFFER_START(UnityPerMaterial)
   half4 _GlowColor;
   half4 _MetalColor;
   half _Metallic;
   half _Smoothness;
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
    // Both coordinates and projection come from the undeformed mesh.
    half3 weights=abs(i.restNormal);
    weights*=weights;
    half total=weights.x+weights.y+weights.z;
    o.projection=total>.001h?weights/total:half3(0,0,1);
    return o;
   }
   float RuneSegment(float2 p,float2 a,float2 b)
   {
    float2 ab=b-a;
    return length(p-a-ab*saturate(dot(p-a,ab)/max(dot(ab,ab),.0001)));
   }
   float2 RuneMarks(float2 p)
   {
    float2 q=frac(p*2.4+float2(_PatternOffset,_PatternOffset*.37))-.5;
    float d=RuneSegment(q,float2(0,-.34),float2(0,.34));
    if(_PatternStyle<.5)
    {
     // Circuit chevrons: rifle and chronolux.
     d=min(d,RuneSegment(q,float2(-.27,.10),float2(0,.32)));
     d=min(d,RuneSegment(q,float2(0,.32),float2(.27,.10)));
     d=min(d,RuneSegment(q,float2(-.27,-.20),float2(0,.02)));
     d=min(d,RuneSegment(q,float2(0,.02),float2(.27,-.20)));
    }
    else if(_PatternStyle<1.5)
    {
     // Arcane diamond with a central staff and side wards.
     d=min(d,abs(abs(q.x)+abs(q.y)-.29)*.7071);
     d=min(d,RuneSegment(q,float2(-.34,-.16),float2(-.34,.16)));
     d=min(d,RuneSegment(q,float2(.34,-.16),float2(.34,.16)));
    }
    else if(_PatternStyle<2.5)
    {
     // Broad angular forge mark for hammer and gauntlets.
     d=min(d,RuneSegment(q,float2(-.27,.28),float2(.27,.28)));
     d=min(d,RuneSegment(q,float2(-.27,.28),float2(-.15,.06)));
     d=min(d,RuneSegment(q,float2(.27,.28),float2(.15,.06)));
     d=min(d,RuneSegment(q,float2(-.24,-.08),float2(0,-.30)));
     d=min(d,RuneSegment(q,float2(0,-.30),float2(.24,-.08)));
    }
    else
    {
     // Split rift sigil for blades, anchor and greatsword.
     d=min(d,RuneSegment(q,float2(-.27,.27),float2(.22,-.05)));
     d=min(d,RuneSegment(q,float2(.22,-.05),float2(-.12,-.31)));
     d=min(d,RuneSegment(q,float2(-.27,.27),float2(-.27,.02)));
     d=min(d,RuneSegment(q,float2(.25,.27),float2(.05,.13)));
    }
    // Pixel derivatives filter both the luminous core and the recessed border.
    float aa=max(fwidth(d),.002);
    return 1-smoothstep(float2(.027,.060),float2(.027,.060)+aa,d);
   }
   float BrushGrain(float2 p)
   {
    float phase=p.x*96+p.y*5;
    float visibility=1-smoothstep(.20,.75,fwidth(phase));
    return sin(phase*6.2831853)*visibility;
   }
   half4 frag(Varyings i):SV_Target
   {
    half3 n=normalize(i.normal);
    half3 view=SafeNormalize(GetWorldSpaceViewDir(i.world));
    half rim=pow(1-saturate(dot(n,view)),2.8h);
    float2 marks=RuneMarks(i.pattern.yz)*i.projection.x
                +RuneMarks(i.pattern.zx)*i.projection.y
                +RuneMarks(i.pattern.xy)*i.projection.z;
    marks*=_PatternStrength;
    float grain=BrushGrain(i.pattern.yz)*i.projection.x
               +BrushGrain(i.pattern.zx)*i.projection.y
               +BrushGrain(i.pattern.xy)*i.projection.z;
    half3 alloy=_MetalColor.rgb*(.95h+grain*.05h);
    alloy*=1-marks.y*.55h;
    half polish=saturate(_Smoothness+grain*.055h-marks.y*.18h);
    half3 metal=DFSurfaceLighting(i.world,n,alloy,_Metallic,polish,1,half3(0,0,0),i.position);
    // Emission stays independent of scene light; the metal keeps real shading.
    half3 color=metal+_GlowColor.rgb*(marks.x*2.4h+rim*.40h);
    half alpha=saturate(lerp(_GlowColor.a,_RimOpacity,rim)+marks.x*.76h);
    return half4(color,alpha);
   }
   ENDHLSL
  }
 }
}


