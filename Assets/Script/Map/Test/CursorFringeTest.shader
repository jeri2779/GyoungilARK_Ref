// 커서 강조 효과 프로토타입 — 가느다란 줄 여러 가닥이 각자 다른 타이밍으로 위아래 반복한다.
// 정식 버전(셰이더 그래프)으로 옮기기 전, 눈으로 빠르게 확인하기 위한 손으로 짠 임시 셰이더.
// 가닥 위치/굵기를 무작위로 흔들고(지터), 끝을 뾰족하게 좁히고, 가장자리를 부드럽게 해서 "막대"가 아닌 "실" 모양을 만든다.
Shader "Custom/CursorFringeTest"
{
    Properties
    {
        _CellDensity ("가닥 촘촘함", Range(4, 120)) = 24
        _MinHeight ("최소 높이(UV)", Range(0, 1)) = 0.12
        _MaxHeight ("최대 높이(UV)", Range(0, 1)) = 0.42
        _Speed ("파도 속도", Range(0.1, 5)) = 1.6
        _StrandWidth ("가닥 굵기(0~1, 1=완전히 붙음)", Range(0.02, 1)) = 0.16
        _JitterAmount ("가닥 위치 흔들림(0~0.4)", Range(0, 0.4)) = 0.25
        _WidthVariance ("가닥 굵기 편차(0~1)", Range(0, 1)) = 0.5
        _TaperPower ("끝 뾰족해지는 정도", Range(0.5, 4)) = 1.5
        _EdgeSoftness ("가장자리 부드러움", Range(0.001, 0.1)) = 0.02
        _ColorA ("색 A", Color) = (0.11, 0.62, 0.46, 1)
        _ColorB ("색 B", Color) = (0.22, 0.54, 0.87, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CursorFringe"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _CellDensity;
                float _MinHeight;
                float _MaxHeight;
                float _Speed;
                float _StrandWidth;
                float _JitterAmount;
                float _WidthVariance;
                float _TaperPower;
                float _EdgeSoftness;
                half4 _ColorA;
                half4 _ColorB;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 씨앗 값으로 항상 같은 랜덤값을 만든다(가닥마다 다른 성질을 담당).
            float HashCell(float seed)
            {
                return frac(sin(seed * 12.9898) * 43758.5453);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float scaled = IN.uv.x * _CellDensity;
                float cell = floor(scaled);
                float local = frac(scaled);

                float rand = HashCell(cell);
                float phase = rand * 6.2831853;
                float wave = sin(_Time.y * _Speed + phase) * 0.5 + 0.5;
                float height = lerp(_MinHeight, _MaxHeight, wave);

                // 가닥 중심을 칸 한가운데에서 무작위로 살짝 밀어낸다(격자 정렬 깨기).
                float jitterRand = HashCell(cell + 91.7);
                float centerOffset = (jitterRand - 0.5) * _JitterAmount;

                // 가닥마다 밑동 굵기를 무작위로 다르게 만든다(균일한 판자 느낌 깨기).
                float widthRand = HashCell(cell + 193.3);
                float baseHalfWidth = (_StrandWidth * lerp(1.0 - _WidthVariance, 1.0, widthRand)) * 0.5;

                // 위로 갈수록 폭을 좁혀서 끝이 뾰족한 실 모양을 만든다.
                float heightRatio = saturate(IN.uv.y / max(height, 0.0001));
                float taperedHalfWidth = baseHalfWidth * pow(1.0 - heightRatio, _TaperPower);

                // 계단식 경계 대신 부드러운 경계로 가장자리를 처리한다.
                float dist = abs(local - 0.5 - centerOffset);
                float strandMask = 1.0 - smoothstep(taperedHalfWidth - _EdgeSoftness, taperedHalfWidth + _EdgeSoftness, dist);

                float heightMask = step(IN.uv.y, height);
                float fade = 1.0 - heightRatio;

                float alpha = strandMask * heightMask * fade;
                half4 color = lerp(_ColorA, _ColorB, rand);

                return half4(color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
