using UnityEngine;

public class GameOverState : IState
{
    private GameManager gameManager;
    private UpgradeState upgradeState;
    private UiManager UiManager;
    private readonly SlotDelete slotDelete = new SlotDelete();

    public GameOverState(GameManager gameManager, UpgradeState upgradeState, UiManager uiManager)
    {
        this.gameManager = gameManager;
        this.upgradeState = upgradeState;
        UiManager = uiManager;
    }

    public void Enter()
    {
        if(Time.timeScale > 0f)
            Time.timeScale = 0f;
        gameManager.isGameOver = true;

        int earnedPoints = Mathf.RoundToInt(gameManager.DayCount * 0.3f);
        UiManager.OpenGameOverUI(gameManager.DayCount, earnedPoints);
        upgradeState.AddPoints(earnedPoints);
        DeleteSaveSlot();
    }

    // 판이 끝났으니 이 슬롯의 세이브 파일을 지운다 (같은 밤을 다시 치르며 업그레이드 보상을 반복 수확하지 못하게).
    private void DeleteSaveSlot()
    {
        if (!SaveManager.ToolEnabled) return;
        if (slotDelete.TryDelete(SelectedSaveSlot.SlotId)) return;

        Debug.LogError($"[SaveLoad] 게임오버 후 Slot {SelectedSaveSlot.SlotId} 삭제에 실패했습니다.");
    }

    public void Exit()
    {
        Time.timeScale = 1f;
        gameManager.isGameOver = false;
    }

    public void Update()
    {

    }
}
