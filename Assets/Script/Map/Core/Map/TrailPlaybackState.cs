using System.Collections.Generic;
using UnityEngine;

// 트레일 재생의 반복/대기/1회 상태와 프레임별 이동을 관리한다.
public class TrailPlaybackState
{
    private readonly List<TrailRun> runs;
    private readonly float moveSpeed;
    private readonly float loopGap;

    private bool isLooping;
    private bool isPlaying;
    private bool isWaiting;
    private float elapsed;
    private float moveTime;

    // 재생할 TrailRun 목록과 이동 속도·반복 대기 시간을 받아 둔다.
    public TrailPlaybackState(List<TrailRun> runs, float moveSpeed, float loopGap)
    {
        this.runs = runs;
        this.moveSpeed = moveSpeed;
        this.loopGap = loopGap;
    }

    // 모든 Trail을 같은 시점에 재생하도록 초기화하고, 반복 여부를 지정한다.
    public void Begin(bool loop)
    {
        isLooping = loop;
        moveTime = GetMoveTime();
        if (moveTime <= 0f)
        {
            Stop();
            return;
        }

        elapsed = 0f;
        isWaiting = false;
        isPlaying = true;

        for (int i = 0; i < runs.Count; i++)
        {
            BeginRun(runs[i]);
        }
    }

    // 모든 Trail의 재생과 대기 상태를 정지한다.
    public void Stop()
    {
        isPlaying = false;
        isWaiting = false;
        elapsed = 0f;

        for (int i = 0; i < runs.Count; i++)
        {
            StopRun(runs[i]);
        }
    }

    // 공통 재생 상태에 따라 이동 또는 대기를 갱신한다.
    public void Tick(float deltaTime)
    {
        if (!isPlaying) return;

        if (isWaiting)
        {
            WaitRuns(deltaTime);
            return;
        }

        MoveRuns(deltaTime);
    }

    // Trail 하나를 경로 시작점에서 방출한다.
    private void BeginRun(TrailRun run)
    {
        if (run.Runner == null) return;

        run.Runner.position = run.Points[0];
        run.Trail.Clear();
        run.Trail.emitting = true;
        run.Trail.AddPosition(run.Points[0]);
        run.LastAdded = run.Points[0];
        run.Index = 1;
    }

    // 공통 진행률로 모든 Trail의 위치를 갱신한다.
    private void MoveRuns(float deltaTime)
    {
        elapsed += deltaTime;
        float progress = Mathf.Clamp01(elapsed / moveTime);

        for (int i = 0; i < runs.Count; i++)
        {
            MoveRun(runs[i], progress);
        }

        if (progress < 1f) return;
        EndRuns();
    }

    // Trail 하나를 진행률에 해당하는 경로 위치로 이동한다.
    private void MoveRun(TrailRun run, float progress)
    {
        if (run.Runner == null) return;

        float distance = run.Length * progress;
        for (int i = 1; i < run.Points.Count; i++)
        {
            Vector3 start = run.Points[i - 1];
            Vector3 target = run.Points[i];
            float length = Vector3.Distance(start, target);
            if (distance < length)
            {
                Vector3 position = Vector3.MoveTowards(start, target, distance);
                run.Runner.position = position;
                KeepPosition(run, position);
                return;
            }

            distance -= length;
            if (i < run.Index) continue;
            KeepPosition(run, target);
            run.Index = i + 1;
        }

        run.Runner.position = run.Points[run.Points.Count - 1];
    }

    // 직전에 넣은 점과 좌표가 다를 때만 Trail에 점을 넣는다.
    // 일시정지 중에는 같은 좌표가 매 프레임 들어오는데 Trail 수명은 스케일드 시간이라
    // 점이 사라지지도 않아, 막지 않으면 한 자리에 점이 무한히 쌓인다.
    private void KeepPosition(TrailRun run, Vector3 position)
    {
        if (run.LastAdded == position) return;

        run.Trail.AddPosition(position);
        run.LastAdded = position;
    }

    // 모든 Trail의 방출을 동시에 끝내고 반복 여부를 처리한다.
    private void EndRuns()
    {
        for (int i = 0; i < runs.Count; i++)
        {
            runs[i].Trail.emitting = false;
        }

        if (!isLooping)
        {
            isPlaying = false;
            return;
        }

        elapsed = 0f;
        isWaiting = true;
    }

    // 공통 반복 대기 후 모든 Trail을 다시 시작한다.
    private void WaitRuns(float deltaTime)
    {
        elapsed += deltaTime;
        if (elapsed < loopGap) return;
        Begin(isLooping);
    }

    // 가장 긴 경로를 기준으로 공통 이동 시간을 계산한다.
    private float GetMoveTime()
    {
        if (moveSpeed <= 0f) return 0f;

        float maxLength = 0f;
        for (int i = 0; i < runs.Count; i++)
        {
            maxLength = Mathf.Max(maxLength, runs[i].Length);
        }

        return maxLength / moveSpeed;
    }

    // Trail 하나의 방출과 화면 잔상을 초기화한다.
    private void StopRun(TrailRun run)
    {
        if (run.Trail == null) return;

        run.Trail.emitting = false;
        run.Trail.Clear();
    }
}
