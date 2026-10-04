Shader "DimensionsFront/Nerok/WornSilverAnchor"
{
    Properties
    {
        _SilverColor("Silver steel", Color) = (.68,.71,.74,1)
        _RustColor("Rust traces", Color) = (.32,.105,.028,1)
        _UseRestCoordinates("Use skin rest coordinates", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Back ZWrite On
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _SilverColor, _RustColor;
                float _UseRestCoordinates;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 rest : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 rest : TEXCOORD2;
                float fog : TEXCOORD3;
            };
            float Hash(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(cell),Hash(cell+float3(1,0,0)),f.x),
                                 lerp(Hash(cell+float3(0,1,0)),Hash(cell+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(Hash(cell+float3(0,0,1)),Hash(cell+float3(1,0,1)),f.x),
                                 lerp(Hash(cell+float3(0,1,1)),Hash(cell+1),f.x),f.y),f.z);
            }
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.rest = lerp(i.positionOS.xyz, i.rest, _UseRestCoordinates);
                o.fog = ComputeFogFactor(o.positionHCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float patches = Noise(i.rest*19.0 + float3(8,13,3));
                float grain = Noise(i.rest*105.0);
                // Localized irregular oxidation; most of the anchor remains exposed silver.
                half rust = smoothstep(.59,.77,patches*.82 + grain*.18);
                half3 color = lerp(_SilverColor.rgb*(.90+.10*grain), _RustColor.rgb*(.65+.65*grain), rust);
                half3 n = normalize(i.normalWS);
                half3 lightDir = normalize(half3(-.38,.84,.39));
                half3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half diffuse = .40 + .60*saturate(dot(n,lightDir));
                half highlight = pow(saturate(dot(n,normalize(lightDir+viewDir))),52.0)*(1-rust)*.7;
                half rim = pow(1-saturate(abs(dot(n,viewDir))),3.0)*.12*(1-rust);
                return half4(MixFog(color*diffuse+highlight+rim,i.fog),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
