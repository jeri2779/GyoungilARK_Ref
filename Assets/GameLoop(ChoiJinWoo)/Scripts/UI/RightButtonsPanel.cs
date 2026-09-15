using UnityEngine;

// OpenBaseUI/EnemyArchiveButton/HeroArchiveButton을 담은 RightButtons 그룹을
// 밤에는 통째로 숨기고 낮이 되면 다시 보여준다.
public class RightButtonsPanel : MonoBehaviour
{
    [SerializeField] private MapGame game;

    private void Start()
    {
        game.Rule.ChangeToNight += Hide;
        game.EnviromentManager.OnDay += Show;
    }

    private void OnDestroy()
    {
        game.Rule.ChangeToNight -= Hide;
        game.EnviromentManager.OnDay -= Show;
    }

    private void Hide() => gameObject.SetActive(false);
    private void Show() => gameObject.SetActive(true);
}
