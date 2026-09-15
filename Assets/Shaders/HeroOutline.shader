// 선택된 영웅 실루엣 테두리 — URP용 Inverted Hull(뒤집힌 껍질) 외곽선.
// HeroOutlineEffect가 원본 렌더러를 복제해 이 머티리얼을 입힌다.
// 정점을 노멀 방향으로 살짝 밀어낸 뒤 앞면을 컬링해, 원본 메쉬가 덮지 않는
// 가장자리(실루엣)에서만 확장된 뒷면이 비쳐 보이게 만드는 방식.
Shader "Custom/HeroOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1.0, 0.85, 0.1, 1.0)
        _OutlineWidth ("Outline Width (Object Space)", Range(0, 0.1)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HeroOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 expandedOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(expandedOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
