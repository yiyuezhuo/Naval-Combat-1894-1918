Shader "Naval/Ocean"
{
    Properties { _WaveTime("Wave time",Float)=0 _SeaState("Sea state",Float)=3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Sunlight.hlsl"
            float _WaveTime,_SeaState;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            Varyings vert(Attributes v){Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            // Independent wave directions/wavelengths avoid the visible square cells of
            // differentiated value noise. Filter each wave before lighting, not afterwards.
            float2 OceanSlope(float2 p)
            {
                float2 slope=0;
                float warp=1.7*sin(dot(p,float2(.013,.009))-_WaveTime*.19)
                    +1.2*sin(dot(p,float2(-.007,.017))+_WaveTime*.13);
                [unroll] for(int k=0;k<16;k++)
                {
                    float angle=k*2.39996323+.37;
                    float2 direction=float2(cos(angle),sin(angle));
                    float frequency=.055*pow(1.255,(float)k);
                    float phase=dot(p,direction)*frequency+warp+ k*7.131
                        -_WaveTime*sqrt(9.81*frequency);
                    float footprint=abs(ddx(phase))+abs(ddy(phase));
                    float visible=1-smoothstep(.35,1.5,footprint);
                    slope+=direction*cos(phase)*(.043*visible);
                }
                return slope;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;
                float rough=lerp(.35,1.3,saturate(_SeaState/8));
                float detailFade=1-smoothstep(1200,6500,distance(_WorldSpaceCameraPos,i.world));
                float2 slope=OceanSlope(p)*detailFade;
                float3 n=normalize(float3(-slope.x*rough,1,-slope.y*rough));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float facing=saturate(dot(n,view));float fresnel=.025+.975*pow(1-facing,5);
                float3 deep=float3(.04,.135,.16)*(_NavalAmbient.rgb+_NavalSunColor.rgb*.5);
                float3 reflection=NavalSky(reflect(-view,n),false);
                float3 sun=_NavalSunDirection;float3 h=normalize(view+sun);
                float highlight=pow(saturate(dot(n,h)),160)*.48;
                float shimmer=pow(saturate(dot(n,h)),18)*.07;
                float3 color=lerp(deep,reflection,fresnel)+_NavalSunColor.rgb*(highlight+shimmer);
                float haze=pow(1-saturate(view.y),8)*.65;
                return half4(lerp(color,NavalSky(normalize(float3(-view.x,.01,-view.z)),false),haze),1);
            }
            ENDHLSL
        }
    }
}
