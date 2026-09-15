using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 특성 칸(EnemyTable.Attribute) 원본을 화면용 문자열로 만든다.
/// 도감(EnemyInfo)과 스테이지 정보 툴팁(StageInfoView)이 같은 규칙을 쓰도록 한 곳에 모았다.
///
/// 각 항목을 &lt;link="ID"&gt;로 감싸므로, 그 TMP를 보는 AttributeTooltip이 hover를 잡아 설명을 띄운다.
/// link ID 규약(특성 enum 이름 또는 스킬 ID)이 AttributeTooltip.DescKeyFor와 짝이라 여기서 바꾸면
/// 그쪽도 같이 봐야 한다.
/// </summary>
public static class EnemyAttributeText
{
    /// <param name="raw">"Fly|Cloaking|FireZoneSkill" 같은 원본. 비면 "특성 없음"(None).</param>
    /// <param name="attrColor">특성별 글자색. null이면 색을 입히지 않는다(툴팁처럼 좁은 곳).</param>
    /// <param name="skillColor">고유 스킬로 적은 항목의 글자색. null이면 색 없음.</param>
    /// <param name="separator">항목 구분자.</param>
    /// <param name="context">경고 로그에서 클릭하면 잡히는 오브젝트(선택).</param>
    public static string Build(string raw,
                               Func<EnemyAttribute, Color> attrColor = null,
                               Color? skillColor = null,
                               string separator = ", ",
                               UnityEngine.Object context = null)
    {
        var st = DataTableManager.StringTable;
        var parts = new List<string>();
        var seen = new HashSet<string>();   // 같은 토큰을 두 번 적어도 한 번만 표기

        // 잠복(Burrow)은 피격 판정 때문에 은신(Cloaking) 비트를 CSV에 같이 달고 다닌다 —
        // 설명엔 잠복만 세우고 은신은 감춘다(표기 순서와 무관하게 미리 훑는다).
        bool hasBurrow = false;
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (string t in raw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(t.Trim(), nameof(EnemyAttribute.Burrow), StringComparison.OrdinalIgnoreCase))
                {
                    hasBurrow = true;
                    break;
                }
        }

        if (!string.IsNullOrEmpty(raw))
        {
            // 표시 순서는 CSV에 적은 순서를 그대로 따른다(enum 선언 순서가 아니다) — 저작자가 순서를 쥔다.
            foreach (string token in raw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = token.Trim();
                if (id.Length == 0 || !seen.Add(id)) continue;

                if (Enum.TryParse(id, true, out EnemyAttribute flag))
                {
                    if (flag == EnemyAttribute.None) continue;   // "None"을 적은 경우 — 아래 폴백이 처리한다
                    if (hasBurrow && flag == EnemyAttribute.Cloaking) continue; // 잠복이 있으면 은신은 겹치므로 생략
                    parts.Add(Link(flag.ToString(), st.Get(flag.ToString()),
                                   attrColor != null ? attrColor(flag) : (Color?)null));
                    continue;
                }

                // 특성으로 파싱이 안 되면 스킬 ID로 본다 — "고유 특성으로 이 스킬을 가진다"를 같이 세우기 위함.
                SkillTable.Data skill = DataTableManager.SkillTable?.Get(id);
                if (skill == null)
                {
                    // 조용히 사라지면 오타를 못 잡는다. 특성도 스킬도 아니면 알려준다.
                    Debug.LogWarning($"EnemyAttributeText: 특성 칸의 '{id}'는 EnemyAttribute도 스킬 ID도 아니라 건너뛴다.", context);
                    continue;
                }
                // NameKey가 빈 칸이면 null이고 StringTable.Get(null)은 예외를 던지므로 스킬 ID를 그대로 보여준다.
                string label = string.IsNullOrEmpty(skill.NameKey) ? id : st.Get(skill.NameKey);
                parts.Add(Link(id, label, skillColor));
            }
        }

        return parts.Count > 0 ? string.Join(separator, parts) : st.Get("None");
    }

    private static string Link(string id, string label, Color? color)
    {
        string body = color.HasValue
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(color.Value)}>{label}</color>"
            : label;
        return $"<link=\"{id}\">{body}</link>";
    }
}
