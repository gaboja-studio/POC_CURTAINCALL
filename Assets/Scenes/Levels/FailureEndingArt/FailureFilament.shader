Shader "CurtainCall/Endings/Filament"
{
    Properties { [HDR] _EmissionColor("Tungsten glow",Color)=(7,3,.84,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor;
            CBUFFER_END
            float4 Vert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag():SV_Target { return half4(_EmissionColor.rgb,1); }
            ENDHLSL
        }
    }
}
