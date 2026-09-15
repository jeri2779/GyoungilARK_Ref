using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class FireReceiver
{
    private const string IgniteId = "Ignite_Tile_Debuff";
    private static readonly DotDebuffSO IgniteEffect = LoadEffect();
    private static readonly Dictionary<Component, int> Active = new();
    private static int nextToken;
    private static bool isNight;
    private static GameManager gameManager;

    // 플레이 재시작 시 점화 대상 기록을 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active.Clear();
        isNight = false;
    }

    // Map이 낮/밤 전환을 알려줄 때 부른다. 불은 밤에만 붙는다.
    public static void SetNight(bool value)
    {
        isNight = value;
    }

    // Map이 조립 시 한 번 넣어준다. 낮 전환 시 DotRegistry가 즉시 지우려면 필요하다.
    public static void SetGameManager(GameManager value)
    {
        gameManager = value;
    }

    // 불 타일에 들어온 대상의 점화 갱신을 시작한다.
    public static void ReceiveEntry(Tile tile, Component target)
    {
        if (CanIgnite(tile, target))
        {
            StartRefresh(tile, target);
        }
    }

    // 불 타일에서 나간 대상의 갱신을 멈추고 마지막 점화를 적용한다.
    public static void ReceiveExit(Tile tile, Component target)
    {
        if (CanIgnite(tile, target))
        {
            Active.Remove(target);
            ApplyEffect(target);
        }
    }

    // 지상 적에게 불 타일 점화를 적용할 수 있는지 확인한다.
    private static bool CanIgnite(Tile tile, Component target)
    {
        EnemyBase enemyBase = target.GetComponent<EnemyBase>();
        bool isFlying = enemyBase != null && enemyBase.IsFly;
        return tile.IsFire && isNight && isFlying == false && tile.Board.IsUnlocked;
    }

    // 대상을 등록하고 즉시 점화를 적용한다.
    private static void StartRefresh(Tile tile, Component target)
    {
        nextToken++;
        Active[target] = nextToken;
        ApplyEffect(target);
        RefreshEffect(tile, target, nextToken).Forget();
    }

    // 대상이 불 타일에 있는 동안 점화를 다시 적용한다.
    private static async UniTask RefreshEffect(Tile tile, Component target, int token)
    {
        while (CanRefresh(tile, target, token))
        {
            await UniTask.Delay(TimeSpan.FromSeconds(IgniteEffect.interval));

            if (CanRefresh(tile, target, token))
            {
                ApplyEffect(target);
            }
        }

        ReleaseTarget(target, token);
    }

    // 대상이 살아 있고, 현재 갱신 대상이고, 여전히 점화 조건을 만족하는지 확인한다.
    private static bool CanRefresh(Tile tile, Component target, int token)
    {
        return target != null && IsCurrent(target, token) && CanIgnite(tile, target);
    }

    // 실행 중인 번호표가 현재 대상의 번호표와 같은지 확인한다.
    private static bool IsCurrent(Component target, int token)
    {
        return Active.TryGetValue(target, out int activeToken)
            && activeToken == token;
    }

    // 종료된 갱신 작업의 대상 기록을 정리한다.
    private static void ReleaseTarget(Component target, int token)
    {
        if (IsCurrent(target, token))
        {
            Active.Remove(target);
        }
    }

    // 준비된 점화 데이터를 디버프 입구로 전달한다.
    private static void ApplyEffect(Component target)
    {
        DebuffApply.To(target, IgniteEffect, null, gameManager);
    }

    // Ignite_Basic 점화 데이터를 불러온다.
    private static DotDebuffSO LoadEffect()
    {
        DotDebuffSO effect = DebuffLoader.Get(IgniteId) as DotDebuffSO;

        if (effect == null)
        {
            throw new InvalidOperationException("점화 데이터가 없습니다.");
        }

        return effect;
    }
}
