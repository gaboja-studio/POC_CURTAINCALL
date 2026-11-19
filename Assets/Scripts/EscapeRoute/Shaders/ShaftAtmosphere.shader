Shader "CurtainCall/Escape/Shaft Atmosphere"
{
    Properties
    {
        _Haze("Upper haze", Color) = (.052,.066,.071,1)
        _DepthColor("Deep atmosphere", Color) = (.007,.010,.014,1)
        _Density("Extinction per metre", Range(0, .2)) = .057
        _Falloff("Upper fade depth (fraction of volume)", Range(.02, 1)) = .19
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Haze, _DepthColor;
                float _Density, _Falloff;
            CBUFFER_END

            struct Input { float3 positionOS : POSITION; };
            struct Output { float4 positionCS : SV_POSITION; };

            Output Vert(Input input)
            {
                Output output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }

            // Integral of smoothstep(0, 1, depth). Its difference gives the optical
            // thickness of the height ramp without marching or sampling a noise texture.
            float IntegratedRamp(float depth)
            {
                float t = saturate(depth);
                return t*t*t*(1-.5*t) + max(0, depth-1);
            }

            half4 Frag(Output input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    const float nearDepth = 1;
                #else
                    const float nearDepth = UNITY_NEAR_CLIP_VALUE;
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, sceneDepth);
                #endif
                float3 nearWS = ComputeWorldSpacePosition(uv, nearDepth, UNITY_MATRIX_I_VP);
                float3 hitWS = ComputeWorldSpacePosition(uv, sceneDepth, UNITY_MATRIX_I_VP);
                float3 segment = hitWS - nearWS;
                float viewLength = length(segment);
                float3 origin = TransformWorldToObject(nearWS);
                float3 ray = TransformWorldToObjectDir(segment / max(viewLength, .0001), false);

                float3 safeRay = lerp(-1, 1, step(0, ray)) * max(abs(ray), 1e-7);
                float3 a = (-.5 - origin) / safeRay;
                float3 b = ( .5 - origin) / safeRay;
                float3 enters = min(a, b), leaves = max(a, b);
                float entry = max(0, max(enters.x, max(enters.y, enters.z)));
                float exitT = min(viewLength, min(leaves.x, min(leaves.y, leaves.z)));
                if (exitT <= entry) return 0;

                float d0 = (.5 - origin.y - ray.y * entry) / _Falloff;
                float d1 = (.5 - origin.y - ray.y * exitT) / _Falloff;
                float meanDensity = abs(d1-d0) > .001
                    ? (IntegratedRamp(d1) - IntegratedRamp(d0)) / (d1-d0)
                    : smoothstep(0, 1, d0);
                float distanceInShaft = exitT-entry;
                float fog = 1 - exp(-_Density * distanceInShaft * saturate(meanDensity));
                half3 shade = lerp(_Haze.rgb, _DepthColor.rgb, smoothstep(.35, 2.7, (d0+d1)*.5));
                // The deep closure and the air in front of it share the same colour.
                // Fade out all residual contrast before the ray reaches that closure.
                float endDepth = .5 - origin.y - ray.y * exitT;
                float concealment = smoothstep(.30, .50, endDepth) * smoothstep(.25, 5, distanceInShaft);
                return half4(lerp(shade, _DepthColor.rgb, concealment), max(fog, concealment));
            }
            ENDHLSL
        }
    }
}
