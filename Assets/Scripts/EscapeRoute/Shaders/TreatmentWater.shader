Shader "CurtainCall/Escape/TreatmentWater"
{
    Properties
    {
        [NoScaleOffset] _FlowNoise("Waterfall mask", 2D) = "gray" {}
        _UVPan("Flow direction and rate", Vector) = (0, .145, 0, 0)
        [Toggle] _Pour("Aerated falling water", Float) = 0
        _Impact("Outfall centres and half widths along channel", Vector) = (0, 1, 0, 1)
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
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FlowNoise);
            SAMPLER(sampler_FlowNoise);
            CBUFFER_START(UnityPerMaterial)
                float4 _UVPan, _Impact;
                float _Pour;
            CBUFFER_END
            struct Input
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 jet : TEXCOORD1;
            };
            struct Interpolator
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float2 localXZ : TEXCOORD3;
                float fog : TEXCOORD4;
                float drop : TEXCOORD5;
            };

            float CellRandom(int2 cell)
            {
                uint h = asuint(cell.x)*1597334677u ^ asuint(cell.y)*3812015801u;
                h ^= h >> 16;
                h *= 2246822519u;
                return (h >> 8) * (1.0/16777216.0);
            }

            float Noise(float2 p)
            {
                float2 u = frac(p);
                u = u*u*(3-2*u);
                int2 cell = (int2)floor(p);
                float4 hashes = float4(CellRandom(cell), CellRandom(cell+int2(1,0)),
                                       CellRandom(cell+int2(0,1)), CellRandom(cell+int2(1,1)));
                return lerp(lerp(hashes.x,hashes.y,u.x),lerp(hashes.z,hashes.w,u.x),u.y);
            }

            float SuspendedFines(float2 at)
            {
                int2 cell = (int2)floor(at);
                float2 seed = float2(CellRandom(cell), CellRandom(cell+int2(73,117)));
                float2 delta = frac(at) - (.25 + .50 * seed);
                float dist = length(delta * float2(1.35,.82));
                return (1 - smoothstep(.035,.19,dist)) * smoothstep(.65,.93,seed.x);
            }

            float FallPhase(float worldDrop)
            {
                return (sqrt(9 + 19.62*max(0,worldDrop))-3)/9.81 - _Time.y;
            }

            Interpolator Vert(Input v)
            {
                Interpolator o;
                float3 p = v.positionOS;
                float t = v.jet.y;
                float freeSpan = _Pour * saturate(8*t) * saturate(12*(1-t));
                float originY = TransformObjectToWorld(float3(0,0,0)).y;
                float phase = FallPhase(originY - TransformObjectToWorld(p).y);
                // The cross-stream channels are stationary. Waves advance only down the drop.
                p.x += freeSpan * (.12*sin(phase*14)*cos(p.z*2.7) +
                                   .065*v.normalOS.x*sin(phase*24)*sin(p.z*3.9));
                float3 world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(world);
                o.localXZ = p.xz * float2(length(TransformObjectToWorldDir(float3(1,0,0), false)),
                                           length(TransformObjectToWorldDir(float3(0,0,1), false)));
                if (_Pour > .5)
                {
                    o.localXZ = float2(o.localXZ.y, originY-world.y);
                    o.localXZ.x += dot(TransformObjectToWorld(float3(0,0,0)).xz,float2(.13,.27));
                }
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.drop = t;
                return o;
            }
            half4 Frag(Interpolator i) : SV_Target
            {
                float time = _Time.y;
                float phase = FallPhase(i.localXZ.y);
                float2 uv = _Pour > .5 ? float2(i.localXZ.x*.48, phase*2.1) : i.localXZ*.48 + time*_UVPan.xy;
                half rim = 1 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half broad = SAMPLE_TEXTURE2D(_FlowNoise, sampler_FlowNoise, uv * float2(.83, .67)).r;
                half detail = SAMPLE_TEXTURE2D(_FlowNoise, sampler_FlowNoise, uv * float2(2.51, 1.21) + float2(.31, .74)).r;
                half3 color;
                half opacity;
                if (_Pour > .5)
                {
                    // Equal intervals in travel time stretch apart as the discharge accelerates.
                    float2 flow = float2(i.localXZ.x, phase*7.9);
                    half eddy = Noise(flow*float2(.64,.42));
                    float crossFlow = flow.x + .91*(eddy-.5) + .18*sin(flow.y*.9 + eddy*5);
                    half sediment = Noise(float2(crossFlow*1.45, flow.y*.69 + eddy));
                    half ripple = Noise(float2(crossFlow*4.2 + sediment*2.3, flow.y*2.1));
                    half foam = smoothstep(.59,.79,.56*ripple+.32*sediment+.12*detail);
                    half surge = saturate(cos(phase*18 + 1.4*eddy + sin(flow.x*.65)));
                    foam = saturate(foam + surge*surge*surge * smoothstep(.49,.78,sediment) * .62);
                    half grit = SuspendedFines(float2(crossFlow*8 + eddy*2,flow.y*4.8)) * sediment;
                    half directional = saturate(dot(normalize(i.normalWS),normalize(half3(-.60,.74,.31))));
                    // Long iron-brown streaks and suspended olive sludge travel with the water.
                    half sludge = saturate(.64*sediment + .29*eddy + .16*ripple);
                    color = lerp(half3(.042,.020,.011), half3(.134,.109,.044), sludge) * (.61+.41*directional+.17*ripple);
                    color = lerp(color, half3(.32,.248,.133), foam*(.45+.48*eddy));
                    half glint = smoothstep(.64,.87,ripple) * smoothstep(.35,.78,eddy);
                    color += half3(.085,.089,.09) * (glint + rim*rim*.22) * (.4+.6*broad);
                    color = lerp(color, half3(.021,.014,.008), saturate(grit*1.5));
                    color *= .78 + .22*smoothstep(0,.09,i.drop);
                    // Thin runs let silhouettes show through between the dense flocs.
                    opacity = .36 + .34*sludge + .17*foam + .04*directional;
                }
                else
                {
                    float bankDistance = min(max(0,abs(i.localXZ.y-_Impact.x)-_Impact.y),
                                             max(0,abs(i.localXZ.y-_Impact.z)-_Impact.w));
                    float reach = length(float2(i.localXZ.x+.18, bankDistance));
                    float inlet = saturate(1 - reach/1.7);
                    float rings = .5 + .5 * sin(reach*16 - time*3.8 + broad*1.5);
                    float wake = inlet*inlet*(.26 + .74*rings);
                    half sheen = saturate(.15 + .35*rim*rim + .24*broad + .12*detail);
                    half scum = smoothstep(.45,.69,.65*broad+.35*detail);
                    color = lerp(half3(.036,.022,.009), half3(.105,.102,.034), saturate(sheen + .40*scum));
                    color = lerp(color, half3(.32,.249,.140), wake*(.36 + .64*detail));
                    opacity = .95;
                }
                return half4(MixFog(color, i.fog), opacity);
            }
            ENDHLSL
        }
    }
}
