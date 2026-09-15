using System;
using System.Collections.Generic;

// 트레일 달린 모듈을 모아 낮/밤 전환에 맞춰 재생 여부를 스스로 판단하고 실행한다.
public class TrailNightController
{
    private readonly DayNightData dayNightData;
    private readonly List<PathTrail> trails = new();
    private readonly List<ModuleLogic> modules = new();
    private readonly List<Action<ModuleState>> handlers = new();

    // 지금 낮/밤 여부를 물어볼 장부 참조를 받아 둔다.
    public TrailNightController(DayNightData dayNightData)
    {
        this.dayNightData = dayNightData;
    }

    // 트레일 달린 모듈을 등록하고, 늦게 준비 상태가 되는 경우를 대비해 구독한다.
    public void Collect(ModuleLogic module, PathTrail trail)
    {
        Action<ModuleState> handler = state => HandleModuleState(trail, state);
        modules.Add(module);
        trails.Add(trail);
        handlers.Add(handler);
        module.OnStateChanged += handler;
        HandleModuleState(trail, module.CurrentState);
    }

    // 모듈이 새로 준비 상태가 되면, 지금 낮이면 반복재생, 밤이면 1회재생으로 맞춘다.
    private void HandleModuleState(PathTrail trail, ModuleState state)
    {
        if (state != ModuleState.Preparing) return;

        if (!dayNightData.IsNight)
        {
            trail.PlayLoop();
            return;
        }

        trail.PlayOnce();
    }

    // 밤 전환이 시작되면 모든 트레일을 즉시 멈춘다.
    public void StopAll()
    {
        for (int index = 0; index < trails.Count; index++)
        {
            trails[index].StopTrail();
        }
    }

    // 등록해둔 모든 모듈 구독을 해제한다.
    public void Release()
    {
        for (int index = 0; index < modules.Count; index++)
        {
            modules[index].OnStateChanged -= handlers[index];
        }
    }
}
