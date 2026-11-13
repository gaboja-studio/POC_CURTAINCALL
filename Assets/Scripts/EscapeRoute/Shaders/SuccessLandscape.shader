Shader "CurtainCall/Endings/Success Landscape"
{
    Properties
    {
        _BaseMap("Dirt / canvas",2D)="white"{}
        _BumpMap("Normal",2D)="bump"{}
        _BaseColor("Tint",Color)=(.4,.35,.26,1)
        _AmbientFill("Overcast environment fill",Color)=(.4,.42,.43,1)
        _DetailScale("World detail scale",Float)=.6
        _Kind("Ground 0 / cloth 1 / foliage 2 / solid 3",Float)=0
        _Wind("Wind",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Cull Off
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_AmbientFill;float _DetailScale,_Kind,_Wind;
            CBUFFER_END
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
            float H(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float N(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(H(a),H(a+float2(1,0)),f.x),lerp(H(a+float2(0,1)),H(a+1),f.x),f.y);}
            V Vert(A v)
            {
                V o;float3 w=TransformObjectToWorld(v.vertex.xyz);
                w.xz+=float2(sin(w.x*.3+w.z*.2+_Time.y*1.15),cos(w.x*.2+_Time.y*.8))*_Wind*v.uv.y*v.uv.y;
                o.world=w;o.pos=TransformWorldToHClip(w);o.normal=TransformObjectToWorldNormal(v.normal);o.uv=v.uv;o.fog=ComputeFogFactor(o.pos.z);return o;
            }
            half4 Frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                float2 p=_Kind<.5?i.world.xz*_DetailScale:i.uv*_DetailScale;
                float grain=N(p*43),coarse=N(p*2.3),fine=N(p*179);
                half3 albedo=_BaseColor.rgb;
                float relief=grain*.025+fine*.004;
                if(_Kind<.5)
                {
                    half3 dirt=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p*.35).rgb;
                    dirt=lerp(dirt,sqrt(max(dirt,.001)),.45);
                    albedo*=dirt*(.7+coarse*.5)*(.65+.6*N(i.world.xz*.15));
                    // Irregular fine fissures, rather than a regular polygon pattern.
                    float fissure=1-smoothstep(.018,.055,abs(N(p*8+N(p*2))-.5));
                    albedo*=1-fissure*.25;
                    relief+=dot(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p*.35).rgb,half3(.2,.3,.5))*.08;
                }
                else if(_Kind<1.5)
                {
                    float damp=smoothstep(.32,.72,N(p*2+19));
                    albedo*=.62+.3*grain+.08*sin(p.x*360)*sin(p.y*360);
                    albedo=lerp(albedo,albedo*half3(.28,.31,.25),damp*.48);
                    relief+=sin(p.x*360)*sin(p.y*360)*.0003;
                }
                else if(_Kind<2.5)
                {
                    albedo*=lerp(.33,1.15,saturate(i.uv.y))*(.8+.3*grain);
                    albedo*=.85+.15*cos(i.uv.x*19);
                }
                else albedo*=.65+.45*grain;
                float3 n=normalize(i.normal)*IS_FRONT_VFACE(front,1,-1);
                float3 dx=ddx(i.world),dy=ddy(i.world),a=cross(dy,n),b=cross(n,dx);float det=dot(dx,a);
                n=normalize(abs(det)*n-.1*sign(det)*(ddx(relief)*a+ddy(relief)*b)+n*1e-8);
                SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.specular=.04;s.smoothness=_Kind<.5?.19:.07;s.normalTS=float3(0,0,1);s.occlusion=.85+.15*coarse;
                InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=max(SampleSH(n),_AmbientFill.rgb*(.7+.3*saturate(n.y)));d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.pos);d.shadowMask=1;
                half3 color=UniversalFragmentPBR(d,s).rgb;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
