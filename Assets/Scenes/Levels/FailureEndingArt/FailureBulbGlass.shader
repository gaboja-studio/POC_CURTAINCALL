Shader "CurtainCall/Endings/Bulb Glass"
{
    Properties { _Tint("Glass tint",Color)=(.38,.28,.15,1) _LampPower("Filament power",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _LampPower;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 positionOS:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.positionOS=v.positionOS.xyz;return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS),view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim=pow(1-saturate(dot(n,view)),2.6);
                float glint=pow(saturate(dot(reflect(-view,n),normalize(float3(-.6,.7,-.8)))),48);
                float2 p=float2(atan2(i.positionOS.z,i.positionOS.x)*.08,i.positionOS.y);
                float cloud=Noise(p*32)+Noise(p*87)*.3;
                float soot=smoothstep(-.12,.07,i.positionOS.y)*(.3+.7*cloud);
                float dust=smoothstep(.54,.94,cloud)*.24;
                float specks=step(.88,Noise(p*540))*.19;
                float grime=saturate(soot*.56+dust+specks);
                half3 glass=_Tint.rgb+rim*half3(.28,.20,.10)+glint*half3(.7,.62,.45)*(1-grime);
                half3 lit=lerp(glass,half3(.11,.075,.038),grime)*lerp(.025,1,_LampPower);
                float2 filamentDistance=(i.positionOS.xy-float2(0,-.115))/float2(.037,.052);
                float halo=exp(-dot(filamentDistance,filamentDistance)*2)*_LampPower;
                lit+=half3(1.4,.65,.16)*halo;
                return half4(lit,.025+rim*.32+glint*.13+grime*.55+halo*.16);
            }
            ENDHLSL
        }
    }
}
