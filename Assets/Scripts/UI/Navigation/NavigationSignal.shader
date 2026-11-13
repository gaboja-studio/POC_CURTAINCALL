Shader "CurtainCall/NavigationSignal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SignalColor ("Signal color", Color) = (1,1,1,1)
        _UseSignalTint ("Use signal color", Float) = 0
        _PulseOpacity ("Pulse opacity", Float) = 1
        _PulseScale ("Pulse scale", Float) = 1
        _SoftMask ("Soft halo", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" "DisableBatching"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float4 _SignalColor;
            float _UseSignalTint;
            float _PulseOpacity, _PulseScale, _SoftMask;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Output vert(Input v)
            {
                Output o; v.vertex.xy *= _PulseScale; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            float4 frag(Output i):SV_Target
            {
                float a=tex2D(_MainTex,i.uv).a;
                a=lerp(smoothstep(.35,.85,a),a,_SoftMask);
                return float4(lerp(i.color.rgb,_SignalColor.rgb,_UseSignalTint), i.color.a*a*_PulseOpacity);
            }
            ENDHLSL
        }
    }
}
