Shader "CurtainCall/Performance/Focused Beam"
{
    Properties { _Tint("원뿔 빛 색과 농도",Color)=(1,.9,.8,.027) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Input { float3 positionOS:POSITION; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            Output Vert(Input v) { Output o; o.positionWS=TransformObjectToWorld(v.positionOS); o.positionCS=TransformWorldToHClip(o.positionWS); return o; }
            // Closed unit cone: apex z=0, radius=z, end z=1. Intersect its volume,
            // rather than tinting the shell (which appears as a flat, constant-width tube).
            bool ConeInterval(float3 o,float3 d,out float nearT,out float farT)
            {
                nearT=0;farT=1e8;
                if(abs(d.z)<1e-8) { if(o.z<=0 || o.z>=1)return false; }
                else { float a=-o.z/d.z,b=(1-o.z)/d.z;nearT=max(0,min(a,b));farT=max(a,b); }
                float A=dot(d.xy,d.xy)-d.z*d.z;
                float B=2*(dot(o.xy,d.xy)-o.z*d.z);
                float C=dot(o.xy,o.xy)-o.z*o.z;
                if(abs(A)<1e-8)
                {
                    if(abs(B)<1e-8) { if(C>0)return false; }
                    else if(B>0)farT=min(farT,-C/B);else nearT=max(nearT,-C/B);
                }
                else
                {
                    float disc=B*B-4*A*C;
                    if(disc<0) { if(A>0)return false; }
                    else
                    {
                        float s=sqrt(disc),a=(-B-s)/(2*A),b=(-B+s)/(2*A);
                        float low=min(a,b),high=max(a,b);
                        if(A>0){nearT=max(nearT,low);farT=min(farT,high);}
                        else if(nearT<low)farT=min(farT,low);else nearT=max(nearT,high);
                    }
                }
                return farT>nearT;
            }
            half4 Frag(Output i):SV_Target
            {
                float3 rayWS=normalize(i.positionWS-GetCameraPositionWS());
                float3 origin=TransformWorldToObject(GetCameraPositionWS());
                float3 ray=TransformWorldToObjectDir(rayWS,false);
                float entry,exitT;
                if(!ConeInterval(origin,ray,entry,exitT))return 0;
                float rawDepth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                float sceneT=LinearEyeDepth(rawDepth,_ZBufferParams)/max(.001,-mul((float3x3)UNITY_MATRIX_V,rayWS).z);
                exitT=min(exitT,sceneT);
                if(exitT<=entry)return 0;
                const int steps=16;
                float stepLength=(exitT-entry)/steps,density=0;
                [unroll]for(int j=0;j<steps;j++)
                {
                    float3 p=origin+ray*(entry+(j+.5)*stepLength);
                    float radial=saturate(1-length(p.xy)/max(p.z,1e-5));
                    density+=smoothstep(0,.45,radial)*smoothstep(0,.015,p.z)*(1-smoothstep(.94,1,p.z));
                }
                float endRadius=length(TransformObjectToWorldDir(float3(1,0,0),false));
                float opacity=1-exp(-density*stepLength/max(.01,endRadius*2));
                return half4(_Tint.rgb,_Tint.a*opacity);
            }
            ENDHLSL
        }
    }
}
