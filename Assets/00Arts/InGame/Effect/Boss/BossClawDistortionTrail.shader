Shader "AlphaZero/Boss/ClawDistortionTrail"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Range(0, 0.02)) = 0.006
        _Opacity ("Distortion Blend", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float edge = smoothstep(0.0, 0.35, 1.0 - abs(uv.y * 2.0 - 1.0));
                float phase = uv.x * 32.0 - _Time.y * 12.0;
                float2 wave = float2(
                    sin(phase + sin(uv.y * 11.0 + _Time.y * 5.0)),
                    cos(uv.x * 21.0 + uv.y * 15.0 + _Time.y * 9.0));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 distortedUV = screenUV + wave * (_DistortionStrength * edge);

                // 트레일 색을 그리지 않고, 흔들린 화면 배경만 원래 화면에 섞는다.
                half3 sceneColor = SampleSceneColor(distortedUV);
                half alpha = saturate(_Opacity * edge * input.color.a);
                return half4(sceneColor, alpha);
            }
            ENDHLSL
        }
    }
}
