Shader "CurtainCall/Audience Sector Trial"
{
    Properties
    {
        _Color ("Silhouette", Color) = (.018,.016,.014,1)
        _Reaction ("Direction", Float) = 0
        _Started ("Start", Float) = -100
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Reaction, _Started;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 cheer:TEXCOORD1; float3 boo:TEXCOORD2; float2 phase:TEXCOORD3; };
            struct Varyings { float4 positionCS:SV_POSITION; half fog:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float age = _Time.y - _Started - input.phase.x * (_Reaction > 0 ? .18 : .035);
                float envelope = saturate(age / .22) * saturate((1.7 - age) / .45);
                envelope *= step(0, age) * saturate(abs(_Reaction));
                float motion = envelope;
                float3 pose = _Reaction > 0 ? input.cheer : input.boo;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz + pose * motion);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // Keep people dark even while the actual seating behind them is lit.
                return half4(MixFog(_Color.rgb, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
