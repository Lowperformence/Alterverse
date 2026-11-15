Shader "DimensionsFront/WeaponEnergyOverlay"
{
 Properties
 {
  [HDR] _GlowColor("Fluorescent rune color and finish opacity",Color)=(.3,.5,1,.78)
  _MetalColor("Brushed alloy",Color)=(.18,.20,.24,1)
  _SecondaryColor("Secondary pattern accent",Color)=(1,1,1,1)
  _Metallic("Metallic",Range(0,1))=.82
  _Smoothness("Polish",Range(0,1))=.84
  _RimOpacity("Edge opacity",Range(0,1))=.92
  _PatternStrength("Rune strength",Range(0,1))=1
  _PatternStyle("Hero rune style",Float)=0
  _PatternVariant("Variant pattern mode",Float)=0
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
   half4 _SecondaryColor;
   half _Metallic;
   half _Smoothness;
   half _RimOpacity;
   half _PatternStrength;
   float _PatternStyle;
   float _PatternVariant;
   float _PatternOffset;
   CBUFFER_END

   Varyings vert(Attributes i)
   {
    Varyings o;
    o.position=TransformObjectToHClip(i.position.xyz);
    o.world=TransformObjectToWorld(i.position.xyz);
    o.normal=TransformObjectToWorldNormal(i.normal);
    o.pattern=i.pattern;
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

   // Hexagonal distance (returns distance to hex border in x, cell id hash in y)
   float2 HexGrid(float2 p)
   {
    const float2 s = float2(1.0, 1.7320508);
    float4 hC = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
    float4 h = float4(p - hC.xy * s, p - (hC.zw + 0.5) * s);
    float4 chosen = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? float4(h.xy, hC.xy) : float4(h.zw, hC.zw + 0.5);
    float2 ab = abs(chosen.xy);
    float edgeDist = 0.5 - max(dot(ab, s * 0.5), ab.x);
    float cellHash = frac(sin(dot(chosen.zw, float2(12.9898, 78.233))) * 43758.5453);
    return float2(edgeDist, cellHash);
   }

   // Returns: x = bright line mask, y = dark groove mask, z = macro two-tone body pattern mask
   float3 PatternMarks(float2 p)
   {
    if (_PatternVariant < 0.5)
    {
     // VARIANT 0: Bold Diagonal Chevron / Tiger Stripes + Sharp Angular Circuit Sigils
     float2 cont = p * 3.2 + float2(_PatternOffset * 0.85, _PatternOffset * 0.41);
     float2 fw = max(fwidth(cont), .0025);
     float aa = clamp(max(fw.x, fw.y) * .45, .003, .025);

     // Macro Chevron / Tiger Stripe bands clearly visible from gameplay distance
     float chevronCoord = cont.y * 1.45 + abs(frac(cont.x * 0.85) - 0.5) * 1.35;
     float stripeWave = sin(chevronCoord * 6.2831853);
     float macroBand = smoothstep(-0.15, 0.15, stripeWave);
     float stripeEdge = 1.0 - smoothstep(0.04, 0.04 + aa * 2.0, abs(stripeWave));

     // Crisp angular rune lines inside the bands
     float2 q = frac(cont) - 0.5;
     float d = RuneSegment(q, float2(-0.36, -0.18), float2(0.0, 0.34));
     d = min(d, RuneSegment(q, float2(0.0, 0.34), float2(0.36, -0.18)));
     d = min(d, RuneSegment(q, float2(-0.28, -0.34), float2(0.28, -0.34)));
     float lineMask = max(stripeEdge, 1.0 - smoothstep(0.042, 0.042 + aa, d));
     float grooveMask = 1.0 - smoothstep(0.085, 0.085 + aa, d);
     return float3(lineMask, grooveMask, macroBand);
    }
    else
    {
     // VARIANT 1: Glowing Hexagonal Honeycomb Lattice + Concentric Diamond/Pulse Rings
     float2 cont = p * 4.4 + float2(_PatternOffset * 1.13, _PatternOffset * 0.79);
     float2 fw = max(fwidth(cont), .0025);
     float aa = clamp(max(fw.x, fw.y) * .45, .003, .025);

     float2 hex = HexGrid(cont);
     float hexLine = 1.0 - smoothstep(0.048, 0.048 + aa, hex.x);
     float hexGroove = 1.0 - smoothstep(0.095, 0.095 + aa, hex.x);

     // Concentric energy rings + alternating filled hex cells for distinct two-tone silhouette
     float ringWave = abs(frac(length(p * 2.2) - _PatternOffset * 0.35) - 0.5);
     float ringLine = 1.0 - smoothstep(0.05, 0.05 + aa, ringWave);
     float macroCell = step(0.48, hex.y);

     return float3(saturate(hexLine + ringLine * 0.85), hexGroove, macroCell);
    }
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
    half rim=pow(1-saturate(dot(n,view)),2.15h);
    float3 marks=PatternMarks(i.pattern.yz)*i.projection.x
                +PatternMarks(i.pattern.zx)*i.projection.y
                +PatternMarks(i.pattern.xy)*i.projection.z;
    marks.xy*=_PatternStrength;
    float grain=BrushGrain(i.pattern.yz)*i.projection.x
               +BrushGrain(i.pattern.zx)*i.projection.y
               +BrushGrain(i.pattern.xy)*i.projection.z;

    // Two-tone patterned chassis coat: blends between _MetalColor and contrasting _SecondaryColor across macro stripes/hex cells
    half3 twoToneCoat=lerp(_MetalColor.rgb, _SecondaryColor.rgb, saturate(marks.z * 0.46h));
    half3 alloy=twoToneCoat*(.96h+grain*.04h);
    alloy*=1-marks.y*.38h;
    half polish=saturate(_Smoothness+grain*.05h-marks.y*.12h);
    half3 metal=DFSurfaceLighting(i.world,n,alloy,_Metallic,polish,1,half3(0,0,0),i.position);
    half pulse=.94h+.12h*sin(_Time.y*2.8h+(i.pattern.x+i.pattern.y+i.pattern.z)*2.6h);
    half3 lineAccent=lerp(_GlowColor.rgb,_SecondaryColor.rgb,0.35h);
    half3 hotCore=lerp(lineAccent,half3(1,1,1),marks.x*.32h);
    // Preserve original weapon base color underneath: only pattern lines and subtle rim emit strong color
    half3 color=metal+hotCore*((marks.x*2.15h+marks.y*0.35h+rim*0.55h)*pulse);
    half alpha=saturate(lerp(_GlowColor.a,_RimOpacity,rim*0.65h)+marks.x*.82h+marks.z*.22h);
    return half4(color,alpha);
   }
   ENDHLSL
  }
 }
}
