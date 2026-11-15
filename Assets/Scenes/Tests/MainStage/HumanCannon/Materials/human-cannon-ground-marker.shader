Shader "CurtainCall/HumanCannon/GroundMarker"
{
    // 원형 착지 그림자(Task-20261113-126). _Blur 0 = 선명한 원, 1 = 완전히 번져 보이지 않음.
    // _RimColor는 외곽선 색(알파 = 투명도), _OutlineWidth는 반지름 1 기준 외곽선 폭. 알파 0이면 외곽선 없음.
    // _ClipMode 1: 착지면(박스 윗면) 밖은 그리지 않고, 원과 착지면이 겹친 모양의 경계를 따라 외곽선을 그린다(좁은 면).
    // _ClipMode 2: 원 모양은 그대로 두고 착지면 밖의 그림자·외곽선만 _ClipOutsideAlpha 배율로 옅게 그린다(넓은 면).
    Properties
    {
        _RimColor ("외곽선 색 (실행 중 공통 설정 값으로 덮어씀)", Color) = (0.85, 0.94, 1, 1)
        _FillColor ("그림자 바탕색·투명도", Color) = (0.015, 0.02, 0.025, 0.4)
        _OutlineWidth ("외곽선 폭 (반지름 1 기준, 실행 중 덮어씀)", Range(0, 0.96)) = 0.17
        _Blur ("번짐 (0 선명, 1 사라짐, 실행 중 덮어씀)", Range(0, 1)) = 0
        _ClipMode ("착지면 처리 0 없음 1 자르기 2 바깥 옅게 (실행 중 덮어씀)", Float) = 0
        _ClipOutsideAlpha ("착지면 밖 불투명도 배율 (실행 중 덮어씀)", Range(0, 1)) = 0.25
        _ClipSize ("착지면 반폭 x·z(m), 그림자 반지름(m) (실행 중 덮어씀)", Vector) = (1, 1, 1, 0)
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
                half _OutlineWidth;
                half _Blur;
                half _ClipMode;
                half _ClipOutsideAlpha;
                float4 _ClipSize;
                float4x4 _ClipMatrix; // 월드 → 착지면 정규 좌표(x·z가 -1~1이면 면 안)
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                const float outer = 0.96;
                // inside: 그림자 모양 경계까지의 안쪽 거리(반지름 1 기준). 원 경계와 착지면 경계 중 가까운 쪽.
                float inside = outer - length(input.uv * 2 - 1);
                float outsideFade = 1;
                if (_ClipMode > 0.5)
                {
                    float3 p = mul(_ClipMatrix, float4(input.positionWS, 1)).xyz;
                    float meters = min((1 - abs(p.x)) * _ClipSize.x, (1 - abs(p.z)) * _ClipSize.y);
                    float surfaceIn = meters / max(_ClipSize.z, 1e-4);
                    if (_ClipMode > 1.5)
                    {
                        float surfaceEdge = max(fwidth(surfaceIn), 0.008);
                        outsideFade = lerp(saturate(_ClipOutsideAlpha), 1, smoothstep(-surfaceEdge, surfaceEdge, surfaceIn));
                    }
                    else inside = min(inside, surfaceIn);
                }
                float edge = max(fwidth(inside), 0.008);
                float blur = saturate(_Blur);
                // 번질수록 가장자리가 중심까지 부드럽게 퍼지고 전체가 옅어진다.
                float soft = max(edge, blur * 0.9);
                float coverage = smoothstep(-edge, soft, inside);
                float fillAlpha = _FillColor.a * coverage * (1 - blur) * outsideFade;
                float width = clamp(_OutlineWidth, 0, outer);
                float ring = (1 - smoothstep(width - edge, width + edge, inside))
                    * smoothstep(-edge, edge, inside) * saturate(width / edge);
                float rimAlpha = _RimColor.a * ring * (1 - saturate(blur * 4)) * outsideFade;
                float alpha = rimAlpha + fillAlpha * (1 - rimAlpha);
                half3 color = (_RimColor.rgb * rimAlpha + _FillColor.rgb * fillAlpha * (1 - rimAlpha)) / max(alpha, 1e-4);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
