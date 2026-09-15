using UnityEngine;

public class DayState : IState
{
    private GameManager gameManager;

    public DayState(GameManager manager)
    {
        gameManager = manager;
    }

    public void Enter()
    {
        gameManager.UiManager.ToggleGameSpeedUi(false);
        gameManager.ChangeCanBuild(true);
        gameManager.IncreaseDayCount();
        if (gameManager.DayCount > 0 && gameManager.DayCount % 10 == 0)
        {
            gameManager.ChangeRequest(true);
        }
        gameManager.todayHp = gameManager.Hp;
    }

    public void Exit()
    {
        gameManager.ChangeCanBuild(false);
    }

    public void Update() { }
}
