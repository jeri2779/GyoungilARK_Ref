using TMPro;
using UnityEngine;

/// <summary>
/// 보스 체력바 패널의 스탯 숫자(체력 / 공격력 / 방어력)만 담당한다.
/// EnemyBase가 소유하고 매 프레임 Tick으로 굴린다(EnemyHealthBar와 같은 구조).
/// 세 개 다 안 꽂은 프리팹이면 Setup/Tick/Reset이 전부 조용히 no-op(일반 적 전부 해당). 일부만 꽂아도 된다.
///
/// 값이 바뀐 것만 갱신하는 게 핵심이다 — 체력은 재생·독 틱으로 계속 움직이고 Tick은 매 프레임 돌기 때문에,
/// 그냥 대입하면 프레임마다 문자열을 새로 만들고 TMP 메시를 다시 빌드한다.
/// 정수로 끊어 캐시와 비교하므로, 실제로 표시가 달라지는 순간에만 문자열이 만들어진다.
/// </summary>
public class EnemyStatText
{
    private TMP_Text _hp;
    private TMP_Text _atk;
    private TMP_Text _def;

    // 마지막으로 표시한 값. int.MinValue = "아직 아무것도 안 씀"이라 첫 Tick이 무조건 갱신한다.
    private int _lastHp = int.MinValue;
    private int _lastMaxHp = int.MinValue;
    private int _lastAtk = int.MinValue;
    private int _lastDef = int.MinValue;

    public bool IsSetup => _hp != null || _atk != null || _def != null;

    /// <summary>EnemyBase가 Awake에서 1회 호출. 필요한 것만 넘기고 나머지는 null로 둬도 된다.</summary>
    public void Setup(TMP_Text hp, TMP_Text atk, TMP_Text def)
    {
        _hp = hp;
        _atk = atk;
        _def = def;
        Reset();
    }

    /// <summary>hp/maxHp는 실수 그대로 넘긴다 — 정수로 끊는 건 이쪽 책임이다.</summary>
    public void Tick(float hp, float maxHp, int atk, int def)
    {
        if (!IsSetup) return;

        if (_hp != null)
        {
            // 살아 있는데 0으로 보이지 않게 올림한다(0.4 남았으면 1로 표시). 죽으면 음수가 되므로 0으로 막는다.
            int cur = Mathf.Max(0, Mathf.CeilToInt(hp));
            int max = Mathf.Max(1, Mathf.RoundToInt(maxHp)); // 버프로 최대 체력이 바뀌어도 따라간다
            if (cur != _lastHp || max != _lastMaxHp)
            {
                _lastHp = cur;
                _lastMaxHp = max;
                _hp.text = $"{cur}/{max}";
            }
        }

        // 공격력/방어력은 EnemyBase가 이미 int로 반올림해 준 값이라 그대로 비교한다.
        if (_atk != null && atk != _lastAtk)
        {
            _lastAtk = atk;
            _atk.text = atk.ToString();
        }
        if (_def != null && def != _lastDef)
        {
            _lastDef = def;
            _def.text = def.ToString();
        }
    }

    /// <summary>
    /// 풀 반납 등에서 호출. 캐시만 비운다 — 다음 Tick이 무조건 다시 쓰게 해서
    /// 재사용된 개체가 이전 개체의 숫자를 한 프레임 보여주는 걸 막는다.
    /// </summary>
    public void Reset()
    {
        _lastHp = int.MinValue;
        _lastMaxHp = int.MinValue;
        _lastAtk = int.MinValue;
        _lastDef = int.MinValue;
    }
}
