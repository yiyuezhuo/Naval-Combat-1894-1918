Shader "Naval/GunneryParticle"
{
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
            float _Emissive;
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float height:TEXCOORD1; };
            Varyings vert(Attributes v){Varyings o;float3 world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(world);o.height=world.y;o.color=v.color;o.uv=v.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                clip(i.height);
                float2 p=i.uv*2-1;
                float r=dot(p,p);
                float noise=.82+.18*sin(p.x*15+sin(p.y*11))*sin(p.y*17-p.x*7);
                float alpha=pow(saturate(1-r),2)*noise;
                return half4(i.color.rgb*lerp(NavalSurfaceLight(float3(0,1,0)),float3(1,1,1),_Emissive),i.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
