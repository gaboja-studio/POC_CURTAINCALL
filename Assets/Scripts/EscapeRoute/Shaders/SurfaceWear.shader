Shader "CurtainCall/Escape/SurfaceWear"
{
    Properties
    {
        _Mask("Existing wear mask", 2D) = "black" {}
        _Tint("Wear tint and strength", Color) = (0.25,0.25,0.22,0.5)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            CBUFFER_START(UnityPerMaterial)
                float4 _Mask_ST;
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _Mask);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, input.uv).r;
                float2 edge = smoothstep(0, 0.06, input.uv) * smoothstep(0, 0.06, 1 - input.uv);
                half strength = saturate(mask * _Tint.a * edge.x * edge.y);
                return half4(lerp(half3(1,1,1), _Tint.rgb, strength), 1);
            }
            ENDHLSL
        }
    }
}
