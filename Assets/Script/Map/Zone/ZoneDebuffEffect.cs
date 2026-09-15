using System.Collections.Generic;
using UnityEngine;

// 지대 디버프의 공통 적용/제거/잠금 규칙을 담당하는 베이스입니다.
public abstract class ZoneDebuffEffect : IZoneEffect
{
    protected readonly MapBoard board;
    private readonly PlacedUnitData unitList;
    private readonly DebuffSO[] debuffs;
    private readonly object[] sources;

    // 이 지대의 보드와 디버프 목록을 보관하고, 디버프마다 독립적인 적용 출처를 만듭니다.
    protected ZoneDebuffEffect(MapBoard board, PlacedUnitData unitList, DebuffSO[] debuffs)
    {
        this.board = board;
        this.unitList = unitList;
        this.debuffs = debuffs;
        sources = CreateSources(debuffs);
    }

    // 보드가 잠겨있으면 낮 반응 자체를 실행하지 않습니다.
    public void OnDayChanged()
    {
        if (!board.IsUnlocked)
        {
            return;
        }

        RunDay();
    }

    // 보드가 잠겨있으면 밤 반응 자체를 실행하지 않습니다.
    public void OnNightChanged()
    {
        if (!board.IsUnlocked)
        {
            return;
        }

        RunNight();
    }

    // 잠금 확인을 통과했을 때만 실행되는 낮 반응입니다.
    protected abstract void RunDay();

    // 잠금 확인을 통과했을 때만 실행되는 밤 반응입니다.
    protected abstract void RunNight();

    // 이 지대의 디버프를 영웅에게 적용합니다.
    protected void Apply(Hero hero)
    {
        ApplyEffects(hero, debuffs, sources);
    }

    // 이 지대가 건 디버프만 영웅에게서 제거합니다.
    protected void Remove(Hero hero)
    {
        RemoveEffects(hero, debuffs, sources);
    }

    // 이 지대 보드에 걸려있던 디버프를 전원에게서 제거합니다.
    protected void ClearBoard()
    {
        BoardHeroes heroes = ReadHeroes();
        RemoveEach(heroes);
    }

    // 이 지대 보드에 서 있는 영웅과 자리를 매번 새로 훑어 모읍니다. 배치 장부가 매번 달라져 다시 읽어야 합니다.
    protected BoardHeroes ReadHeroes()
    {
        BoardHeroes heroes = new();
        for (int unitIndex = 0; unitIndex < unitList.Count; unitIndex++)
        {
            GameObject unit = unitList.UnitAt(unitIndex);
            PlacementArea area = unitList.AreaAt(unitIndex);
            KeepHero(heroes, unit, area);
        }

        return heroes;
    }

    // 이 지대 보드 소속 영웅만 골라 담습니다.
    private void KeepHero(BoardHeroes heroes, GameObject unit, PlacementArea area)
    {
        if (area.Board != board)
        {
            return;
        }

        if (!unit.TryGetComponent(out Hero hero))
        {
            return;
        }

        heroes.Heroes.Add(hero);
        heroes.Areas.Add(area);
    }

    // 훑은 영웅 전원에게서 이 지대 디버프를 제거합니다.
    private void RemoveEach(BoardHeroes heroes)
    {
        for (int index = 0; index < heroes.Heroes.Count; index++)
        {
            Remove(heroes.Heroes[index]);
        }
    }

    // 디버프 목록을 영웅에게 출처별로 적용합니다.
    private static void ApplyEffects(Hero hero, DebuffSO[] debuffs, object[] sources)
    {
        for (int effectIndex = 0; effectIndex < debuffs.Length; effectIndex++)
        {
            DebuffSO debuff = debuffs[effectIndex];
            DebuffApply.To(hero, debuff, hero.Buffs, durationOverride: float.PositiveInfinity, source: sources[effectIndex]);
        }
    }

    // 특정 지대가 영웅에게 건 디버프만 제거합니다.
    private static void RemoveEffects(Hero hero, DebuffSO[] debuffs, object[] sources)
    {
        for (int effectIndex = 0; effectIndex < debuffs.Length; effectIndex++)
        {
            DotRegistry.Remove(hero, debuffs[effectIndex].type);
            hero.Buffs.RemoveBuffs(hero, sources[effectIndex]);
            DebuffEffectView.Remove(hero, debuffs[effectIndex].type, sources[effectIndex]);
        }
    }

    // 디버프마다 독립적인 적용 출처를 만듭니다.
    private static object[] CreateSources(DebuffSO[] debuffs)
    {
        object[] sources = new object[debuffs.Length];
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            sources[sourceIndex] = new object();
        }

        return sources;
    }

    // 지대 보드에 서 있는 영웅과 자리를 나란히 보관합니다.
    protected sealed class BoardHeroes
    {
        public readonly List<Hero> Heroes = new();
        public readonly List<PlacementArea> Areas = new();
    }
}
