Shader "CurtainCall/Performance/Focused Beam"
{
    Properties { _Tint("빛기둥 색과 농도",Color)=(1,.9,.8,.027) }
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
            struct Input { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Output Vert(Input v) { Output o; o.positionWS=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; return o; }
            half4 Frag(Output i):SV_Target
            {
                half facing=saturate(abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS))));
                half edge=smoothstep(.03h,.32h,facing);
                half ends=smoothstep(0.0h,.025h,i.uv.y)*(1-smoothstep(.88h,1.0h,i.uv.y));
                return half4(_Tint.rgb,_Tint.a*edge*ends*lerp(1.0h,.55h,i.uv.y));
            }
            ENDHLSL
        }
    }
}
