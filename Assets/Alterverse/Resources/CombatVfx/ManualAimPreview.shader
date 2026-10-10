Shader "DimensionsFront/ManualAimPreview"
{
    Properties
    {
        _Color ("Preview color", Color) = (1,.94,.56,.22)
        _Mode ("Circle / cone / line", Float) = 0
        _ConeCos ("Cone half angle cosine", Float) = .707
        _Width ("Normalized line width", Float) = .02
        _Opacity ("Preview opacity multiplier", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+40" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1,-1
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Mode, _ConeCos, _Width, _Opacity;
            CBUFFER_END
            Varyings vert(Attributes v)
            { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - .5;
                float r = length(p);
                float aa = max(fwidth(r), .001);
                float mask = 1 - smoothstep(.5-aa, .5, r);
                float edge = 1 - smoothstep(aa, aa*3, abs(r-.495));
                if (_Mode > 1.5)
                {
                    float side = abs(p.x);
                    mask = (1-smoothstep(_Width*.5, _Width*.5+aa, side))*step(0,p.y);
                    edge = 1;
                }
                else if (_Mode > .5)
                {
                    float alignment = p.y / max(r,.00001);
                    float angularAA = max(fwidth(alignment),.001);
                    mask *= smoothstep(_ConeCos-angularAA,_ConeCos+angularAA,alignment);
                    edge = max(edge,1-smoothstep(angularAA,angularAA*3,abs(alignment-_ConeCos)));
                }
                half4 c = _Color;
                c.a = mask * lerp(c.a,min(1,c.a+ .55),edge) * saturate(_Opacity);
                return c;
            }
            ENDHLSL
        }
    }
}
