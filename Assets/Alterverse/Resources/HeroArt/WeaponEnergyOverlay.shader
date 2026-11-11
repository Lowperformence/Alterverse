Shader "DimensionsFront/WeaponEnergyOverlay"
{
 Properties
 {
  [HDR] _GlowColor("Fluorescent rune color and finish opacity",Color)=(.3,.5,1,.30)
  _MetalColor("Brushed alloy",Color)=(.18,.20,.24,1)
  _Metallic("Metallic",Range(0,1))=.88
  _Smoothness("Polish",Range(0,1))=.76
  _RimOpacity("Edge opacity",Range(0,1))=.76
  _PatternStrength("Rune strength",Range(0,1))=1
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
    // Rotate slightly by colorway offset so Variant 0 and Variant 1 have distinct rune flow.
    float ang=.22+frac(_PatternOffset*.73)*.42;
    float s=sin(ang),c=cos(ang);
    float2 rp=float2(c*p.x-s*p.y,s*p.x+c*p.y);
    float2 cont=rp*3.45+float2(_PatternOffset*1.31,_PatternOffset*.67);
    // Compute derivative width from continuous coordinates BEFORE frac() to prevent seam dropouts.
    float2 fw=max(fwidth(cont),.0025);
    float aa=clamp(max(fw.x,fw.y)*.45,.003,.024);
    float2 cell=floor(cont);
    float2 q=frac(cont)-.5;
    if(fmod(abs(cell.x+cell.y+round(_PatternOffset*2.0)),2.0)>.5)
     q.x=-q.x;
    float d=RuneSegment(q,float2(0,-.38),float2(0,.38));
    if(_PatternStyle<.5)
    {
     // Circuit chevrons & dual rail sigils: rifle, chronolux, mech weapons.
     d=min(d,RuneSegment(q,float2(-.34,.08),float2(0,.36)));
     d=min(d,RuneSegment(q,float2(0,.36),float2(.34,.08)));
     d=min(d,RuneSegment(q,float2(-.34,-.20),float2(0,.06)));
     d=min(d,RuneSegment(q,float2(0,.06),float2(.34,-.20)));
     d=min(d,RuneSegment(q,float2(-.36,-.34),float2(.36,-.34)));
    }
    else if(_PatternStyle<1.5)
    {
     // Arcane sunburst diamond with inner ring & side wards.
     d=min(d,abs(abs(q.x)+abs(q.y)-.32)*.7071);
     d=min(d,abs(length(q)-.18));
     d=min(d,RuneSegment(q,float2(-.36,-.22),float2(-.36,.22)));
     d=min(d,RuneSegment(q,float2(.36,-.22),float2(.36,.22)));
    }
    else if(_PatternStyle<2.5)
    {
     // Forge hex-crown & conduit mark for hammer and gauntlets.
     d=min(d,RuneSegment(q,float2(-.34,.30),float2(.34,.30)));
     d=min(d,RuneSegment(q,float2(-.34,.30),float2(-.16,.04)));
     d=min(d,RuneSegment(q,float2(.34,.30),float2(.16,.04)));
     d=min(d,RuneSegment(q,float2(-.30,-.06),float2(0,-.36)));
     d=min(d,RuneSegment(q,float2(0,-.36),float2(.30,-.06)));
     d=min(d,RuneSegment(q,float2(-.26,0),float2(.26,0)));
    }
    else
    {
     // Split rift sigil & starlight cross for blades, anchor and greatsword.
     d=min(d,RuneSegment(q,float2(-.34,.32),float2(.28,-.06)));
     d=min(d,RuneSegment(q,float2(.28,-.06),float2(-.18,-.36)));
     d=min(d,RuneSegment(q,float2(-.34,.32),float2(-.34,-.04)));
     d=min(d,RuneSegment(q,float2(.32,.32),float2(-.32,-.32)));
     d=min(d,abs(length(q)-.24));
    }
    // Continuous energy veins & micro-glyph lattice so thin weapon parts always show crisp runes.
    float vein=abs(sin((cont.x+cont.y*.72)*3.14159265))*.18;
    float2 mq=frac(cont*2.0+.25)-.5;
    float micro=abs(abs(mq.x)+abs(mq.y)-.26)*.42;
    float dCore=min(d,min(vein+.014,micro+.016));
    float2 mark=1-smoothstep(float2(.042,.088),float2(.042,.088)+aa,dCore);
    return mark;
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
    half rim=pow(1-saturate(dot(n,view)),2.35h);
    float2 marks=RuneMarks(i.pattern.yz)*i.projection.x
                +RuneMarks(i.pattern.zx)*i.projection.y
                +RuneMarks(i.pattern.xy)*i.projection.z;
    marks*=_PatternStrength;
    float grain=BrushGrain(i.pattern.yz)*i.projection.x
               +BrushGrain(i.pattern.zx)*i.projection.y
               +BrushGrain(i.pattern.xy)*i.projection.z;
    half3 alloy=_MetalColor.rgb*(.95h+grain*.05h);
    alloy*=1-marks.y*.50h;
    half polish=saturate(_Smoothness+grain*.055h-marks.y*.15h);
    half3 metal=DFSurfaceLighting(i.world,n,alloy,_Metallic,polish,1,half3(0,0,0),i.position);
    half pulse=.92h+.14h*sin(_Time.y*2.8h+(i.pattern.x+i.pattern.y+i.pattern.z)*2.6h);
    half3 hotCore=lerp(_GlowColor.rgb,half3(1,1,1),marks.x*.38h);
    // Boosted HDR emission for both the rune network and the energized fresnel rim.
    half3 color=metal+hotCore*((marks.x*4.25h+marks.y*1.25h+rim*1.15h)*pulse);
    half alpha=saturate(lerp(_GlowColor.a,_RimOpacity,rim)+marks.x*.88h+marks.y*.34h);
    return half4(color,alpha);
   }
   ENDHLSL
  }
 }
}
