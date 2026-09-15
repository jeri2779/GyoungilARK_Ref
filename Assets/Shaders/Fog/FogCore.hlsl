#ifndef FOG_CORE_INCLUDED
#define FOG_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float4 _FogAreas[8];
float _FogOpens[8];
int _FogCount;
float _FogCoverMargin;

float BoxDist(float2 position, float4 area)
{
    float2 delta = abs(position - area.xy) - area.zw;
    float outside = length(max(delta, 0.0));
    float inside = min(max(delta.x, delta.y), 0.0);

    return outside + inside;
}

float FogSampleDepth(float2 screenPosition)
{
#if UNITY_REVERSED_Z
    return SampleSceneDepth(screenPosition);
#else
    return lerp(
        UNITY_NEAR_CLIP_VALUE,
        1.0,
        SampleSceneDepth(screenPosition)
    );
#endif
}

float FogLockedCoverage(float areaDistance)
{
    return step(areaDistance, _FogCoverMargin);
}

float FogOpenedCoverage(float areaDistance)
{
    return step(areaDistance, 0.0);
}

float FogModuleMask(float2 worldPosition)
{
    float moduleMask = 0.0;
    float openedMask = 0.0;
    int areaCount = _FogCount;

    [unroll]
    for (int areaIndex = 0; areaIndex < 8; areaIndex++)
    {
        if (areaIndex >= areaCount)
        {
            break;
        }

        float areaDistance = BoxDist(worldPosition, _FogAreas[areaIndex]);
        float lockedCoverage = FogLockedCoverage(areaDistance);
        float openedCoverage = FogOpenedCoverage(areaDistance);
        float openAmount = saturate(_FogOpens[areaIndex]);

        moduleMask = max(
            moduleMask,
            lockedCoverage * (1.0 - openAmount)
        );
        openedMask = max(
            openedMask,
            openedCoverage * openAmount
        );
    }

    return min(moduleMask, 1.0 - openedMask);
}

void FogAreaMask_float(
    float2 UV,
    out float AreaMask)
{
    float depth = FogSampleDepth(UV);
    float3 worldPosition = ComputeWorldSpacePosition(
        UV,
        depth,
        UNITY_MATRIX_I_VP
    );

    AreaMask = FogModuleMask(worldPosition.xz);
}

#endif
