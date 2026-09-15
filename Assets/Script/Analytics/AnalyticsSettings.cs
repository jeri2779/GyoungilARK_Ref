using UnityEngine;

// Resources에 두 벌 둔다 — 에디터 플레이용 Resources/Test, 빌드용 Resources/Build.
// AnalyticsRecorder.EnsureInit이 #if UNITY_EDITOR로 둘 중 하나를 집는다.
// 해당 에셋이 없으면 기본값(원격 전송 비활성화)으로 로컬 파일에만 기록한다.
[CreateAssetMenu(fileName = "AnalyticsSettings", menuName = "Analytics/AnalyticsSettings")]
public class AnalyticsSettings : ScriptableObject
{
    [Tooltip("이벤트 배치를 전송할 서버 엔드포인트. 비워두면 로컬 파일에만 기록하고 원격 전송은 하지 않는다.")]
    public string remoteEndpointUrl;

    [Tooltip("버퍼를 로컬 파일/서버로 내보내는 주기(초).")]
    public float flushIntervalSeconds = 15f;

    [Tooltip("이 개수만큼 이벤트가 쌓이면 flushIntervalSeconds를 기다리지 않고 즉시 내보낸다.")]
    public int maxBufferedEvents = 50;
}
