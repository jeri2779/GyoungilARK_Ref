// 돔형 보호막(포스필드) — URP용 프레넬 반투명 셰이더.
// 정면은 거의 투명, 시선과 스치는 가장자리로 갈수록 밝게 빛나 비눗방울/실드 느낌을 낸다.
// 반구(hemisphere) 또는 구(sphere) 메시에 이 셰이더로 만든 머티리얼을 입혀서 사용.
Shader "Custom/ShieldDome"
{
    Properties
    {
        _Color            ("Shield Color", Color) = (0.3, 0.7, 1.0, 1.0)
        _BaseAlpha        ("Base Alpha (정면 투명도)", Range(0, 1)) = 0.12
        _FresnelPower     ("Fresnel Power (가장자리 두께)", Range(0.5, 8)) = 3.0
        _FresnelIntensity ("Fresnel Intensity (가장자리 밝기)", Range(0, 6)) = 2.5
        _PulseSpeed       ("Pulse Speed (맥동 속도, 0=끔)", Range(0, 8)) = 1.5
        _PulseAmount      ("Pulse Amount (맥동 세기)", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType"       = "Transparent"
            "Queue"            = "Transparent"
            "RenderPipeline"   = "UniversalPipeline"
            "IgnoreProjector"  = "True"
        }

        Pass
        {
            Name "ShieldForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One    // 가산 혼합 — 겹칠수록 밝게 빛남(글로우). 일반 반투명 원하면 아래 주석 참고.
            // Blend SrcAlpha OneMinusSrcAlpha  // ← 이걸로 바꾸면 발광 없는 순수 반투명
            ZWrite Off            // 반투명이라 깊이 기록 안 함
            Cull Off              // 돔 안쪽 면도 보이게(비눗방울처럼 앞뒤 다 렌더)

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _BaseAlpha;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // 프레넬: 시선과 법선이 스칠수록(가장자리) 1에 가까워짐.
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // 은은한 맥동(살아있는 실드 느낌). PulseSpeed=0이면 정적.
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float glow  = fresnel * _FresnelIntensity * pulse;
                float alpha = saturate(_BaseAlpha + glow);

                half3 col = _Color.rgb * (_BaseAlpha + glow);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
