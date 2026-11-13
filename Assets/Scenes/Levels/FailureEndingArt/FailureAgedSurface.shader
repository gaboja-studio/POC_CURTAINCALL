Shader "CurtainCall/Endings/Aged Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Surface texture", 2D) = "white" {}
        [MainColor] _BaseColor("Surface tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Surface normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = 1
        _Kind("Costume 0 / plaster 1 / floor 2 / wood 3", Float) = 0
        _Wear("Wear", Range(0,1)) = .7
        _Stains("Dried stains", Range(0,1)) = .8
        _Seed("Variation", Float) = 1
        _Relief("Surface relief", Range(0,.2)) = .035
        _Smoothness("Rough surface sheen", Range(0,1)) = .12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Kind, _Wear, _Stains, _Seed, _Relief, _Smoothness, _BumpScale;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float3 positionOS:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes v)
        {
            Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.positionOS=v.positionOS.xyz;return o;
        }
        float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float Noise(float2 p)
        {
            float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);
            return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);
        }
        float Grain(float2 p) { return Noise(p)*.58+Noise(p*2.11+12)*.28+Noise(p*4.23+37)*.14; }
        float Spots(float2 p)
        {
            float2 cell=floor(p),f=frac(p);float seed=Hash(cell+_Seed);
            float2 center=.18+.64*float2(Hash(cell+19),Hash(cell+61));
            float radius=lerp(.025,.31,seed*seed);
            return (1-smoothstep(radius*.55,radius,length((f-center)*float2(1,.85))))*step(.43,Hash(cell+42));
        }
        half4 Frag(Varyings i):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float3 world=i.positionWS+float3(0,1000,0);
            float2 p=_Kind>1.5&&_Kind<2.5?world.xz:world.xy;
            if(_Kind<.5)p=(i.positionOS.xy+i.positionOS.z*float2(.47,.83))*3.2;
            p+=_Seed*.17;
            float coarse=Grain(p*2.4),fine=Noise(p*55),grit=Grain(p*25);
            half3 raw=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
            half luminance=dot(raw,half3(.2126,.7152,.0722));
            raw=lerp(raw,luminance.xxx,.28*_Wear);
            float grime=smoothstep(.28,.73,coarse)*_Wear;
            float height=grit*.06+fine*.005;
            if(_Kind>.5&&_Kind<1.5)
            {
                // Islands of remaining plaster, exposed dark substrate, tide marks and pits.
                float peel=1-smoothstep(.27,.42,Grain(p*.78+grit*.2));
                raw*=lerp(half3(1.02,1.01,.95),half3(.46,.48,.40),peel);
                raw*=lerp(.78,1.12,grit);
                float tide=(1-smoothstep(.10,1.12,world.y))*Grain(p*float2(7,.9));
                raw*=1-tide*.78;height+=peel*.025;
            }
            else if(_Kind>2.5)
            {
                float grain=Grain(p*float2(1.7,95));
                raw*=lerp(.30,1.45,grain);
                raw*=1-smoothstep(.74,.91,Noise(p*float2(.9,260)))*.65;
                height+=grain*.025;
            }
            else if(_Kind>1.5)
            {
                raw*=lerp(.60,1.15,grit);
                float worn=1-smoothstep(.36,.46,coarse);
                raw=lerp(raw,raw*half3(1.2,1.15,.96),worn*.4);
            }
            else
            {
                // Cloth fibres, desaturated ground-in dirt, scuffs and dried smears.
                float weave=sin(i.uv.x*1700)*sin(i.uv.y*1700);
                raw*=.82+.18*grit+weave*.025;height+=weave*.0004;
            }
            raw=lerp(raw,raw*half3(.28,.23,.17),grime*.63);
            float cluster=Grain(p*2.1+float2(27,8));
            float region=1;
            if(_Kind>.5&&_Kind<1.5)
            {
                float a=length((world.xy-float2(1.1,1.45))/float2(.78,1.0));
                float b=length((world.xy-float2(3.35,1.95))/float2(.93,.9));
                float c=length((world.xy-float2(2.25,.55))/float2(1.35,.56));
                region=1-smoothstep(.55,1.2,min(min(a,b),c)+Noise(p*9)*.25);
            }
            if(_Kind>1.5&&_Kind<2.5)region=1-smoothstep(.6,1.4,length((world.xz-float2(2.1,-1.0))/float2(2.0,1.1))+Noise(p*7)*.2);
            float blob=smoothstep(_Kind<.5?.48:.58,_Kind<.5?.66:.74,cluster+Noise(p*23)*.13);
            float drops=max(Spots(p*7.3),Spots(p*19.7+17)*.55);
            float drip=smoothstep(.75,.88,Noise(p*float2(45,1.9)+11))*smoothstep(.42,.62,cluster);
            float stain=saturate(max(blob,max(drops*.82,drip*.85))*region*_Stains);
            float broken=smoothstep(.24,.37,Noise(p*155));
            stain*=lerp(.55,1,broken);
            half3 dried=lerp(half3(.035,.006,.005),half3(.16,.025,.017),Grain(p*12+4));
            // A brown-red opaque crust, with only a faint grazing highlight.
            raw=lerp(raw,dried,stain*.98);
            height+=stain*.018;
            float3 n=normalize(i.normalWS),dx=ddx(i.positionWS),dy=ddy(i.positionWS);
            float2 uvDx=ddx(i.uv),uvDy=ddy(i.uv);
            float3 dp2perp=cross(dy,n),dp1perp=cross(n,dx);
            float3 t=dp2perp*uvDx.x+dp1perp*uvDy.x,b=dp2perp*uvDx.y+dp1perp*uvDy.y;
            float inverse=rsqrt(max(max(dot(t,t),dot(b,b)),1e-12));
            float3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
            n=normalize(t*inverse*normalTS.x+b*inverse*normalTS.y+n*normalTS.z);
            float3 r1=cross(dy,n),r2=cross(n,dx);float determinant=dot(dx,r1);
            n=normalize(abs(determinant)*n-_Relief*sign(determinant)*(ddx(height)*r1+ddy(height)*r2)+n*1e-8);
            SurfaceData s=(SurfaceData)0;s.albedo=raw;s.alpha=1;s.metallic=0;s.specular=.04;
            s.smoothness=lerp(_Smoothness,.19,stain);s.normalTS=half3(0,0,1);s.occlusion=1-grime*.22;
            InputData d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
            d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);d.bakedGI=SampleSH(n);d.vertexLighting=VertexLighting(i.positionWS,n);
            d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
            return UniversalFragmentPBR(d,s);
        }
        half4 DepthFrag(Varyings i):SV_Target { return 0; }
        half4 NormalFrag(Varyings i):SV_Target { return half4(normalize(i.normalWS),0); }
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            Varyings ShadowVert(Attributes v)
            {
                Varyings o=Vert(v);
                #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction=normalize(_LightPosition-o.positionWS);
                #else
                float3 direction=_LightDirection;
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" } ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
