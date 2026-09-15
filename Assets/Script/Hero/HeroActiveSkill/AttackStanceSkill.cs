using System;
using System.Threading;
using Cysharp.Threading.Tasks;

// 스탠스 교체 스킬 — 지속시간 동안 basePattern[0]를 다른 AttackDataSO로 바꿔치기한다.
// Continuous 타입 데이터로 바꾸면 그 순간부터 채널링형으로, 원래 데이터로 돌아오면 자동 원상복구.
public class AttackStanceSkill : HeroActiveSkill
{
    public AttackDataSO stanceAttackData;

    public override async UniTask Execute(Tile targetTile, CancellationToken token)
    {
        AttackDataSO previous = hero.SwapPrimaryAttackData(stanceAttackData);
        try { await UniTask.Delay(TimeSpan.FromSeconds(Math.Max(duration, 0.01f)), cancellationToken: token); }
        finally { hero.SwapPrimaryAttackData(previous); }
    }
}
