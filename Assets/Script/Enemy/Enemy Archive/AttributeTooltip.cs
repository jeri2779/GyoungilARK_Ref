using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// 특성 텍스트(e_Attribute)의 단어(<link>)에 마우스를 hoverDelay초 이상 올리면 설명 툴팁을 띄운다.
//  - 단어는 EnemyInfo가 <link="ID">...</link> 로 감싸준다.
//  - ID가 EnemyAttribute 이름이면 StringTable의 "<특성이름>_Desc" 키를 쓴다.
//  - ID가 스킬 ID면(고유 특성으로 표기한 스킬) SkillTable 그 행의 Desc 키를 쓴다.
//    스킬 쪽은 키 규약이 "Desc_<스킬ID>"라 특성과 반대이므로, 규약을 맞추려 하지 않고
//    SkillTable에 적힌 키를 그대로 읽는다 — CSV가 단일 출처가 되고 규약이 또 갈리지 않는다.
public class AttributeTooltip : MonoBehaviour
{
    [SerializeField] private TMP_Text attributeText;   // 특성이 표시되는 TMP (e_Attribute)
    [SerializeField] private GameObject tooltip;       // 툴팁 패널 (켜고 끔)
    [SerializeField] private TMP_Text tooltipText;     // 툴팁 안 설명 텍스트
    [SerializeField] private float hoverDelay = 1f;    // 몇 초 머무르면 뜨는지

    private Camera uiCamera;   // 오버레이 캔버스면 null
    private int lastLink = -1; // 현재 머무르는 link 인덱스
    private float hoverTimer;
    private bool shown;

    void Awake()
    {
        Hide();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;
    }

    void OnDisable() => ResetHover();

    void Update()
    {
        if (attributeText == null || Mouse.current == null) { ResetHover(); return; }

        Vector2 mouse = Mouse.current.position.ReadValue();
        int link = TMP_TextUtilities.FindIntersectingLink(attributeText, mouse, uiCamera);

        if (link == -1)          // 특성 단어 위가 아님
        {
            ResetHover();
            return;
        }

        if (link != lastLink)    // 다른 특성으로 이동 → 타이머 리셋
        {
            lastLink = link;
            hoverTimer = 0f;
            if (shown) Hide();
        }

        hoverTimer += Time.unscaledDeltaTime;
        if (!shown && hoverTimer >= hoverDelay)
            ShowFor(link);
    }

    private void ShowFor(int linkIndex)
    {
        string id = attributeText.textInfo.linkInfo[linkIndex].GetLinkID();   // 예: "Cloaking", "FireZoneSkill"
        if (tooltipText != null) tooltipText.text = DataTableManager.StringTable.Get(DescKeyFor(id));
        if (tooltip != null) tooltip.SetActive(true);   // 위치는 씬/인스펙터에 고정해둔 그대로
        shown = true;
    }

    // link ID → 설명 StringTable 키.
    // 특성을 먼저 본다 — 기존 특성 거동을 그대로 두고, 특성이 아닌 ID만 스킬로 넘긴다.
    private static string DescKeyFor(string id)
    {
        if (System.Enum.TryParse(id, true, out EnemyAttribute _)) return $"{id}_Desc";

        SkillTable.Data skill = DataTableManager.SkillTable?.Get(id);
        // Desc 칸이 비어 있으면 null이 되고, StringTable.Get(null)은 Dictionary 조회에서 예외를 던진다.
        // 스킬표에 없거나 Desc가 비면 기존 규약으로 돌린다 — 키가 없으면 StringTable이 UnKnown을 돌려준다.
        return !string.IsNullOrEmpty(skill?.Desc) ? skill.Desc : $"{id}_Desc";
    }

    private void ResetHover()
    {
        lastLink = -1;
        hoverTimer = 0f;
        if (shown) Hide();
    }

    private void Hide()
    {
        if (tooltip != null) tooltip.SetActive(false);
        shown = false;
    }
}
