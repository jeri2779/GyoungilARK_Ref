using System;
using UnityEngine;
using UnityEngine.UI;

// 스킬 버튼 3개를 마나링 아이콘 위치에서 펼치거나 접는 조이스틱형 연출. 좌표는 애니메이션 클립이 갖고, 이 스크립트는 재생만 담당한다.
[RequireComponent(typeof(Animator))]
public class SkillRadialToggle : MonoBehaviour
{
    [SerializeField] private Button icon;

    private static readonly int SpreadHash = Animator.StringToHash("SkillSpread");
    private static readonly int CollapseHash = Animator.StringToHash("SkillCollapse");
    private Animator anim;
    private bool spread;
    // Play()로 시간을 순간이동시키면 Unity가 지나친 구간의 애니메이션 이벤트를 같이 발화시킨다 -
    // ResetCollapsedNow()가 그 부작용으로 Collapsed를 잘못 쏘는 걸 막는 데 쓴다.
    private bool suppressNextCollapsedEvent;

    // 지금 펼쳐진 상태인지 - PlayerSkillPanel이 낮 전환 시 접힘 연출을 재생할지 판단하는 데 쓴다.
    public bool IsSpread => spread;

    // 접힘 연출이 끝났을 때 발화한다 - PlayerSkillPanel이 구독해 그제서야 패널을 내린다.
    public event Action Collapsed;

    // 같은 오브젝트의 Animator를 보관하고 아이콘 클릭을 연결한다.
    private void Awake()
    {
        anim = GetComponent<Animator>();
        icon.onClick.AddListener(Toggle);
    }

    // 아이콘 클릭 시 펼침/접힘을 전환한다.
    private void Toggle()
    {
        if (spread)
        {
            Collapse();
        }
        else
        {
            Spread();
        }
    }

    // 스킬 버튼 3개를 펼친다.
    public void Spread()
    {
        anim.Play(SpreadHash, 0, 0f);
        spread = true;
    }

    // 스킬 버튼 3개를 접는다.
    public void Collapse()
    {
        anim.Play(CollapseHash, 0, 0f);
        spread = false;
    }

    // 연출 없이 즉시 접힌 상태로 되돌린다 - 밤이 시작될 때마다 매번 초기화하는 데 쓴다.
    public void ResetCollapsedNow()
    {
        suppressNextCollapsedEvent = true;
        anim.Play(CollapseHash, 0, 1f);
        spread = false;
    }

    // SkillCollapse 애니메이션 이벤트가 접힘 연출이 끝난 시점에 호출한다 - 같은 오브젝트의
    // Animator가 재생하므로 이벤트는 여기(SkillRadialToggle)로 와야 찾을 수 있다. ResetCollapsedNow()의
    // 순간이동으로 인해 잘못 발화된 경우는 걸러낸다.
    public void NotifyCollapsed()
    {
        if (suppressNextCollapsedEvent)
        {
            //suppressNextCollapsedEvent = false;
            return;
        }
        //Collapsed?.Invoke();
    }
}
