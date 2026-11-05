Shader "CurtainCall/Escape/Meadow"
{
    Properties { _BaseColor("Tint", Color)=(.68,.53,.28,1) _Wind("Wind", Range(0,1))=.22 _Ground("Ground", Range(0,1))=0 _PlaybackSpeed("Playback speed", Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; float _Wind; float _Ground; float _PlaybackSpeed;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;};
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Soil(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;float3 p=TransformObjectToWorld(v.positionOS.xyz);
                float time=_Time.y*_PlaybackSpeed;
                float gust=sin(p.x*.085+p.z*.05-time*.67)*.5+.5;
                float wave=sin(time*.85+p.x*.21+p.z*.17)*.55+sin(time*1.21+p.z*.4)*.2;
                p.xz+=float2(1,.38)*(wave+gust*gust*1.2)*_Wind*v.uv.y*v.uv.y;
                o.positionCS=TransformWorldToHClip(p);o.world=p;o.normal=TransformObjectToWorldNormal(v.normalOS);o.color=v.color;
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                Light light=GetMainLight();half diffuse=abs(dot(normalize(lerp(i.normal,float3(0,1,0),_Ground)),light.direction));
                half noise=.70+.28*Soil(i.world.xz*.42)+.16*Soil(i.world.xz*5.3);
                half3 base=_BaseColor.rgb*lerp(i.color.rgb,half3(1,1,1),_Ground)*lerp(1,noise,_Ground);
                half3 lit=base*(half3(.40,.43,.39)+light.color*(.35+.45*diffuse));
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
    }
}
