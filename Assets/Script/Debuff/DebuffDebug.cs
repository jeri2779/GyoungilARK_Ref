using UnityEngine;

/// <summary>
/// 디버프 적용 경로 진단용 임시 로거. 적용 사슬이 여러 파일에 걸쳐 있어
/// (호출부 → DebuffSO.Apply → 파생 OnApply → DotRegistry/BuffManager) 스위치를 한 곳에 모았다.
///
/// 다 확인했으면 Enabled를 false로 내리면 전부 조용해진다.
/// 완전히 걷어낼 때는 이 파일을 지우고 "DebuffDebug"를 검색해 호출부를 지우면 된다.
/// </summary>
public static class DebuffDebug
{
    // const가 아니라 static readonly인 이유 — const false로 두면 Log 본문이 도달 불가 코드가 되어 경고가 뜬다.
    // 지속 피해 틱 로그(DotRegistry)를 보려고 켜 둔 상태다. 확인이 끝나면 false로 내릴 것 — 켜 두면
    // 디버프가 걸릴 때마다 콘솔에 줄이 쌓여 다른 로그가 묻힌다.
    public static readonly bool Enabled = false;

    // context를 넘기면 콘솔에서 그 줄을 눌렀을 때 해당 오브젝트가 하이라이트된다.
    public static void Log(string message, Object context = null)
    {
        if (!Enabled) return;
        Debug.Log($"[Debuff] {message}", context);
    }
}
