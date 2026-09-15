using System.Collections.Generic;
using UnityEngine;

// 도감 목록. EnemyTable의 모든 적을 순회하며 버튼 프리팹을 content에 하나씩 생성한다.
// 각 버튼(EnemyArchiveButton)이 해금 여부·등급 색을 스스로 그린다.
public class EnemyArchive : MonoBehaviour
{
    [Tooltip("적 하나당 생성할 버튼 프리팹 (EnemyArchiveButton 붙은 것)")]
    [SerializeField] private EnemyArchiveButton enemyButtonPrefab;
    [Tooltip("버튼들이 담길 부모 (Scroll View의 Content 등)")]
    [SerializeField] private Transform content;
    [Tooltip("클릭 시 상세정보를 출력할 패널")]
    [SerializeField] private EnemyInfo infoPanel;
    [Tooltip("미해금 적 버튼에 표시할 ? 이미지")]
    public Sprite lockIcon;
    [Tooltip("도감을 처음 열었을 때 띄울 적. EnemyTable에 없으면 표의 첫 적으로 폴백한다.")]
    [SerializeField] private string defaultEnemyKey = "Chicken";

    private bool built;
    private readonly Dictionary<string, EnemyArchiveButton> buttons = new();   // enemyKey → 버튼
    // 마지막으로 펼쳐 본 적. 도감을 닫아도 이 오브젝트는 비활성화만 되므로 값이 살아남는다.
    private EnemyTable.Data lastShown;

    
    void OnEnable()
    {
        Build();
        EnemyArchiveData.OnUnlocked += HandleUnlocked;
        RefreshAll();   // 도감이 닫혀있는 동안 해금된 것도 열 때 반영
    }

    void OnDisable()
    {
        EnemyArchiveData.OnUnlocked -= HandleUnlocked;
    }

    private void Build()
    {
        if (built) return;

        var enemyTable = DataTableManager.EnemyTable;
        if (enemyTable == null || enemyButtonPrefab == null || content == null)
        {
            Debug.LogWarning("EnemyArchive: 참조 누락 — EnemyTable / 버튼프리팹 / Content 를 확인하세요.");
            return;
        }
        built = true;

        foreach (var data in enemyTable.GetAll())
        {
            EnemyArchiveButton btn = Instantiate(enemyButtonPrefab, content);
            btn.name = $"EnemyBtn_{data.Name}";
            btn.Set(data, lockIcon, OnClickEnemy);
            buttons[data.Name] = btn;
        }
    }

    private void OnClickEnemy(EnemyTable.Data data)
    {
        if (infoPanel == null) return;
        // 책이 펼쳐지는 중엔 무시한다 — 도중에 다른 적으로 갈아끼우면 펼침 애니가 끊겨 보인다.
        if (infoPanel.IsBookOpening) return;
        // 미해금 ? 이미지는 목록 버튼과 같은 것을 쓴다 — 인스펙터에 두 번 꽂지 않게 여기서 넘겨준다.
        infoPanel.SetLockIcon(lockIcon);
        infoPanel.Info(data);
        lastShown = data;   // 다음에 도감을 열 때 이 페이지로 돌아온다
        EnemySoundManager.Play("BookPage");
    }

    // 도감을 (특정 적 지정 없이) 열 때 호출한다. 마지막으로 보던 적, 없으면 기본 적을 펼친다.
    // OnClickEnemy를 거치지 않는 이유:
    //  - 그쪽은 페이지 넘김 효과음을 내는데, 도감을 여는 순간엔 BookOpen이 이미 재생 중이라 겹친다.
    //  - IsBookOpening 가드에 걸려 조용히 아무것도 안 하는 경우가 생긴다(닫자마자 다시 열 때).
    public void ShowLastOrDefault()
    {
        if (infoPanel == null) return;
        Build();   // 열기 애니메이션보다 먼저 불릴 수 있으니 목록을 확보해둔다(이미 지었으면 무시됨)

        EnemyTable.Data data = lastShown ?? DefaultData();
        if (data == null) return;

        infoPanel.SetLockIcon(lockIcon);
        infoPanel.Info(data);
        lastShown = data;
    }

    // 기본 적. 키가 표에 없으면(인스펙터 오타 등) 표의 첫 적으로 폴백해 빈 화면을 피한다.
    private EnemyTable.Data DefaultData()
    {
        EnemyTable table = DataTableManager.EnemyTable;
        if (table == null) return null;

        EnemyTable.Data data = string.IsNullOrEmpty(defaultEnemyKey) ? null : table.Get(defaultEnemyKey);
        if (data != null) return data;

        foreach (var first in table.GetAll()) return first;
        return null;
    }

    // 외부(스테이지 정보 툴팁의 도감 버튼)에서 특정 적 페이지를 바로 띄울 때.
    // 목록에서 그 적 버튼을 누른 것과 같은 상태가 된다.
    public void ShowEnemy(EnemyTable.Data data)
    {
        if (data == null) return;
        Build();   // OnEnable 전에 불릴 수도 있으니 목록을 먼저 확보한다(이미 지었으면 무시됨)
        OnClickEnemy(data);
    }

    private void RefreshAll()
    {
        foreach (var b in buttons.Values) b.Refresh();
    }

    // 새로 해금될 때 해당 버튼만 즉시 갱신 (도감이 열려있는 동안 실시간)
    private void HandleUnlocked(string key)
    {
        if (buttons.TryGetValue(key, out var b)) b.Refresh();
    }
}
