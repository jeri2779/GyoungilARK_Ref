using System;
using UnityEngine;
using VContainer.Unity;

// 낮에 일어난 자원·시민·영웅·강화 변경을 모아뒀다가, 일정 주기마다 한 번만 저장한다
public class SaveChangeTracker : IStartable, ITickable, IDisposable
{
    private const float SaveIntervalSeconds = 30f; // 마지막 저장 이후 이만큼(실제 시간) 지나야 다음 저장을 시도한다

    private readonly SaveManager saveManager;
    private readonly ResourcesManager resourcesManager;
    private readonly CitizenManager citizenManager;
    private readonly HeroRoster heroRoster;
    private readonly HeroTierUpgradeState tierState;
    private readonly HeroClassUpgradeState classState;
    private bool changePending;
    private float lastSaveTime;

    // 저장을 맡길 관리자와 변경 신호를 보내는 시스템들을 받아 둔다
    public SaveChangeTracker(
        SaveManager saveManager,
        ResourcesManager resourcesManager,
        CitizenManager citizenManager,
        HeroRoster heroRoster,
        HeroTierUpgradeState tierState,
        HeroClassUpgradeState classState)
    {
        this.saveManager = saveManager;
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this.heroRoster = heroRoster;
        this.tierState = tierState;
        this.classState = classState;
    }

    // 감시할 변경 신호들을 구독한다
    public void Start()
    {
        resourcesManager.ProductUpdate += MarkChanged;
        citizenManager.CitizenChanged += MarkChanged;
        heroRoster.Changed += MarkChanged;
        tierState.LevelChanged += MarkLevelChanged;
        classState.LevelChanged += MarkLevelChanged;
        lastSaveTime = Time.unscaledTime;
    }

    // 감시하던 변경 신호 구독을 해제한다
    public void Dispose()
    {
        resourcesManager.ProductUpdate -= MarkChanged;
        citizenManager.CitizenChanged -= MarkChanged;
        heroRoster.Changed -= MarkChanged;
        tierState.LevelChanged -= MarkLevelChanged;
        classState.LevelChanged -= MarkLevelChanged;
    }

    // 모아 둔 변경이 있고, 마지막 저장에서 일정 시간이 지났으면 낮 활동 상태를 저장한다
    public void Tick()
    {
        if (!changePending)
        {
            return;
        }

        if (Time.unscaledTime - lastSaveTime < SaveIntervalSeconds)
        {
            return;
        }

        changePending = false;
        lastSaveTime = Time.unscaledTime;
        saveManager.SaveDayActive();
    }

    // 변경이 생겼다고 표시한다
    private void MarkChanged()
    {
        changePending = true;
    }

    // 강화 레벨 변경을 변경 표시로 넘긴다
    private void MarkLevelChanged(int changedIndex)
    {
        changePending = true;
    }
}
