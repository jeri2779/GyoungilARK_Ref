using System;
using UnityEngine;
using UnityEngine.UI;

// 도감 목록의 버튼 한 칸. EnemyArchive가 프리팹을 인스턴스화한 뒤 Set()으로 값을 채운다.
//  - 등급(Class)에 따라 테두리(border) 색
//  - 해금 여부에 따라 아이콘(icon) 스프라이트
// 프리팹 하이어라키 권장 구조:
//   Enemy (Button + 이 스크립트)
//    ├─ Border (Image)  ← 아이콘보다 위(먼저 그려짐=뒤), 살짝 크게
//    └─ Icon   (Image)  ← 아래(나중=앞)
public class EnemyArchiveButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;    // 적 아이콘 (앞)
    [SerializeField] private Image border;  // 등급 색 테두리 (뒤)

    [Header("등급별 테두리 색")]
    [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color eliteColor  = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField] private Color bossColor   = new Color(0.9f, 0.2f, 0.2f);

    [Header("미해금 처리")]
    [Tooltip("체크 시 미해금 적은 등급 색 대신 회색 테두리(등급 스포일러 방지)")]
    [SerializeField] private bool grayWhenLocked = true;
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f);

    private EnemyTable.Data data;
    private Sprite lockIcon;
    private Action<EnemyTable.Data> onClick;

    public void Set(EnemyTable.Data data, Sprite lockIcon, Action<EnemyTable.Data> onClick)
    {
        this.data = data;
        this.lockIcon = lockIcon;
        this.onClick = onClick;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => this.onClick?.Invoke(this.data));
        }
        Refresh();
    }

    // 해금 상태/등급을 화면에 반영 (해금될 때 EnemyArchive가 다시 호출)
    public void Refresh()
    {
        if (data == null) return;
        bool unlocked = EnemyArchiveData.IsUnlocked(data.Name);

        if (icon != null)
        {
            // 해금 → 실제 아이콘, 미해금 → ? 이미지. 못 찾으면 기존 이미지 유지(개발 중 빈 이미지 방지)
            Sprite sprite = unlocked ? Resources.Load<Sprite>($"EnemyIcons/{data.Name}") : lockIcon;
            border.gameObject.SetActive(unlocked);
            if (sprite != null) icon.sprite = sprite;   
        }

        if (border != null)
            border.color = (!unlocked && grayWhenLocked) ? lockedColor : ClassColor(data.Class);
    }

    private Color ClassColor(string cls)
    {
        if (Enum.TryParse(cls, true, out EnemyClass c))
        {
            switch (c)
            {
                case EnemyClass.Boss:  return bossColor;
                case EnemyClass.Elite: return eliteColor;
            }
        }
        return normalColor;   // Normal 또는 파싱 실패
    }
}
