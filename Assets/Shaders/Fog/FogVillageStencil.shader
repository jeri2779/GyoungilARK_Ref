Shader "Hidden/Fog/VillageStencil"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "VillageStencil"
            ZWrite Off
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertexMain
            #pragma fragment FragmentMain
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct VertexInput
            {
                float4 positionOS : POSITION;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
            };

            VertexOutput VertexMain(VertexInput inputData)
            {
                VertexOutput outputData;
                outputData.positionCS = TransformObjectToHClip(inputData.positionOS.xyz);
                return outputData;
            }

            half4 FragmentMain() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
