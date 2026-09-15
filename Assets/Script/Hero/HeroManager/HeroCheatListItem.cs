using System;

using UnityEngine;
using UnityEngine.UI;

// 테스트/치트 전용: HeroCheatSpawnUI 리스트뷰의 한 행. 영웅 아이콘+이름을 보여주고, 클릭 시 자신의 HeroData를 콜백으로 알린다.
public class HeroCheatListItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    private HeroData data;
    private Action<HeroData> onClick;

    public void Setup(HeroData data, Action<HeroData> onClick)
    {
        this.data = data;
        this.onClick = onClick;
        icon.sprite = data.Icon;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => this.onClick(this.data));
    }
}
