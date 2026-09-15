using System;
using UnityEngine;
using VContainer.Unity;

// 프로그램이 꺼지는 순간을 붙잡아 마지막 저장을 부른다
public class SaveExitHook : IStartable, IDisposable
{
    private readonly SaveManager saveManager;

    // 종료 순간에 저장을 맡길 저장 관리자를 받아 둔다
    public SaveExitHook(SaveManager saveManager)
    {
        this.saveManager = saveManager;
    }

    // 프로그램 종료 신호를 구독한다
    public void Start()
    {
        Application.quitting += SaveOnQuitting;
    }

    // 프로그램 종료 신호 구독을 해제한다
    public void Dispose()
    {
        Application.quitting -= SaveOnQuitting;
    }

    // 종료 직전 낮 활동 상태를 저장한다
    private void SaveOnQuitting()
    {
        saveManager.SaveDayActive();
    }
}
