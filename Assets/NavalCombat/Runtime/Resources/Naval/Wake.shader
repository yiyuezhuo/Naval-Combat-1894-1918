Shader "Naval/Wake"
{
    Properties { _WaveTime("Wave time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Sunlight.hlsl"
            float _WaveTime;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;float4 color:COLOR; };
            Varyings vert(Attributes v){Varyings o;float3 w=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(w);o.world=w.xz;o.uv=v.uv;o.color=v.color;return o;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                float across=abs(i.uv.x*2-1);
                float noise=.25+.5*Noise(i.world*.38+float2(_WaveTime*.08,0))+.25*Noise(i.world*1.1-float2(0,_WaveTime*.12));
                float center=pow(saturate(1-across),1.5);
                float edges=exp(-pow((across-.76)*9,2))*.55;
                float edgeFade=1-smoothstep(.85,1,across);
                float foam=(center*.75+edges)*edgeFade*saturate(noise+.25);
                return half4(lerp(float3(.38,.65,.67),float3(.92,.98,.96),noise)*NavalSurfaceLight(float3(0,1,0)),foam*i.color.a);
            }
            ENDHLSL
        }
    }
}
