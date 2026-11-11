Shader "Custom/FlagWaveShader"
{
     Properties
    { 
        _BaseColor("Base Color", Color) = (1, 1, 1, 1) // 기존 텍스쳐 컬러
        _MainTex("MainTex", 2D) = "white" // 메인 텍스쳐
        _SecondTex("SecondTex", 2D) = "black" // 노이즈 텍스쳐
        _FlowSpeed("Flow Speed", float) = 1 // 노이즈 적용 속도
        _AmplitudeX("Amplitude X", float) = 0.1 // x축 흔들림 정도
        _AmplitudeY("Amplitude Y", float) = 0.1 // y축 흔들림 정도
        _Offset("Texture Offset", Vector) = (0, 0, 0, 0) // 텍스쳐 오프셋 지정
        _ColorVariation("Color Variation", float) = 0.1 // 색상 변경 정도
        _CurveAmount("Curve Amount", float) = 1.0
        _CurveScale("Curve Scale", float) = 0.1
        _CurveSpeed("Curve Speed", float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;  
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 screenPos    : TEXCOORD1;
            };            

            TEXTURE2D(_MainTex);
            TEXTURE2D(_SecondTex);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_SecondTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;   
                float4 _BaseColor;
                float _FlowSpeed;
                float _AmplitudeX;
                float _AmplitudeY;
                float4 _Offset;
                float _ColorVariation;
                float _CurveAmount;
                float _CurveScale;
                float _CurveSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex) + _Offset.xy;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 noiseCoords = IN.screenPos.xy * _FlowSpeed + _Time.yy * _FlowSpeed;
                float2 noiseCoordsLayer2 = IN.screenPos.xy * (_FlowSpeed * 0.5) + _Time.yy * _FlowSpeed;

                float noiseValueX = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, noiseCoords).r * 2 - 1;
                float noiseValueY = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, noiseCoords + float2(0.1, 0.1)).r * 2 - 1; 
                
                float adjustedAmplitudeX = _AmplitudeX * (1.0 - IN.uv.x);
                float2 uvNoised = IN.uv + float2(noiseValueX * adjustedAmplitudeX, noiseValueY * _AmplitudeY);

                float fixPointEffect = 1.0 - uvNoised.x;
                float animatedCurveAmount = sin(_Time.y * _CurveSpeed + uvNoised.x * _CurveAmount) * _CurveScale * fixPointEffect;
                uvNoised.y += animatedCurveAmount;

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvNoised) * _BaseColor;

                float noiseValueColor = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, noiseCoordsLayer2).r;
                float colorVariation = (noiseValueColor - 0.5) * 2 * _ColorVariation;
                color.rgb += colorVariation;

                return color;
            }
            ENDHLSL
        }
    }
}
