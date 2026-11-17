Shader "CurtainCall/Camera/Foreground Dither"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = .5
        _CameraOcclusionCoverage ("Occlusion Coverage", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _SpecColor ("Specular", Color) = (.2, .2, .2, 1)
        _SpecGlossMap ("Specular Map", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = .5
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _Surface ("Surface", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull [_Cull]
        ZWrite On
        Blend Off

        HLSLINCLUDE
        float _CameraOcclusionCoverage;

        // Ordered 8x8 Bayer matrix. The same screen pixels survive in color and depth.
        uint Bayer2(uint2 p) { return ((p.x ^ p.y) << 1) | p.y; }

        void ClipForeground(float2 screenPosition)
        {
            uint2 pixel = uint2(screenPosition);
            uint rank = 16u * Bayer2(pixel & 1u) + 4u * Bayer2((pixel >> 1) & 1u) + Bayer2((pixel >> 2) & 1u);
            clip(_CameraOcclusionCoverage - (rank + .5) / 64.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma editor_sync_compilation
            #pragma vertex LitPassVertex
            #pragma fragment DitherLit
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            half4 DitherLit(Varyings input) : SV_Target
            {
                ClipForeground(input.positionCS.xy);
                half4 color;
                LitPassFragment(input, color);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma editor_sync_compilation
            #pragma vertex DepthOnlyVertex
            #pragma fragment DitherDepth
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            half DitherDepth(Varyings input) : SV_Target
            {
                ClipForeground(input.positionCS.xy);
                return DepthOnlyFragment(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma editor_sync_compilation
            #pragma vertex DepthNormalsVertex
            #pragma fragment DitherNormals
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            half4 DitherNormals(Varyings input) : SV_Target
            {
                ClipForeground(input.positionCS.xy);
                half4 normal;
                DepthNormalsFragment(input, normal);
                return normal;
            }
            ENDHLSL
        }

        // Keep the object's original solid shadow while only the camera view is clipped.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
