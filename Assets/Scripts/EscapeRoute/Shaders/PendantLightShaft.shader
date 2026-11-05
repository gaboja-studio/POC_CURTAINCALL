Shader "CurtainCall/Escape/Pendant Light Shaft"
{
    Properties { _Tint ("Dust in lamplight", Color) = (1,0.80,0.48,0.14) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Vertex { float3 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Output { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half depth : TEXCOORD2; };
            Output Vert(Vertex v)
            {
                Output o;
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.depth = saturate(-v.positionOS.y);
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half facing = abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS)));
                half edge = smoothstep(0.0h, 1.0h, facing);
                half fade = (1.0h - smoothstep(0.42h, 1.0h, i.depth)) / (1.0h + 9.0h * i.depth);
                return half4(_Tint.rgb, _Tint.a * edge * fade);
            }
            ENDHLSL
        }
    }
}
