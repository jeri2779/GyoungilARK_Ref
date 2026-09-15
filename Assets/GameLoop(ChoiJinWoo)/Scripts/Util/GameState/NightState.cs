using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class NightState : IState
{
    private GameManager gameManager;
    
    public NightState(GameManager manager)
    {
        this.gameManager = manager;
    }

    public void Enter()
    {
        gameManager.perfactDefence = false;
        Night().Forget();
    }

    public void Exit()
    {
        gameManager.ChangeCanSpawnEnemy(false);
        if (gameManager.Hp == gameManager.todayHp)
            gameManager.perfactDefence = true;
    }

    public void Update()
    {
        
    }

    private async UniTaskVoid Night()
    {
        await UniTask.WaitUntil(() => gameManager.CanSpawnEnemy);
        gameManager.UiManager.ToggleGameSpeedUi(true);
        gameManager.SpawnEnemy();
    }
}
