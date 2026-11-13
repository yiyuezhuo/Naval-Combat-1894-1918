Shader "Naval/ShipVertexColor"
{
    Properties { _Style ("Style",Float)=0 }
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
            float _Style;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float3 bary:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normal:TEXCOORD0; float4 color:COLOR; float3 bary:TEXCOORD1; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normal=TransformObjectToWorldNormal(v.normalOS);o.color=v.color;o.bary=v.bary;return o; }
            half4 frag(Varyings i):SV_Target
            {
                if(_Style>3.5)return half4(i.color.rgb,1);
                float3 illumination=NavalSurfaceLight(i.normal);
                float lighting=dot(illumination,float3(.2126,.7152,.0722));
                if(_Style<.5)return half4(i.color.rgb*illumination,1);
                float3 edge=smoothstep(0,fwidth(i.bary)*1.2,i.bary);float inside=min(edge.x,min(edge.y,edge.z));
                if(_Style<1.5){clip(.95-inside);return half4(float3(.1,.12,.15)*illumination,1);}
                if(_Style>2.5)return half4(lerp(.08,1,inside).xxx*illumination,1);
                float hatch=step(.35,frac((i.positionCS.x+i.positionCS.y)*.22));
                float gray=lighting*lerp(.75,1,hatch);return half4(lerp(.12*lighting,gray,inside).xxx,1);
            }
            ENDHLSL
        }
    }
}
