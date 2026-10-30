Shader "DimensionsFront/CosmicMenu"
{
    Properties
    {
        [NoScaleOffset] _Panorama("Authored space background (sRGB)",2D)="black"{}
        [NoScaleOffset] _CosmicNoise("Periodic cloud data (linear)",2D)="gray"{}
        _ImageAspect("Image aspect",Float)=2
        _FrameAspect("Frame aspect",Float)=2
        _CosmicParams("Exposure, seed, clock, cloud strength",Vector)=(.8,11,0,.018)
        _DustColor("Cloud tint",Color)=(.4,.6,1,1)
        _MeteorA("First streak: x, y, visibility, length",Vector)=(0,0,0,.15)
        _MeteorB("Second streak: x, y, visibility, length",Vector)=(0,0,0,.15)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Panorama);SAMPLER(sampler_Panorama);
        TEXTURE2D(_CosmicNoise);SAMPLER(sampler_CosmicNoise);
        CBUFFER_START(UnityPerMaterial)
        float _ImageAspect,_FrameAspect;
        float4 _CosmicParams,_DustColor,_MeteorA,_MeteorB;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
        struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
            output.uv=input.uv;
            return output;
        }
        float Hash21(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        half Meteor(float2 screen,float4 state)
        {
            // Screen-height units keep the width/length stable across phone aspect ratios.
            float2 delta=(screen-state.xy)*float2(_FrameAspect,1);
            const float2 direction=float2(.9,-.4358899);
            float along=-dot(delta,direction);
            float across=abs(dot(delta,float2(-direction.y,direction.x)));
            float aa=max(fwidth(across),.00045);
            float tail=saturate(1-along/max(state.w,.01))*step(0,along);
            float core=1-smoothstep(.00065,.00065+aa,across);
            float halo=1/(1+across*across*700000);
            float head=saturate(1-length(delta)/(.0015+aa));
            return (tail*tail*(core*.65+halo*.19)+head*.55)*state.z;
        }
        half4 Cosmos(float2 screen)
        {
            float2 crop=float2(min(1,_FrameAspect/max(_ImageAspect,.001)),
                min(1,_ImageAspect/max(_FrameAspect,.001)));
            float2 uv=(screen-.5)*crop+.5;
            half3 color=SAMPLE_TEXTURE2D(_Panorama,sampler_Panorama,saturate(uv)).rgb*_CosmicParams.x;
            // A faint moving cloud veil leaves the authored planets and star positions fixed.
            float time=_CosmicParams.z;
            float2 seedOffset=float2(_CosmicParams.y*.173,_CosmicParams.y*.317);
            float n1=SAMPLE_TEXTURE2D(_CosmicNoise,sampler_CosmicNoise,
                screen*float2(2.1,1.2)+seedOffset+float2(time*.0007,-time*.0003)).r;
            float n2=SAMPLE_TEXTURE2D(_CosmicNoise,sampler_CosmicNoise,
                screen*float2(3.7,2.1)-seedOffset+float2(-time*.0004,time*.0005)).g;
            float dustMask=saturate((dot(color,half3(.2126,.7152,.0722))-.012)*6);
            color+=_DustColor.rgb*(n1*n2)*dustMask*_CosmicParams.w;
            // Sparse local glints; no global exposure animation or additional lights.
            float2 cells=screen*float2(_FrameAspect,1)*105;
            float2 cell=floor(cells);
            float random=Hash21(cell+seedOffset);
            float2 spot=frac(cells)-float2(.2+.6*frac(random*23),.2+.6*frac(random*47));
            float glint=saturate(1-length(spot)*25)*step(.995,random);
            color+=half3(.65,.78,1)*glint*(.025+.02*sin(time*.75+random*45));
            half streak=Meteor(screen,_MeteorA)+Meteor(screen,_MeteorB);
            color+=lerp(half3(.72,.84,1),_DustColor.rgb,.20)*streak;
            return half4(saturate(color),1);
        }
        ENDHLSL
        Pass
        {
            Name "Menu"
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragMenu
            half4 FragMenu(Varyings input):SV_Target{return Cosmos(input.uv);}
            ENDHLSL
        }
    }
    Fallback Off
}

