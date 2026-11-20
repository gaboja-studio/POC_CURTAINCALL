Shader "CurtainCall/Escape/Dawn Backdrop"
{
    // 탈출문 밖 새벽 하늘 배경판. 깊이를 써서 뒤쪽 어두운 공장 배경벽을 가리고, 실내 안개는 받지 않는다.
    Properties
    {
        _HorizonColor ("Dawn horizon", Color) = (0.96,0.80,0.64,1)
        _SkyColor ("Upper morning sky", Color) = (0.56,0.72,0.92,1)
        _HorizonHeight ("Horizon world height", Float) = 24
        _FadeHeight ("Gradient height", Float) = 45
        _SunGlow ("Sun glow", Color) = (1,0.88,0.70,0.55)
        _SunPoint ("Sun world point", Vector) = (-652,34,40,0)
        _SunRadius ("Sun glow radius", Float) = 38
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor, _SkyColor, _SunGlow;
                float4 _SunPoint;
                float _HorizonHeight, _FadeHeight, _SunRadius;
            CBUFFER_END
            struct Vertex { float3 positionOS : POSITION; };
            struct Output { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Output Vert(Vertex v)
            {
                Output o;
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 a = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(a), Hash(a + float2(1, 0)), f.x), lerp(Hash(a + float2(0, 1)), Hash(a + 1), f.x), f.y);
            }
            half4 Frag(Output i) : SV_Target
            {
                float height = saturate((i.positionWS.y - _HorizonHeight) / max(1, _FadeHeight));
                float t = smoothstep(0, 1, height);
                float2 p = float2(i.positionWS.z, i.positionWS.y * 2.6) * 0.035;
                float clouds = Noise(p) * 0.6 + Noise(p * 2.1) * 0.3 + Noise(p * 4.3) * 0.1;
                half3 color = lerp(_HorizonColor.rgb, _SkyColor.rgb * (0.9 + 0.18 * clouds), t);
                float sun = saturate(1 - distance(i.positionWS, _SunPoint.xyz) / max(1, _SunRadius));
                color += _SunGlow.rgb * _SunGlow.a * sun * sun;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
