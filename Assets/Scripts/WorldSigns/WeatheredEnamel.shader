Shader "CurtainCall/World Signs/Weathered Enamel"
{
    Properties
    {
        _Enamel ("Aged paint", Color) = (0.73, 0.67, 0.52, 1)
        _Oxide ("Exposed oxide", Color) = (0.34, 0.22, 0.14, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Enamel;
                half4 _Oxide;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 seed : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                float3 h = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS);
                o.uv = positionOS.xy + 0.5;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = floor(origin.xz * 2.0) + floor(origin.y * 2.0);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Measure wear in units of panel height so chips stay roughly isotropic.
                float2 uv = i.uv;
                float2 p = uv * float2(2.665, 1);
                float2 edges = min(uv, 1 - uv) * float2(2.665, 1);
                float rim = min(edges.x, edges.y);
                float2 q = p + i.seed;
                float blotch = Noise(q * 3.5);
                float flakes = 0.6 * Noise(q * 19.0) + 0.4 * Noise(q * 63.0);
                float roughRim = 0.012 + 0.19 * blotch * blotch + 0.105 * (flakes - 0.45);
                float aa = max(0.002, fwidth(rim));
                float chips = 1 - smoothstep(roughRim - aa, roughRim + aa, rim);
                float drips = smoothstep(0.52, 0.86, Noise(q * float2(29, 2.1)));
                drips *= smoothstep(0.25, 1.0, uv.y + (blotch - 0.5) * 0.38);
                float grime = saturate(0.17 + 0.30 * blotch + 0.22 * flakes + 0.32 * drips);
                half3 paint = lerp(_Enamel.rgb, _Oxide.rgb, grime);
                float2 inset = abs(edges - float2(0.095, 0.095));
                float bolt = 1 - smoothstep(0.014, 0.021 + aa, length(inset));
                float stripe = (1 - smoothstep(0.002, 0.005 + aa, abs(rim - 0.052))) * (1 - flakes);
                half3 worn = lerp(paint, _Oxide.rgb * (0.50 + flakes), chips);
                worn *= 1 - 0.40 * stripe;
                worn = lerp(worn, _Oxide.rgb * 0.46, bolt);
                return half4(MixFog(worn, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
