Shader "CurtainCall/Escape/FactoryConcrete"
{
 Properties
 {
  [MainTexture] _BaseMap("Factory concrete",2D)="white"{}
  [Normal] _BumpMap("Factory normal",2D)="bump"{}
  _DamageMap("Worn concrete",2D)="white"{}
  _LeakMask("Industrial runoff",2D)="black"{}
  [MainColor] _BaseColor("Facility tint",Color)=(.75,.76,.73,1)
  _BumpScale("Normal strength",Range(0,2))=.65
  _Smoothness("Smoothness",Range(0,1))=.12
  _TileScale("Texture repeats per metre",Float)=.4
  _Floor("Floor wear",Range(0,1))=0
  _PanelSize("Formwork metres",Vector)=(4,2.8,0,0)
  _ShaftFade("Shaft fade (world Y bottom, top, strength)",Vector)=(0,1,0,0)
  _Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;
   half4 _BaseColor;
   float4 _PanelSize;
   float4 _ShaftFade;
   half _BumpScale, _Smoothness, _TileScale, _Floor, _Cull;
  CBUFFER_END
  struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
  float4 DepthVertex(Attributes a):SV_POSITION {UNITY_SETUP_INSTANCE_ID(a);return TransformObjectToHClip(a.positionOS.xyz);}
  half4 EmptyFragment():SV_Target{return 0;}
  ENDHLSL
  Pass
  {
   Name "ForwardLit"
   Tags {"LightMode"="UniversalForward"}
   Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex Vertex
   #pragma fragment Fragment
   #pragma target 3.5
   #pragma multi_compile_instancing
   #pragma instancing_options renderinglayer
   #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile _ _LIGHT_LAYERS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_DamageMap);SAMPLER(sampler_DamageMap);
   TEXTURE2D(_LeakMask);SAMPLER(sampler_LeakMask);
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 fogLight:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
   Varyings Vertex(Attributes a)
   {
    Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
    o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);
    o.fogLight=half4(ComputeFogFactor(o.positionCS.z),VertexLighting(o.positionWS,o.normalWS));return o;
   }
   float Hash(float2 p){float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
   float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
   half4 Fragment(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);
    half3 n=normalize(i.normalWS);float3 a=abs(n);
    // 한 면의 우세 축을 사용해 늘어난 UV와 세 방향 중복 텍스처 샘플을 피한다.
    bool y=a.y>=a.x&&a.y>=a.z;bool x=!y&&a.x>a.z;
    float2 p=y?i.positionWS.xz:x?i.positionWS.zy:i.positionWS.xy;
    float2 panel=max(_PanelSize.xy,.1);float2 cell=floor(p/panel);
    float2 uv=p*_TileScale+float2(Hash(cell),Hash(cell+13.4))*3;
    half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
    half3 worn=SAMPLE_TEXTURE2D(_DamageMap,sampler_DamageMap,uv*.79+7.3).rgb;
    half drift=Noise(p*.27);
    half patch=smoothstep(.40,.82,drift);
    half3 albedo=lerp(base,worn,patch*lerp(.28,.66,_Floor));
    albedo*=lerp(.82,1.08,Noise(p*.11))*_BaseColor.rgb;
    float2 edge=min(frac(p/panel),1-frac(p/panel))*panel;
    half joint=smoothstep(lerp(.008,.004,_Floor),lerp(.025,.013,_Floor),min(edge.x,edge.y));
    albedo*=lerp(lerp(.62,.72,_Floor),1,joint);
    // 작은 거푸집 체결 흔적은 벽에서만 보인다. 바닥은 보수 얼룩으로 구분한다.
    float2 tie=(frac(p/float2(2,1.4))-.5)*float2(2,1.4);
    half holes=smoothstep(.018,.038,length(tie));albedo*=lerp(lerp(.55,1,holes),1,_Floor);
    // Let runoff follow gravity on walls. On upward faces, grime collects at joints
    // and in irregular patches rather than repeating the vertical streak texture.
    half damp;
    half3 dampTint=half3(.41,.32,.25);
    half wetSmoothness=.49;
    if (!y)
    {
     // Continuous projection avoids the rectangular borders and repeated rows of decal cells.
     half spread=Noise(p*float2(.075,.045)+8.3);
     float2 runoffUV=p*float2(.13,.06);
     runoffUV.x+=(spread-.5)*.28;
     half fineRunoff=SAMPLE_TEXTURE2D(_LeakMask,sampler_LeakMask,runoffUV).r;
     half broadRunoff=SAMPLE_TEXTURE2D(_LeakMask,sampler_LeakMask,runoffUV*float2(.53,.71)+float2(.37,.19)).r;
     half runoff=smoothstep(.10,.65,fineRunoff*.65+broadRunoff*.35);
     half wetArea=smoothstep(.30,.78,spread);
     damp=saturate(wetArea*.48+runoff*wetArea*.36+patch*.08);
     // Cement is mainly darkened by moisture. Brown iron runoff is sparse and faint.
     half ironResidue=runoff*smoothstep(.62,.83,drift)*.32;
     dampTint=lerp(half3(.64,.65,.62),half3(.63,.49,.36),ironResidue);
     wetSmoothness=.28;
    }
    else
    {
     half seam=1-smoothstep(.02,.38,min(edge.x,edge.y));
     half grain=saturate(dot(worn,half3(.21,.72,.07)));
     damp=smoothstep(.36,.77,drift+.08*(.5-grain))*lerp(.62,1,seam);
    }
    // Water darkens porous cement and carries brown residue from nearby iron.
    albedo*=lerp(half3(1,1,1),dampTint,damp);
    half3 tn=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
    half3 detail=y?half3(tn.x,tn.z*sign(n.y),tn.y):x?half3(tn.z*sign(n.x),tn.y,tn.x):half3(tn.x,tn.y,tn.z*sign(n.z));
    n=normalize(lerp(n,detail,.65));
    InputData d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=n;
    d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
    d.fogCoord=i.fogLight.x;d.vertexLighting=i.fogLight.yzw;d.bakedGI=SampleSH(n);
    d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.metallic=0;s.specular=half3(.04,.04,.04);
    s.smoothness=lerp(_Smoothness*lerp(.65,1,patch),wetSmoothness,damp);s.normalTS=half3(0,0,1);s.occlusion=lerp(.8,1,joint);s.alpha=1;
    half4 color=UniversalFragmentPBR(d,s);
    color.rgb=MixFog(color.rgb,d.fogCoord);
    half belowRoute=1-smoothstep(_ShaftFade.x,_ShaftFade.y,i.positionWS.y);
    color.rgb=lerp(color.rgb,half3(.0006,.0008,.0012),belowRoute*_ShaftFade.z);
    return color;
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex ShadowVertex
   #pragma fragment EmptyFragment
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection,_LightPosition;
   float4 ShadowVertex(Attributes a):SV_POSITION
   {
    UNITY_SETUP_INSTANCE_ID(a);float3 p=TransformObjectToWorld(a.positionOS.xyz);float3 n=TransformObjectToWorldNormal(a.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
     float3 light=normalize(_LightPosition-p);
    #else
     float3 light=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,light));
    #if UNITY_REVERSED_Z
     clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #else
     clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #endif
    return clip;
   }
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex DepthVertex
   #pragma fragment EmptyFragment
   #pragma multi_compile_instancing
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormals"
   Tags {"LightMode"="DepthNormals"}
   ZWrite On Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex NormalsVertex
   #pragma fragment NormalsFragment
   #pragma multi_compile_instancing
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   struct NormalVaryings {float4 positionCS:SV_POSITION;half3 normalWS:TEXCOORD0;};
   NormalVaryings NormalsVertex(Attributes a){UNITY_SETUP_INSTANCE_ID(a);NormalVaryings o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(a.normalOS);return o;}
   half4 NormalsFragment(NormalVaryings i):SV_Target
   {
    half3 n=normalize(i.normalWS);
    #if _GBUFFER_NORMALS_OCT
     return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
    #else
     return half4(n,0);
    #endif
   }
   ENDHLSL
  }
 }
}
