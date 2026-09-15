// ESC로 패널을 닫는 여러 곳(RegionOverviewPanel, AddCitizen, UiManager 등)이 튜토리얼 진행 중인지
// 확인하는 데 쓴다. 이벤트 버스가 없는 프로젝트라 HeroSelectionService와 같은 static 서비스 패턴을
// 따른다 - 굳이 각 패널에 TutorialManager를 주입하는 역방향 의존을 만들지 않기 위함이다.
public static class TutorialInputGate
{
    public static bool BlockEscapeClose { get; set; }

    // ESC 외의 단축키(B/N/O/G/P/U/R/E/I 등)를 튜토리얼 진행 중에 막는 데 쓴다. 스포트라이트가
    // 짚어주는 순서를 벗어나 임의로 다른 패널을 열면 튜토리얼 스텝이 꼬인다. BlockEscapeClose와
    // 항상 같은 구간(TutorialManager가 켜져 있는 동안)에서 함께 토글된다.
    public static bool BlockHotkeys { get; set; }

    // SaveManager가 0일차 연습 상태를 세이브 파일에 남기지 않으려고 확인한다 - 0일차는 다음 날이
    // 되는 순간 전부 초기 상태로 되돌아가는 임시 데이터라, 그 사이에 저장되면 안 된다.
    // TutorialManager가 켜져 있는 동안(0일차 리셋까지 끝날 때까지) true.
    public static bool BlockSave { get; set; }

    // PlaceHero 단계에서 로스터 아이콘을 고르고 맵 클릭을 기다리는 동안(TutorialManager.
    // ShowUnblockedMessage)에는 3D 맵 타일 클릭을 통과시키려고 오버레이의 딤/전체 차단을 전부
    // 꺼둔다 - 그 사이엔 지역 슬롯, 거점 화면 열기 버튼, 가이드 버튼 등 스포트라이트 밖의 다른
    // 버튼도 똑같이 눌려버려 BuildingPanel/FacilityBuildChoicePanel 같은 패널이 열리고, 그게
    // 맵 위를 덮어 배치를 완료할 수 없는 상태로 튜토리얼이 멈춘다. 그 구간에서만 true가 되어
    // 그런 패널들이 마우스로 열리는 걸 막는다.
    public static bool BlockPanelOpen { get; set; }

    // HeroUpgradeMention 단계는 영웅 강화 버튼을 스포트라이트로 짚어 "이런 게 있다"고 언급만 할
    // 뿐, BuildingUpgradeMention과 달리 실제로 눌러서 완료시키는 스텝이 아니다(완료는 "다음"
    // 확인으로만 이루어진다). 그런데 스포트라이트 구멍이 실제 버튼 위에 뚫려 있어 그대로 눌리면
    // HeroTierUpgradeMenu가 열리면서 그 밑의 영웅 인벤토리 UI가 꺼져 튜토리얼이 깨진다 - 이 단계인
    // 동안만 그 버튼을 못 누르게 막는다.
    public static bool BlockHeroUpgradeOpen { get; set; }

    // HeroCombineMention 단계는 영웅 합성을 언급/유도하는 스텝이라 로스터 아이콘 더블클릭(합성)과
    // combineAllButton(일괄합성)은 그대로 눌리게 둬야 한다 - 하지만 같은 아이콘의 단일 클릭은
    // HeroInventory.OnIconClicked의 배치 분기(HideContentForPlacement + view.SetHero)로 빠져
    // PlaceHero 스텝도 아닌데 배치 모드로 들어가버려 튜토리얼이 꼬인다. 합성은 막지 않고 이
    // "단일 클릭으로 배치 시작" 경로만 막는 데 쓴다.
    public static bool BlockHeroPlacementFromInventory { get; set; }

    // 튜토리얼 중 인벤토리에서 영웅을 회수하면 PlaceHero 이후 스텝들(RelocateHero, HeroUpgradeMention,
    // HeroCombineMention 등)이 전제하는 "배치된 영웅"이 사라져 진행이 꼬인다. BlockSave와 같은 생명주기
    // (TutorialManager.OnEnable~OnDisable 전체 구간) 동안 true - HeroInventory가 회수 버튼 자체를 숨긴다.
    public static bool BlockHeroRetrieve { get; set; }

    // PlayerSkillMention 단계는 스킬 버튼을 스포트라이트로 짚어 언급만 하는 스텝이라, 그 구멍으로
    // 실제로 눌려서 스킬이 나가면 안 된다. 이전에는 패널 CanvasGroup.blocksRaycasts를 꺼서 막았는데,
    // 그러면 EventSystem 레이캐스트 자체가 안 잡혀 OnPointerEnter/Exit도 같이 막혀버려 툴팁마저
    // 안 뜨는 부작용이 있었다(hoverDelay/unscaledTime과는 무관한 원인). 레이캐스트는 그대로 두고
    // 클릭 핸들러(PlayerSkillPanel)에서 이 플래그만 확인해 스킬 발동만 막는다.
    public static bool BlockPlayerSkillCast { get; set; }
}
