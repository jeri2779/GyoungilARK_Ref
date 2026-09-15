// 은신(Cloaking) 셰이더 — URP 전용. 하나의 재질 안에서 [본체 ↔ 흐릿한 은신]을 _CloakAmount로 섞는다.
// _CloakAmount 0 = 본체 텍스처 또렷(저지 시) / 1 = 배경 굴절+블러로 반투명·흐릿(걸을 때).
// 재질 교체 없이 이 값만 0~1로 보간하므로 전환이 "점점" 자연스럽게 일어난다(팝 없음).
// _BaseMap(본체 텍스처)은 EnemyBase가 MaterialPropertyBlock으로 몬스터별로 주입 → 재질 하나를 모두 공유 가능.
//
// ⚠️ 흐릿한 배경을 샘플하려면 URP 에셋에서 "Opaque Texture"를 켜야 한다.
//    꺼져 있으면 배경 대신 _CloakColor 단색으로 폴백된다.
Shader "Enemy/Cloak"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Cloak)]
        _CloakAmount ("Cloak Amount (0 clear - 1 cloaked)", Range(0,1)) = 1
        _CloakColor ("Cloak Tint", Color) = (0.6, 0.8, 1.0, 1)
        _Alpha ("Cloaked Alpha", Range(0,1)) = 0.35

        [Header(Refraction and Blur)]
        _Distortion ("Distortion", Range(0,3)) = 1
        _BlurSize ("Blur Size", Range(0,5)) = 1.5
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseSpeed ("Noise Speed", Float) = 1.5

        [Header(Rim (edge glow))]
        _RimColor ("Rim Color", Color) = (0.7, 0.9, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimIntensity ("Rim Intensity", Range(0,3)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Cloak"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // _CameraOpaqueTexture 선언 + SampleSceneColor(uv) 제공. Opaque Texture 켜져 있어야 유효.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float  fogCoord    : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _CloakAmount;
                half4  _CloakColor;
                half   _Alpha;
                half   _Distortion;
                half   _BlurSize;
                float  _NoiseScale;
                float  _NoiseSpeed;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(pos.positionWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // 본체 색(언릿). _CloakAmount=0일 때 이게 그대로 또렷하게 보인다.
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // 화면 좌표 UV (0~1). 뒤 배경 샘플에 사용.
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

                // 시간에 따라 일렁이는 굴절 오프셋 — 은신량만큼만.
                float  t   = _Time.y * _NoiseSpeed;
                float2 nUV = IN.positionWS.xz * _NoiseScale;
                float2 distort = float2(sin(nUV.y * 6.2831 + t), cos(nUV.x * 6.2831 + t));
                distort *= _Distortion * 0.01 * _CloakAmount;

                // 뒤 배경을 여러 번 샘플해 평균 → 흐릿(블러). 5탭 십자 커널.
                float2 texel = _BlurSize * 0.0025;
                half3 bg = SampleSceneColor(screenUV + distort);
                bg += SampleSceneColor(screenUV + distort + float2( texel.x, 0));
                bg += SampleSceneColor(screenUV + distort + float2(-texel.x, 0));
                bg += SampleSceneColor(screenUV + distort + float2(0,  texel.y));
                bg += SampleSceneColor(screenUV + distort + float2(0, -texel.y));
                bg *= 0.2;
                bg *= _CloakColor.rgb; // 은신 색조

                // 프레넬 림 — 은신 중일수록 윤곽만 은은히.
                half fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);

                // 핵심: 은신량만큼 본체 → 흐릿 배경으로 섞고, 알파도 불투명(1) → _Alpha로 보간 = "점점" 전환.
                // 드러난 상태(_CloakAmount=0)는 무조건 불투명 — 원본 텍스처의 알파 채널(0일 수 있음)에 의존하지 않는다.
                half3 color = lerp(baseCol.rgb, bg, _CloakAmount) + _RimColor.rgb * fresnel * _RimIntensity * _CloakAmount;
                half alpha = lerp(1.0h, _Alpha, _CloakAmount);
                alpha = saturate(alpha + fresnel * _RimIntensity * _CloakAmount);

                color = MixFog(color, IN.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
