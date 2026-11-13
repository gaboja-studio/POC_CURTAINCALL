Shader "Hidden/CurtainCall/SilhouetteOutline"
{
    Properties
    {
        _SilhouetteColor ("Color", Color) = (0,1,1,1)
        _SilhouettePixels ("Width", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Union mask"
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _SilhouetteColor;
                float _SilhouettePixels;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float eye:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);o.eye=-TransformWorldToView(world).z;return o;
            }
            struct Output { float4 color:SV_Target0;float4 info:SV_Target1; };
            Output Frag(Varyings i){Output o;o.color=_SilhouetteColor;o.info=float4(_SilhouettePixels,i.eye,0,1);return o;}
            ENDHLSL
        }
        Pass
        {
            Name "Exterior only"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_X(_SilhouetteMask);
            TEXTURE2D_X(_SilhouetteInfo);
            TEXTURE2D_X_FLOAT(_SilhouetteSceneDepth);
            float4 _SilhouetteTexel;
            float4 _SilhouetteBounds;
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(uint id:SV_VertexID)
            {
                Varyings o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=GetFullScreenTriangleVertexPosition(id);o.uv=GetFullScreenTriangleTexCoord(id);return o;
            }
            float EyeDepth(float2 uv)
            {
                float raw=SAMPLE_TEXTURE2D_X(_SilhouetteSceneDepth,sampler_PointClamp,uv).r;
                if(unity_OrthoParams.w>.5)
                {
                    #if UNITY_REVERSED_Z
                    raw=1-raw;
                    #endif
                    return lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                }
                return LinearEyeDepth(raw,_ZBufferParams);
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(any(i.uv<_SilhouetteBounds.xy-9*_SilhouetteTexel.xy)||any(i.uv>_SilhouetteBounds.zw+9*_SilhouetteTexel.xy))return 0;
                // The complete projected union excludes every internal seam, including overlapping parts.
                if(SAMPLE_TEXTURE2D_X(_SilhouetteInfo,sampler_PointClamp,i.uv).a>.5)return 0;
                float4 best=0;float bestDistance=100;
                float sceneAtPixel=EyeDepth(i.uv);
                [loop] for(int radius=1;radius<=8;radius++)
                {
                    [unroll] for(int direction=0;direction<8;direction++)
                    {
                        float angle=direction*.785398163;
                        float2 uv=i.uv+float2(cos(angle),sin(angle))*radius*_SilhouetteTexel.xy;
                        if(any(uv<0)||any(uv>1))continue;
                        float4 info=SAMPLE_TEXTURE2D_X(_SilhouetteInfo,sampler_PointClamp,uv);
                        if(info.a<.5||radius>info.x+.5||radius>=bestDistance)continue;
                        float tolerance=max(.025,info.y*.001);
                        if(info.y>EyeDepth(uv)+tolerance||info.y>sceneAtPixel+tolerance)continue;
                        best=SAMPLE_TEXTURE2D_X(_SilhouetteMask,sampler_PointClamp,uv);
                        bestDistance=radius;
                    }
                    if(bestDistance<100)break;
                }
                return best;
            }
            ENDHLSL
        }
    }
}
