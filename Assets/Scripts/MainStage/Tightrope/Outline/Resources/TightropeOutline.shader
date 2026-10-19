Shader "CurtainCall/Tightrope/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.035, 0.045, 0.06, 1)
        _OutlinePixels ("Outline Width (pixels)", Range(0, 6)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "TightropeOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 projectedNormal = mul((float3x3)UNITY_MATRIX_P, normalVS).xy;
                float2 pixelNormal = projectedNormal * _ScreenParams.xy;
                pixelNormal *= rsqrt(max(dot(pixelNormal, pixelNormal), 0.000001));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionCS.xy += pixelNormal * (2.0 * _OutlinePixels / _ScreenParams.xy) * output.positionCS.w;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
