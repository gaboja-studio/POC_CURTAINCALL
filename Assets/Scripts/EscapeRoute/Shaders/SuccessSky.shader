Shader "CurtainCall/Endings/Success Overcast Sky"
{
    Properties {_SkyColor("Upper daylight",Color)=(.42,.46,.5,1) _HorizonColor("Dark horizon",Color)=(.035,.045,.055,1)}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque"}
        Cull Front ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _SkyColor,_HorizonColor;
            CBUFFER_END
            struct V{float4 pos:SV_POSITION;float3 ray:TEXCOORD0;};
            V Vert(float4 v:POSITION){V o;o.pos=TransformObjectToHClip(v.xyz);o.ray=v.xyz;return o;}
            float H(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float N(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(H(a),H(a+float2(1,0)),f.x),lerp(H(a+float2(0,1)),H(a+1),f.x),f.y);}
            half4 Frag(V i):SV_Target
            {
                float3 ray=normalize(i.ray);float height=smoothstep(-.015,.35,ray.y);
                float2 p=ray.xz/max(.08,ray.y)*1.5;
                float clouds=N(p)*.6+N(p*2.1)*.3+N(p*4.3)*.1;
                return half4(lerp(_HorizonColor.rgb,_SkyColor.rgb*(.7+.45*clouds),height),1);
            }
            ENDHLSL
        }
    }
}
