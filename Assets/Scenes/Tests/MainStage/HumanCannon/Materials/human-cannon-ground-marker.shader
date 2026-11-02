Shader "CurtainCall/HumanCannon/GroundMarker"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (0.85, 0.94, 1, 1)
        _FillColor ("Fill Color", Color) = (0.015, 0.02, 0.025, 0.4)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half4 _FillColor;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float distance = length(input.uv * 2 - 1);
                float edge = max(fwidth(distance), 0.008);
                float coverage = 1 - smoothstep(0.96 - edge, 0.96 + edge, distance);
                float rim = smoothstep(0.79 - edge, 0.79 + edge, distance);
                return half4(lerp(_FillColor.rgb, _RimColor.rgb, rim), coverage * lerp(_FillColor.a, 0.9, rim));
            }
            ENDHLSL
        }
    }
}
