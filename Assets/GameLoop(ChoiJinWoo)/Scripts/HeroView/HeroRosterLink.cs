using UnityEngine;

// 로스터에서 배치된 유닛에 붙여 원본 엔트리를 기억해둔다.
// 이 유닛이 제거될 때 UnitRemover가 이 컴포넌트를 보고 엔트리를 Available로 되돌린다.
public class HeroRosterLink : MonoBehaviour
{
    public HeroRosterEntry Entry;

    // 이미 붙어 있으면 재사용하고 Entry만 갱신한다 — AddComponent를 매번 새로 부르면 풀에서
    // 재사용되는 유닛에 오래된 링크가 계속 쌓여 GetComponent가 예전 엔트리를 돌려주는 버그가 생긴다.
    public static void Attach(GameObject unit, HeroRosterEntry entry)
    {
        if (!unit.TryGetComponent(out HeroRosterLink link))
            link = unit.AddComponent<HeroRosterLink>();
        link.Entry = entry;
    }
}
