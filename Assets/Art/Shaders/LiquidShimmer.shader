Shader "UI/LiquidShimmer"
{
    // UI 液体荡漾：在填充贴图上叠 UV 波动 + 流动高光带(_Time 驱动),做出水面荡漾/焦散感。
    // 当作 Image 的 material 用；颜色由 Image.color(顶点色)× _Color 控制。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color        ("Tint", Color) = (1,1,1,1)
        _ShimmerSpeed ("Shimmer Speed", Float) = 1.5
        _ShimmerFreq  ("Shimmer Freq", Float) = 9
        _ShimmerAmp   ("Shimmer Amp", Range(0,1)) = 0.28
        _WaveAmp      ("Wave Amp (UV 起伏)", Range(0,0.08)) = 0.02
        _WaveFreq     ("Wave Freq", Float) = 11
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
            float _ShimmerSpeed, _ShimmerFreq, _ShimmerAmp, _WaveAmp, _WaveFreq;

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                o.color = IN.color * _Color;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // 1) UV 上下起伏：按 x 给 y 一点 sin 位移 → 光泽像水面荡漾
                float2 uv = IN.uv;
                uv.y += sin(uv.x * _WaveFreq + t * _ShimmerSpeed) * _WaveAmp;
                uv.y = saturate(uv.y);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // 2) 流动高光带：两条不同频率/速度的 sin 叠加 → 焦散荡漾
                float s = sin(uv.x * _ShimmerFreq - t * _ShimmerSpeed)
                        + sin(uv.x * _ShimmerFreq * 0.6 + uv.y * 5.0 + t * _ShimmerSpeed * 1.4);
                s = saturate(s * 0.5 + 0.5);
                float shimmer = pow(s, 3.0) * _ShimmerAmp;

                half3 rgb = tex.rgb * IN.color.rgb * (1.0 + shimmer);
                return half4(rgb, tex.a * IN.color.a);
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
