// 바닥 장판(원형 AoE) — URP용 방사형 반투명 셰이더.
// 바닥에 눕힌 Quad(회전 X=90)에 이 머티리얼을 입혀 사용. 중앙 채움 + 가장자리 링 + 맥동.
Shader "Custom/GroundAoE"
{
    Properties
    {
        _Color         ("Color", Color) = (1.0, 0.3, 0.2, 1.0)
        _FillAlpha     ("Fill Alpha (안쪽 채움)", Range(0, 1)) = 0.25
        _EdgeThickness ("Edge Thickness (링 두께)", Range(0.001, 0.5)) = 0.08
        _EdgeIntensity ("Edge Intensity (링 밝기)", Range(0, 5)) = 2.0
        _PulseSpeed    ("Pulse Speed (맥동 속도, 0=끔)", Range(0, 8)) = 2.0
        _PulseAmount   ("Pulse Amount (맥동 세기)", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundAoEForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha // 일반 반투명. 발광 느낌 원하면 SrcAlpha One(가산)으로.
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _FillAlpha;
                float  _EdgeThickness;
                float  _EdgeIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // UV 중심(0.5,0.5) 기준 거리: 0=중앙, 1=쿼드에 내접한 원의 가장자리.
                float d = length(IN.uv - 0.5) * 2.0;
                if (d > 1.0) discard; // 원 밖은 그리지 않음(사각 쿼드 → 원형)

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // 가장자리 링: d가 (1 - 두께)~1 구간에서 밝아짐.
                float edge  = smoothstep(1.0 - _EdgeThickness, 1.0, d);
                float alpha = saturate(_FillAlpha + edge * _EdgeIntensity * pulse);

                half3 col = _Color.rgb * (1.0 + edge * max(0.0, _EdgeIntensity - 1.0) * pulse);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
