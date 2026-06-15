Shader "UI/LiquidFill"
{
    // 液体进度条:按 _Fill(0~1)裁切,液面边界是随时间流动的正弦波(_Wave 控波幅,由代码在变化时浪涌)。
    // 横向填充(从左到右),液面=右侧竖向波动边。当作 Image(Simple)的 material 用，颜色走 Image.color。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color     ("Tint", Color) = (1,1,1,1)
        _Fill      ("Fill", Range(0,1)) = 1
        _Vertical  ("Vertical Fill (0=横 1=竖)", Float) = 0
        _Aspect    ("Aspect (厚/长，代码填)", Float) = 1
        _Wave      ("Wave Amp", Range(0,1)) = 0.2
        _WaveFreq  ("Wave Freq", Float) = 8
        _WaveSpeed ("Wave Speed", Float) = 5
        _Shimmer   ("Shimmer", Range(0,1)) = 0.18
        _EdgeSoft  ("Edge Soft (占厚度)", Range(0,1)) = 0.15
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
            float _Fill, _Vertical, _Aspect, _Wave, _WaveFreq, _WaveSpeed, _Shimmer, _EdgeSoft;

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

                // 填充方向：横=沿 x、竖=沿 y；液面波动沿与之垂直的方向
                float fillCoord = lerp(IN.uv.x, IN.uv.y, _Vertical);
                float waveCoord = lerp(IN.uv.y, IN.uv.x, _Vertical);
                // 液面边界：沿垂直方向的正弦(两层不同频率/速度叠加 → 荡漾)
                float w = sin(waveCoord * _WaveFreq + t * _WaveSpeed) * _Wave
                        + sin(waveCoord * _WaveFreq * 0.55 - t * _WaveSpeed * 0.8) * _Wave * 0.5;
                w *= _Aspect;   // 波幅按厚度算，宽扁条上不会变成长尖刺
                // 满/空附近收敛波动，避免满条被啃掉一条边
                w *= smoothstep(0.0, 0.06, _Fill) * smoothstep(1.0, 0.94, _Fill);
                float edge = _Fill + w;
                float soft = max(0.0015, _EdgeSoft * _Aspect);   // 软边也按厚度算，宽扁条上才不会糊成圆头
                float a = smoothstep(edge + soft, edge - soft, fillCoord);   // fillCoord<edge → 显示

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 流动高光
                float s = sin(IN.uv.x * 9.0 - t * 1.6) + sin(IN.uv.x * 5.0 + IN.uv.y * 4.0 + t * 2.0);
                float shim = pow(saturate(s * 0.5 + 0.5), 3.0) * _Shimmer;

                half3 rgb = tex.rgb * IN.color.rgb * (1.0 + shim);
                return half4(rgb, tex.a * IN.color.a * a);
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
