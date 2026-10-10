Shader "Hidden/DimensionsFront/MenuCosmosMotion"
{
    Properties
    {
        _MainTex ("Menu artwork", 2D) = "black" {}
        _NoiseTex ("Cloud tile", 2D) = "gray" {}
        _AnimationTime ("Menu clock", Float) = 0
        _MotionStrength ("Motion strength", Range(0,1)) = 1
        _ReferenceCrop ("Artwork reference framing", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2D(_MainTex);
            UNITY_DECLARE_TEX2D(_NoiseTex);
            float _AnimationTime, _MotionStrength;
            float4 _ReferenceCrop;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                return o;
            }

            // Local masks move subjects gently inside the existing painting. Soft
            // boundaries keep the surrounding city and sky steady without hard cutout edges.
            float SubjectMask(float2 uv,float2 center,float2 radius)
            {
                float2 p=(uv-center)/radius;
                return 1.0-smoothstep(.48,1.0,dot(p,p));
            }

            float CellPhase(float2 uv)
            {
                float2 cell=floor(uv*float2(140.0,80.0));
                return frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453);
            }

            float4 frag(v2f i) : SV_Target
            {
                float t=_AnimationTime;
                float strength=_MotionStrength;
                float2 uv=(i.uv-_ReferenceCrop.xy)/_ReferenceCrop.zw;
                float planet=SubjectMask(uv,float2(.61,.64),float2(.25,.46))*smoothstep(.44,.50,uv.y);
                float ship=SubjectMask(uv,float2(.875,.59),float2(.15,.09));

                float2 planetDrift=float2(sin(t*.065)*.0016,cos(t*.051)*.0010);
                float2 shipDrift=float2(sin(t*.20+.4)*.008,cos(t*.17)*.003);
                float2 cloudFlow=float2(sin(uv.y*14.0+t*.045)*.0012,
                    cos(uv.x*12.0+t*.035)*.0007);
                float2 offset=(planetDrift+cloudFlow)*planet*(1.0-ship)+shipDrift*ship;
                float2 sourceUV=saturate(i.uv+offset*_ReferenceCrop.zw*strength);
                float3 color=UNITY_SAMPLE_TEX2D(_MainTex,sourceUV).rgb;

                // Two cached noise samples provide slow, independently drifting cloud layers.
                float cloudA=UNITY_SAMPLE_TEX2D(_NoiseTex,uv*float2(1.7,1.3)+float2(t*.004,-t*.001)).r;
                float cloudB=UNITY_SAMPLE_TEX2D(_NoiseTex,uv*float2(2.9,2.1)+float2(-t*.002,t*.0015)).r;
                float sky=smoothstep(.32,.80,uv.y)*(1.0-planet*.85)*(1.0-ship);
                float cloud=smoothstep(.42,.78,cloudA*.65+cloudB*.35);
                color+=float3(.035,.055,.10)*cloud*sky*strength;

                // Lights blink at fixed positions. Their phase varies by small image cell;
                // there are no moving light dots crossing the menu's controls.
                float phase=CellPhase(uv);
                float pulse=.5+.5*sin(t*(.85+phase*.65)+phase*6.2831853);
                float city=1.0-smoothstep(.26,.44,uv.y);
                float warm=saturate((color.r-color.b*.90)*6.0)*smoothstep(.12,.40,color.r);
                color*=1.0-warm*city*(1.0-pulse)*.16*strength;
                color+=float3(.13,.07,.018)*warm*city*pulse*strength;

                float stars=smoothstep(.38,.78,max(color.r,max(color.g,color.b)))*sky;
                color*=1.0-stars*(1.0-pulse)*.10*strength;
                float engines=saturate((color.b-color.r)*7.0)*smoothstep(.26,.65,color.b)*ship;
                float thrust=.5+.5*sin(t*2.1);
                color+=float3(.02,.07,.13)*engines*thrust*strength;
                return float4(saturate(color),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
