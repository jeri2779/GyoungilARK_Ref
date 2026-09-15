using System;
using UnityEngine;

// 튜토리얼 한 단계 안에서 순서대로 거쳐가는 UI 하나. messageKey를 비워두면 이 waypoint가 속한
// 단계의 기본 문구(TutorialStepDefinition.messageKey)를 그대로 쓴다.
[Serializable]
public class TutorialWaypoint
{
    [Tooltip("스포트라이트로 뚫어줄(=클릭 가능하게 남겨둘) 사각형.")]
    public RectTransform target;

    [Tooltip("비워두면 target 자신의 activeInHierarchy로 '지금 이 waypoint 차례인지'를 판단한다. " +
        "채우면 이 오브젝트의 activeInHierarchy를 대신 쓴다 - 예를 들어 생산 시설 슬롯처럼 빈 칸/지어진 " +
        "칸이 같은 버튼을 쓰고 아이콘만 켜고 끄는 경우, target은 슬롯 버튼 전체(하이라이트 영역이 안 좁아지게), " +
        "activationCheck는 그 안의 아이콘 오브젝트(지어진 뒤에만 켜짐)로 따로 지정한다.")]
    public GameObject activationCheck;

    [Tooltip("이 오브젝트가 켜져 있는 동안엔 target이 활성 상태여도 이 waypoint를 건너뛴다. " +
        "예: 거점 UI가 열려있는 동안엔 거점 밖 버튼들이 화면에 같이 떠있어도 아직 그 차례가 아니게 " +
        "만들 때, 그 버튼들의 blockedWhile에 거점 패널을 지정한다.")]
    public GameObject blockedWhile;

    [Tooltip("비워두면 이 단계의 기본 messageKey를 그대로 쓴다.")]
    public string messageKey;
}

// 튜토리얼 한 단계의 저작 데이터. messageKey는 DataTableManager.StringTable.Get(...)으로 표시한다
// (TooltipUi.Show, GuideUI.ShowSpecificGuide와 같은 컨벤션).
[Serializable]
public class TutorialStepDefinition
{
    public TutorialStepId id;

    [Tooltip("이 단계의 기본 StringTable 키. waypoint에 messageKey가 따로 없으면 이걸 쓴다.")]
    public string messageKey;

    [Tooltip("진입 버튼부터 최종 액션 버튼까지 순서대로. 매 프레임 activeInHierarchy인 것 중 " +
        "가장 뒤쪽(가장 깊이 들어간) 항목을 스포트라이트하고, 그 waypoint의 문구를 보여준다.")]
    public TutorialWaypoint[] waypoints;

    [Tooltip("게이팅할 게임 이벤트가 없는 단계(자원 확인)만 true - '다음' 버튼 클릭으로 완료 처리한다.")]
    public bool completesOnAcknowledge;

    [Tooltip("이 단계가 활성인 동안 Time.timeScale을 0으로 멈춘다(예: 밤 전투 중 플레이어 스킬 설명). " +
        "이 단계가 완료되면 자동으로 1로 되돌린다.")]
    public bool pauseTimeWhileActive;
}
