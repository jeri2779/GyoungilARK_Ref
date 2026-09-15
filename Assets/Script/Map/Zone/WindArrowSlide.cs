using System.Collections.Generic;
using UnityEngine;

// 낮 안내 화살표를 바람 방향으로 밀어내고 끝에서 흐려지게 하는 출력 전담.
public class WindArrowSlide : MonoBehaviour
{
    private readonly List<SpriteRenderer> targets = new();
    private readonly List<Vector3> origins = new();

    private Vector3 slideDirection;
    private float slideDistance;
    private float slidePeriod;
    private Color baseColor;
    private float elapsedTime;

    // 밀어낼 거리와 한 바퀴 시간, 화살표 기본 색을 보관하고 멈춘 채로 대기한다.
    public void SetMotion(float distance, float period, Color color)
    {
        slideDistance = distance;
        slidePeriod = period;
        baseColor = color;
        enabled = false;
    }

    // 이번 낮에 켜진 화살표와 그 기준 자리를 받아 밀어내기를 시작한다.
    public void PlayArrows(IReadOnlyList<SpriteRenderer> arrows, IReadOnlyList<Vector3> spots, Vector3 direction)
    {
        targets.Clear();
        origins.Clear();
        KeepAll(arrows, spots);
        slideDirection = direction;
        elapsedTime = 0f;
        enabled = true;
    }

    // 밀어내기를 멈춘다.
    public void StopArrows()
    {
        enabled = false;
    }

    // 흐른 시간에 맞춰 모든 화살표의 자리와 진하기를 갱신한다.
    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float progress = WindArrowSlideCalc.ReadProgress(elapsedTime, slidePeriod);
        Vector3 offset = slideDirection * WindArrowSlideCalc.ReadOffset(progress, slideDistance);
        float alpha = WindArrowSlideCalc.ReadAlpha(progress);
        ApplyEach(offset, alpha);
    }

    // 받은 화살표와 기준 자리를 나란히 보관한다.
    private void KeepAll(IReadOnlyList<SpriteRenderer> arrows, IReadOnlyList<Vector3> spots)
    {
        for (int index = 0; index < arrows.Count; index++)
        {
            targets.Add(arrows[index]);
            origins.Add(spots[index]);
        }
    }

    // 화살표마다 밀린 자리와 진하기를 넣는다.
    private void ApplyEach(Vector3 offset, float alpha)
    {
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        for (int index = 0; index < targets.Count; index++)
        {
            targets[index].transform.position = origins[index] + offset;
            targets[index].color = color;
        }
    }
}
