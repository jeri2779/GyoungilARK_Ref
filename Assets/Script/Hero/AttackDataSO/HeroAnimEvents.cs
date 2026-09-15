using System.Collections.Generic;
using UnityEngine;

public class HeroAnimEvents : MonoBehaviour
{
    private Dictionary<string, System.Action> handlers = new();
    public void Subscribe(string name, System.Action h)
    {
        if (handlers.ContainsKey(name)) handlers[name] += h;
        else handlers[name] = h;
    }

    public void Unsubscribe(string name, System.Action h)
    {
        if (handlers.ContainsKey(name)) handlers[name] -= h;
    }

    public void AnimEvent_Telegraph() => handlers.GetValueOrDefault("Telegraph")?.Invoke();
    public void AnimEvent_Attack() => handlers.GetValueOrDefault("Attack")?.Invoke();
    public void AnimEvent_Recovery() => handlers.GetValueOrDefault("Recovery")?.Invoke();
}

// 전체 인구수 89 / 100
// 건물마다 작업중 인원 5 / 7
// 작업 중 70 / 놀고 있음 19