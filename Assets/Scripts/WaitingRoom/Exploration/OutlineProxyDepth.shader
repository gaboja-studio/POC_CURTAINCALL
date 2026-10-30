// 색은 그리지 않고 깊이만 남기는 외곽선 기준판. 벽면과 같은 면의 탐색 대상(금 간 벽·문)은
// 뒤집힌 껍질 외곽선이 벽 깊이에 가려 점선이 된다. 이 판을 대상 앞 몇 mm에 두면 외곽선이 판 둘레 한 줄로 그려진다.
// 불투명 이후(Geometry+1), 상호작용 외곽선(Geometry+2) 전에 그린다.
Shader "CurtainCall/WaitingRoom/OutlineProxyDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "OutlineProxyDepth"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
