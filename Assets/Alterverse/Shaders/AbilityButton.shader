Shader "DimensionsFront/UI/AbilityButton"
{
    Properties
    {
        [PerRendererData] _MainTex ("Ability atlas", 2D) = "black" {}
        _Cell ("Atlas rectangle", Vector) = (0,0,1,1)
        _Accent ("Hero accent", Color) = (1,1,1,1)
        _Cooldown ("Cooldown fraction", Range(0,1)) = 0
        _Unavailable ("Unavailable", Range(0,1)) = 0
        _Held ("Held", Range(0,1)) = 0
        _Press ("Press impulse", Range(0,1)) = 0
        _Ultimate ("Ultimate", Range(0,1)) = 0
        _Clock ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float4 _Cell;
            fixed4 _Accent;
            float _Cooldown, _Unavailable, _Held, _Press, _Ultimate, _Clock;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv-.5;
                float radius=length(p);
                float aa=max(fwidth(radius),.001);
                // Icon art is clipped to a circle. Ultimate ornaments are a separate bounded technical frame.
                fixed3 art=tex2D(_MainTex,_Cell.xy+i.uv*_Cell.zw).rgb;
                float luma=dot(art,float3(.2126,.7152,.0722));
                float face=1-smoothstep(.458-aa,.458+aa,radius);
                float luminous=smoothstep(.025,.12,max(art.r,max(art.g,art.b)));
                float outline=1-smoothstep(.492-aa,.492+aa,radius);
                float alpha=max(face,luminous*outline);
                float frameAngle=atan2(p.y,p.x);
                float segmented=step(.24,abs(sin(frameAngle*6)));
                float outerRing=1-smoothstep(.013,.023,abs(radius-.467));
                float innerRing=1-smoothstep(.006,.012,abs(radius-.431));
                float diamond=min(abs(abs(p.x)-.465)+abs(p.y),abs(abs(p.y)-.465)+abs(p.x));
                float tips=1-smoothstep(.014,.029,diamond);
                float trim=max(max(outerRing*segmented,innerRing),tips)*_Ultimate;
                if(_Ultimate>.5)
                {
                    alpha=face;
                    art*=1-smoothstep(.419,.441,radius);
                    art+=_Accent.rgb*trim*1.25;
                    alpha=max(alpha,trim);
                }
                art=lerp(art,luma.xxx*.55,_Unavailable);
                float cooling=step(.0001,_Cooldown);
                art*=lerp(1,.62,cooling);
                // Clockwise sector begins at twelve o'clock. As remaining time falls the dark sector clears.
                float angle=frac(atan2(p.x,p.y)/6.2831853+1);
                float sector=step(angle,_Cooldown)*cooling;
                art*=1-sector*face*.52;
                float arc=(1-smoothstep(.007,.014,abs(radius-.438)))*sector;
                art+=_Accent.rgb*arc*.7*(1-_Unavailable);
                float pulse=.5+.5*sin(_Clock*6);
                float ring=1-smoothstep(.004,.016,abs(radius-.475));
                art+=lerp(_Accent.rgb,fixed3(1,1,1),_Press*.65)*ring*
                    (_Held*(.4+.6*pulse)+_Press*.8)*(1-_Unavailable)*(1-cooling*.65);
                art*=1+_Press*.18;
                alpha=max(alpha,ring*max(_Held,_Press));
                return fixed4(art,alpha)*i.color;
            }
            ENDCG
        }
    }
}
