Shader "Hidden/CurtainCall/Pursuit/GraftBake"
{
 Properties {
  _MainTex("Donor surface",2D)="white"{}
  _BaseColor("Donor tint",Color)=(1,1,1,1)
  _VariantColor("Outfit",Color)=(1,1,1,1)
  _VariantPattern("Pattern",Float)=0
  _Model("Character",Float)=0
  _OutputSurface("Surface output",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline"}
  Pass {
   Cull Off ZWrite Off ZTest Always Blend Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST; half4 _BaseColor,_VariantColor;
   float _VariantPattern,_Model,_OutputSurface;
   CBUFFER_END
   struct A {float3 p:POSITION;float2 uv:TEXCOORD0;float2 seam:TEXCOORD1;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 seam:TEXCOORD1;};
   V Vert(A v) {
    V o;o.p=float4(v.p.xy*2-1,0,1);
    #if UNITY_UV_STARTS_AT_TOP
    o.p.y=-o.p.y;
    #endif
    o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.seam=v.seam;return o;
   }
   half4 Frag(V i):SV_Target {
    float d=i.seam.x,arc=i.seam.y;
    // Millimetre-sized thread and punctures lie on the shell, never on detached geometry.
    d+=sin(arc*371)*.0007;
    float aa=max(fwidth(d),.00035);
    float cover=smoothstep(-aa,aa,d);
    float edge=1-smoothstep(.0011,.0024+aa,abs(d));
    float cell=(frac(arc/.024+.5)-.5)*.024;
    float crossThread=min(abs(cell-d*.65),abs(cell+d*.65));
    float thread=(1-smoothstep(.00075,.0015+aa,crossThread))*(1-smoothstep(.0065,.0105+aa,abs(d)));
    float hole=1-smoothstep(.0012,.0023+aa,length(float2(abs(cell)-.0052,abs(d)-.008)));
    float detail=max(thread,max(edge*.75,hole));
    float coverage=max(cover,detail);clip(coverage-.002);
    half3 raw=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
    // Match the player's palette substitution, before any dirt or blood is applied.
    float tile=floor(i.uv.x*4)+4*floor((1-i.uv.y)*4);
    float costume=(1-step(.1,abs(tile-3)))+(1-step(.1,abs(tile-4)))+(1-step(.1,abs(tile-5)))+(1-step(.1,abs(tile-8)))+(1-step(.1,abs(tile-11)));
    float dye=saturate((raw.r-raw.b)*3.5)*saturate((raw.r-raw.g-.10)*10)*saturate(costume);
    half luminance=dot(raw,half3(.3,.59,.11));
    half3 palette=_Model>2.5?lerp(_VariantColor.rgb,half3(1,.78,.70),.18):_VariantColor.rgb;
    if(_Model>.5&&_Model<1.5) raw=lerp(raw,raw*half3(.60,.36,.19),1-step(.1,abs(tile-12)));
    raw=lerp(raw,luminance*palette*(_Model<1.5?1.6:1.25),dye);
    float2 q=frac(i.uv*32)-.5;
    float motif=_VariantPattern<.5?1-smoothstep(.09,.14,abs(q.x)+abs(q.y)):_VariantPattern<1.5?1-smoothstep(.04,.08,abs(q.x)):_VariantPattern<2.5?1-smoothstep(.08,.13,length(q)):1-smoothstep(.04,.08,min(abs(q.x),abs(q.y)));
    raw*=1-dye*motif*.16;raw*=_BaseColor.rgb;
    half3 threadColor=half3(.035,.023,.014);
    half3 color=lerp(raw,threadColor,saturate(detail));
    float height=saturate(.5+thread*.42-edge*.32-hole*.3);
    return _OutputSurface>.5?half4(.45+.4*dye,.85,height,coverage):half4(color,coverage);
   }
   ENDHLSL
  }
  // Pad UV islands while protecting the authored seam band and its thread width.
  Pass {
   Cull Off ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma vertex PadVert
   #pragma fragment PadFrag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_TexelSize;
   TEXTURE2D(_CoverageMap); SAMPLER(sampler_CoverageMap);
   struct PadA {float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct PadV {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   PadV PadVert(PadA v){PadV o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;return o;}
   half4 PadFrag(PadV i):SV_Target {
    half4 original=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv),best=original;
    if(original.a>.99 || SAMPLE_TEXTURE2D(_CoverageMap,sampler_CoverageMap,i.uv).r>.5) return original;
    for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++) {
     half4 candidate=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy);
     if(candidate.a>best.a)best=candidate;
    }
    return best;
   }
   ENDHLSL
  }
  Pass {
   Cull Off ZWrite Off ZTest Always
   HLSLPROGRAM
   #pragma vertex CoverageVert
   #pragma fragment CoverageFrag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct CoverageA {float4 p:POSITION;float2 uv:TEXCOORD0;float2 seam:TEXCOORD1;};
   struct CoverageV {float4 p:SV_POSITION;float distance:TEXCOORD0;};
   CoverageV CoverageVert(CoverageA v) {
    CoverageV o;o.p=float4(v.p.xy*2-1,0,1);o.distance=v.seam.x;
    #if UNITY_UV_STARTS_AT_TOP
    o.p.y=-o.p.y;
    #endif
    return o;
   }
   half4 CoverageFrag(CoverageV i):SV_Target {return 1-step(.016,abs(i.distance));}
   ENDHLSL
  }
 }
}
