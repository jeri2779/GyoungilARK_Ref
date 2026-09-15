using UnityEngine;

// 맵이 화면 밖으로 벗어나지 않도록 카메라 초점을 경계 안으로 되돌린다.
// 맵 경계 상자를 화면에 투영해 삐져나온 양을 재고, 그만큼 초점을 화면 축 방향으로 밀어 넣는다.
// 원근 투영이라 한 번에 못 맞추고 여러 번 반복해 수렴시킨다.
public class CameraClamp
{
    private const float Step = 0.5f;   // 유한차분 간격
    private const int MaxPass = 6;

    private readonly Vector3[] corners = new Vector3[8];

    private Bounds box;
    private Quaternion rot;
    private float dist;
    private float tanH;
    private float tanV;

    // 맵이 화면 밖으로 새지 않도록 초점을 경계 안으로 맞춘 값을 돌려준다.
    public Vector3 FitFocus(Vector3 focus, Bounds area, Quaternion rotation, float distance,
                            float fieldOfView, float aspect, float fillH, float fillV)
    {
        box = area;
        rot = rotation;
        dist = distance;
        tanV = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        tanH = tanV * aspect;
        FillCorners(area);

        Vector3 right = rot * Vector3.right;       // 화면 가로 = 카메라 오른쪽(수평)
        Vector3 ahead = rot * Vector3.forward;     // 화면 세로 = 카메라 전방의 지면 투영
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 1e-6f)
        {
            return focus;   // 거의 수직으로 내려보면 세로 축이 사라진다
        }
        ahead.Normalize();

        for (int pass = 0; pass < MaxPass; pass++)
        {
            ProjectBox(focus, out Vector2 ndcMin, out Vector2 ndcMax);
            float shiftH = FitAxis(ndcMin.x, ndcMax.x, fillH);
            float shiftV = FitAxis(ndcMin.y, ndcMax.y, fillV);
            if (Mathf.Approximately(shiftH, 0f) && Mathf.Approximately(shiftV, 0f))
            {
                break;
            }
            // focus를 축으로 밀었을 때 ndc가 얼마나 변하는지(유한차분)로 필요한 거리를 역산.
            float slopeH = (ProjectCenter(focus + right * Step).x - ProjectCenter(focus).x) / Step;
            focus += right * SafeDivide(shiftH, slopeH);
            float slopeV = (ProjectCenter(focus + ahead * Step).y - ProjectCenter(focus).y) / Step;
            focus += ahead * SafeDivide(shiftV, slopeV);
        }
        return focus;
    }

    // 한 축의 ndc 범위[low,high]에 필요한 보정량. 맵이 화면보다 좁으면 중앙, 넓으면 빈 쪽만 메운다.
    // fill = 그 축의 이동 한계(1=화면 끝, 낮출수록 맵 끝을 화면 안쪽까지 끌어와 더 이동 가능).
    private float FitAxis(float low, float high, float fill)
    {
        if (high - low <= 2f * fill)
        {
            return -(low + high) * 0.5f;   // 화면보다 좁음 → 중앙
        }
        if (high < fill)
        {
            return fill - high;            // + 쪽에 빈 공간
        }
        if (low > -fill)
        {
            return -fill - low;            // - 쪽에 빈 공간
        }
        return 0f;                         // 맵이 화면을 덮음 → 자유 이동
    }

    // 상자 8꼭짓점이 화면 채움 비율(fillH, fillV) 안에 다 들어오는 최소 거리.
    // focus를 상자 중심에 고정해두고 계산하므로, 축별 화면 위치가 거리에 선형으로만 반응해 반복 없이 바로 구해진다.
    public float FitDistance(Vector3 focus, Bounds area, Quaternion rotation, float fieldOfView, float aspect, float fillH, float fillV)
    {
        rot = rotation;
        tanV = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        tanH = tanV * aspect;
        FillCorners(area);

        Quaternion inv = Quaternion.Inverse(rot);
        float need = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = inv * (corners[i] - focus);
            float needX = Mathf.Abs(local.x) / (fillH * tanH) - local.z;
            float needY = Mathf.Abs(local.y) / (fillV * tanV) - local.z;
            need = Mathf.Max(need, Mathf.Max(needX, needY));
        }
        return need;
    }

    // 기울기가 0에 가까우면 그 축은 화면에서 사라진 상태. 그냥 나누면 NaN이라 반드시 걸러야 한다.
    private float SafeDivide(float shift, float slope)
    {
        if (Mathf.Abs(slope) < 1e-5f)
        {
            return 0f;
        }
        return shift / slope;
    }

    // 맵 경계 상자의 8개 꼭짓점 좌표를 채운다.
    private void FillCorners(Bounds area)
    {
        Vector3 low = area.min;
        Vector3 high = area.max;
        corners[0] = new Vector3(low.x, low.y, low.z);
        corners[1] = new Vector3(low.x, low.y, high.z);
        corners[2] = new Vector3(low.x, high.y, low.z);
        corners[3] = new Vector3(low.x, high.y, high.z);
        corners[4] = new Vector3(high.x, low.y, low.z);
        corners[5] = new Vector3(high.x, low.y, high.z);
        corners[6] = new Vector3(high.x, high.y, low.z);
        corners[7] = new Vector3(high.x, high.y, high.z);
    }

    // 박스 8코너의 뷰포트 ndc 경계(±1이 화면 끝). 주어진 focus 시점 기준.
    private void ProjectBox(Vector3 focus, out Vector2 ndcMin, out Vector2 ndcMax)
    {
        Quaternion inv = Quaternion.Inverse(rot);
        Vector3 camPos = focus - rot * Vector3.forward * dist;
        ndcMin = new Vector2(float.MaxValue, float.MaxValue);
        ndcMax = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 ndc = ToNdc(inv * (corner - camPos));
            ndcMin = Vector2.Min(ndcMin, ndc);
            ndcMax = Vector2.Max(ndcMax, ndc);
        }
    }

    // 박스 중심의 ndc(유한차분 감도용).
    private Vector2 ProjectCenter(Vector3 focus)
    {
        Quaternion inv = Quaternion.Inverse(rot);
        Vector3 camPos = focus - rot * Vector3.forward * dist;
        return ToNdc(inv * (box.center - camPos));
    }

    // 카메라 기준 좌표를 화면 좌표(±1이 화면 끝)로 바꾼다.
    private Vector2 ToNdc(Vector3 local)
    {
        float depth = Mathf.Max(local.z, 0.01f);
        return new Vector2(local.x / (depth * tanH), local.y / (depth * tanV));
    }
}
