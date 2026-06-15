Shader "Custom/AdditiveSprite"
{
    // 加色精灵：保留贴图 RGB（区别于 AdditiveGlow 只取 alpha 形状），按 tint×顶点色 叠加发光。
    // 适合画在纯黑底上的 VFX（火花/闪光）——黑底在 additive 下自动隐形，无需抠图。
    // _Cutoff 减掉接近黑的底噪；tint 的 alpha 当整体强度/淡出用。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color  ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Black Cutoff", Range(0,0.3)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One One   // 纯加色

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;
            float  _Cutoff;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                c = max(0.0.xxx, c - _Cutoff.xxx);
                return half4(c * IN.color.rgb * IN.color.a, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
