Shader "CurtainCall/Pursuit/LocalEye"
{
    Properties
    {
        [PerRendererData] _MainTex ("White sprite", 2D) = "white" {}
        [HDR] _Tint ("State color", Color) = (1,1,1,1)
        _Emission ("Emission", Float) = 0
        _GlowOpacity ("Soft glow", Range(0,.5)) = .12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Emission, _GlowOpacity;
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
            half Coverage(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }
            half4 Frag(Varyings input) : SV_Target
            {
                half core = Coverage(input.uv);
                float2 d = _MainTex_TexelSize.xy * 3;
                half blur = (Coverage(input.uv + float2(d.x,0)) + Coverage(input.uv - float2(d.x,0))
                    + Coverage(input.uv + float2(0,d.y)) + Coverage(input.uv - float2(0,d.y))
                    + Coverage(input.uv + d) + Coverage(input.uv - d)
                    + Coverage(input.uv + float2(d.x,-d.y)) + Coverage(input.uv + float2(-d.x,d.y))) * .125h;
                half halo = blur * saturate(_Emission) * _GlowOpacity * (1-core);
                half alpha = saturate((core + halo) * _Tint.a);
                half3 rgb = _Tint.rgb * (core * (1 + max(0,_Emission)) + halo) * _Tint.a;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
