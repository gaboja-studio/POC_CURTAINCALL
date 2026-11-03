Shader "CurtainCall/Escape/TreatmentWater"
{
    Properties
    {
        [NoScaleOffset] _FlowNoise("Waterfall mask", 2D) = "gray" {}
        _UVPan("Flow direction and rate", Vector) = (0, .145, 0, 0)
        [Toggle] _Pour("Aerated falling water", Float) = 0
        _Impact("Inlet impact in local XZ", Vector) = (0, 0, 0, 0)
        _Weir("Spill center, half width, downstream toe", Vector) = (0, 2, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-30" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FlowNoise);
            SAMPLER(sampler_FlowNoise);
            CBUFFER_START(UnityPerMaterial)
                float4 _UVPan, _Impact, _Weir;
                float _Pour;
            CBUFFER_END
            struct Input
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 jet : TEXCOORD1;
            };
            struct Interpolator
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 localXZ : TEXCOORD3;
                float fog : TEXCOORD4;
                float2 jet : TEXCOORD5;
            };

            Interpolator Vert(Input v)
            {
                Interpolator o;
                float3 p = v.positionOS;
                // Only free-falling water moves laterally. Both wet junctions remain pinned.
                float t = v.jet.y;
                float sway = saturate(v.jet.x) * saturate(8 * t) * saturate(6 * (1 - t));
                p.xz += sway * float2(.054 * sin(11*t - 4*_Time.y), .039 * sin(15*t - 5*_Time.y));
                float3 world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(world);
                o.uv = v.uv;
                o.localXZ = p.xz;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.jet = v.jet;
                return o;
            }
            half4 Frag(Interpolator i) : SV_Target
            {
                float time = _Time.y;
                float2 uv = i.uv + time * _UVPan.xy;
                half rim = 1 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half broad = SAMPLE_TEXTURE2D(_FlowNoise, sampler_FlowNoise, uv * float2(.83, .67)).r;
                half detail = SAMPLE_TEXTURE2D(_FlowNoise, sampler_FlowNoise, uv * float2(2.51, 1.21) + float2(.31, .74)).r;
                half3 color;
                half opacity;
                if (_Pour > .5)
                {
                    half streak = smoothstep(.28, .83, .58*broad + .42*detail);
                    half filaments = smoothstep(.5, .95, sin(31*i.uv.x + 2*broad + .75*sin(5.2*uv.y + 1.7*detail)));
                    half turning = saturate(dot(normalize(i.normalWS), half3(.80,.36,-.47)));
                    half aeration = saturate(.06 + .32*streak + .21*filaments + .16*turning + .32*rim*rim);
                    color = lerp(half3(.02,.039,.031), half3(.40,.47,.405), aeration);
                    opacity = .70 + .18*streak - .33*rim;
                    if (i.jet.x < -.5)
                    {
                        float edge = min(i.jet.y, 1 - i.jet.y);
                        opacity *= (.57 + .43*streak) * smoothstep(0, .045, edge + .015*(detail-.5));
                    }
                }
                else
                {
                    float2 d = i.localXZ - _Impact.xy;
                    float reach = length((d + float2(.10,.25)) * float2(.99, .88));
                    float inlet = saturate(1 - reach/1.7);
                    float rings = .5 + .5 * sin(reach*16 - time*3.8 + broad*1.5);
                    float spill = saturate(1 - abs(i.localXZ.y - _Weir.z)/.53) *
                        saturate((_Weir.y - abs(i.localXZ.x - _Weir.x)) * 2.5);
                    float wake = saturate(inlet*inlet*(.26 + .74*rings) + .65*spill);
                    half sheen = saturate(.15 + .35*rim*rim + .24*broad + .12*detail);
                    color = lerp(half3(.009,.018,.014), half3(.045,.068,.050), sheen);
                    color = lerp(color, half3(.33,.39,.34), wake*(.36 + .64*detail));
                    opacity = .95;
                }
                return half4(MixFog(color, i.fog), opacity);
            }
            ENDHLSL
        }
    }
}

