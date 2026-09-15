using UnityEngine;

// HeroSkillCastController의 자매 클래스. "시전자(영웅) 선택" 대신 UI 버튼으로 스킬을 무장(arm)한 뒤
// 필드 타일 클릭으로 장판을 심는다. PlaceAction.SelectTile을 통해 매 클릭마다 호출되며,
// MapAssemble이 조립한다.
public class PlayerSkillCastController
{
    public DayNightBuildRule dayNightRule;
    public BuffManager buffManager;
    public PlayerManaManager mana;
    //public HeroSkillCastController heroSkillCast; // 영웅 스킬 시전과 상호 배타 처리용

    // Hero.GroundZoneLift(0.02f)와 동일한 값 — 알파블렌드 장판 VFX가 바닥과 z-fighting하는 것을 막는다.
    private const float GroundZoneLift = 0.02f;

    private PlayerSkillSlot armed;
    public PlayerSkillSlot Armed => armed;

    public void ArmSkill(PlayerSkillSlot slot)
    {
        armed = slot;
        //heroSkillCast?.ClearSelection(); // 영웅 스킬 시전 중이었다면 취소한다(같은 클릭이 두 컨트롤러에 겹치지 않도록)
    }

    public void ClearArmed() => armed = null;

    // true를 반환하면 이 클릭을 소비했다는 뜻 — PlaceAction은 HeroSkillCastController로 넘기지 않는다.
    public bool HandleClick(Tile tile)
    {
        if (armed == null || tile == null) return false;
        if (dayNightRule.CanBuild()) return true; // 낮: 무장은 유지하되 아무 일도 하지 않는다
        if (!mana.TryConsume(armed.manaCost)) return true; // 마나 부족: 무장은 유지한다

        GameObject go = Object.Instantiate(armed.zonePrefab, tile.WorldTop + Vector3.up * GroundZoneLift, armed.zonePrefab.transform.localRotation);
        if (go.TryGetComponent(out PlayerGroundZoneEffect zone))
            zone.Init(tile.Board, buffManager, gameManager: dayNightRule?.rule);

        armed = null;
        return true;
    }
}
