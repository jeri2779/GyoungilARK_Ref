#ifndef RANGE_SCAN_INCLUDED
#define RANGE_SCAN_INCLUDED

// 월드 좌표를 따라 움직이는 대각선 사거리 스캔을 계산합니다.
void RangeScan_float(float3 WorldPos, out float3 Color, out float Alpha)
{
    float dirLength = max(length(_ScanDirection.xy), 0.001);
    float2 direction = _ScanDirection.xy / dirLength;
    float axis = dot(WorldPos.xz, direction);
    float phase = frac(axis * _ScanScale - _Time.y * _ScanSpeed);
    float offset = abs(phase - 0.5);
    float width = saturate(_ScanWidth);
    float softness = max(_ScanSoft, 0.001);
    float band = 1.0 - smoothstep(width, width + softness, offset);

    Color = lerp(_BaseColor.rgb, _ScanColor.rgb, band * saturate(_ScanBlend));
    Alpha = saturate(_BaseAlpha + band * _ScanAlpha);
}

// Half 정밀도 그래프에서도 같은 사거리 스캔을 사용합니다.
void RangeScan_half(half3 WorldPos, out half3 Color, out half Alpha)
{
    float3 resultColor;
    float resultAlpha;

    RangeScan_float(WorldPos, resultColor, resultAlpha);
    Color = resultColor;
    Alpha = resultAlpha;
}

#endif
