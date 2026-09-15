# 계산 결과 보관 주체 감사 — Map 폴더

작성 2026-08-05 · 브랜치 `Feature/Map/TileState` · 대상 `Assets/Script/Map/**` 101개 파일 전수

## 📌 읽기 전 — 게임 규칙 전제

이 문서의 판정을 읽을 때 아래 확정 사항을 먼저 적용한다.
상세는 `CodeSaveBase/DefenseProject/DefenseProjectNew/게임 규칙/게임_규칙_정정_2026-08-15.md`.

- **배치물은 전부 한 칸이다.** 다중 칸 점유는 죽은 기능 → 칸 수에 비례해 부풀린 비용 계산은 무효
- **모듈은 6개 고정이다.** 모듈 수 증가를 전제한 확장성 지적은 무효
- **지대 기믹은 그 모듈에서만 유효하다.** 다른 모듈에 안 걸리는 건 의도

## ⚠️ 2026-08-15 갱신 — 08-05 이후 커밋으로 상태가 바뀐 항목

원본 표는 2026-08-05 스냅샷이다. 그 뒤 08-12~08-14 커밋(경로 캐시 구조 변경 등)이 지나가며 아래 항목이 달라졌다. **표·본문의 원래 서술은 그대로 남기고, 각 항목 끝에 갱신 표시만 덧붙인다.**

| 항목 | 08-05 판정 | 08-15 확인 결과 |
|---|---|---|
| B1 포인터 아래 타일 | 🔴 주인 없음 | ✅ **해결됨** — `HoveredTileData` 실제로 만들어져 있음 (`Preview/HoveredTileData.cs`) |
| A3/B2 프레임 2회 계산 | 🔴 프레임마다 2회 | 🟡 **대부분 해결** — `HoverPlaceData`가 프레임당 1회로 줄임. 클릭 확정 시점(`PlaceAction.PlaceUnit`)만 재계산 남음 |
| C1 `MapBoard.GetPath` | 🔴 제거 대상 | 🟡 **절반 해결** — `private`로 내려감(`MapBoard.cs:143`). 다만 `GetWaypoints`는 여전히 `public`이고 `WaveSpawner`·`EnemyMovement`의 폴백 경로로 남아 있음 |
| C2 레인 월드 좌표 재생성 | 🔴 호출마다 새 List | ✅ **해결됨** — `EnemyLanes._cachedWalkPaths0` 캐시 추가 (`EnemyLanes.cs:18,91-93,110`) |
| C3 캐시 중복(`WaveSpawner._allPaths`) | 🟡 주인 중복 | 🔴 **그대로 미해결** (`WaveSpawner.cs:40`) |
| **F1 레인 신원** | 🔴 미완성(최우선 순위) | ✅ **이미 해결됨 — 단, 문서가 제안한 방식이 아니다.** 상세는 §6-갱신 참고 |

## ⚠️ 2026-08-16 갱신 — 08-15 이후 커밋으로 다시 바뀐 항목 (A1·A2·A3·B2)

| 항목 | 08-15 판정 | 08-16 확인 결과 |
|---|---|---|
| A1/A2 배치 판정 중복 | 🟡 `CanPlace`→`TopY`→`Place`가 같은 칸에 판정을 3회 다시 물음 | ✅ **해결됨** — `AreaPlace.CanPlace()` 메서드 자체를 삭제. `TopY(area, kind, out canPlace)`가 높이를 재는 같은 순회 안에서 판정도 함께 냄(`AreaPlace.cs:19~39`). 칸당 판정 호출이 1회로 줄었고, `Place()`의 실행(점유 기록) 순회만 별도로 남음 — 이건 계산이 아니라 실행이라 이 문서의 "중복 계산"에 안 해당 |
| A3 놓일 자리(`PlaceData`) 재계산 | 🟡 `HoverPlaceData`로 대부분 해결, 클릭 확정 시점만 재계산 남음 | 🟡 **그 부분은 동일** — 클릭 확정 재계산은 이번에도 안 건드림. 대신 `PlaceFinder.TryResolve`의 `if (area == null) {...; return false;}`는 "실제로 못 일어나는 경우를 대비하던 죽은 코드"로 확인돼 삭제함(`PlaceFinder.cs`) |
| B2 포인터 아래 자리(`PlacementArea`) 재계산 | (08-16 이전 세션에서 프레임 캐시로 임시 해결) | 🔴 **의도적으로 다시 미해결 상태로 되돌림** — 그 프레임 캐시(`EnsureFrameAnchorTile` 등)가 Map 폴더 `if`·`null` 전면금지 규칙을 어겨서 통째로 삭제함. "자리를 못 찾는 경우"가 실제 빌드 씬(`MainScene.unity`)에서도 도달 불가능임을 직접 확인했고, 모듈이 6개로 고정돼 있어 매 프레임 재계산 비용은 실측상 무시 가능하다고 보고 **캐시 없이 그냥 두기로 판단**함 |

**판단 근거**: 이번 경우는 "계산 결과를 보관할 주체가 없어서 문제"가 아니라 "보관 자체가 원칙 위반(if·null)의 원인이라 안 보관하기로 한" 경우라, 이 문서 §D("제대로 된 사례") 기준과는 반대 방향처럼 보임. 모듈 6개 고정·실측 비용 사실상 0이라는 게임 규칙 전제(§0)가 판단 근거이며, 모듈 수가 늘거나 호출 빈도가 커지면 재검토 대상.

## ⚠️ 2026-08-16 갱신(2차) — A3 클릭 확정 시점 재계산 해결

| 항목 | 이전 상태 | 이번 확인 결과 |
|---|---|---|
| A3 놓일 자리(`PlaceData`) 재계산 — 클릭 확정 시점 | 🟡 미해결(바로 위 표에 "이번에도 안 건드림"으로 기록) | ✅ **해결됨** — `PlaceAction.PlaceUnit`과 `MapCommand.HeldData`가 `finder.TryResolveSlot`/`finder.TryResolve`로 다시 계산하던 걸 지우고, 같은 화면에 `PlaceHoverFinder`가 이미 구해서 `hoverPlace`에 넣어둔 `PlaceData`를 그대로 읽게 바꿈(`PlaceAction.cs`, `MapCommand.cs`, `MapAssemble.cs`) |
| B2 포인터 아래 자리 — 확정 시점의 추가 호출 | 화면당 미리보기 1회 + 확정 클릭 시 추가 1회(최대 2회) | 🟡 확정 시점 추가 호출이 없어져 화면당 필요한 만큼(1회)만 남음. 다만 `GetArea` 자체가 결과를 안 보관하고 매번 다시 훑는다는 08-16(1차) 판단·반복문(`for`) 문제는 그대로 미해결 |

**같이 지운 것**: `PlaceAction`·`MapCommand`가 각자 갖고 있던 `PlaceFinder finder` 참조가 이제 안 쓰여서 같이 삭제함. 계산 도구(`PlaceFinder`) 자체는 미리보기 쪽(`PlaceHoverFinder`)이 계속 쓰고 있어 죽은 클래스는 아님.

(표에는 안 올리는 작은 정리) `PointerPick`의 "화면당 한 번만 계산" 판단 로직도 같은 세션에 정리함 — "이미 이번 화면에 했나"/"마우스가 움직였나"를 각각 묻기 전용·바꾸기 전용 메서드(`IsSameFrame`·`MarkFrame`·`MousePos`·`IsMoved`·`MarkPos`)로 쪼갬. 계산 결과 주인 문제와는 무관한 이름·구조 정리라 표에는 안 넣음.

## ⚠️ 2026-08-16 갱신(3차) — `Under`/`Nearest` 이름·역할 정리 (계산 결과 주인 문제와 무관)

아래 74·78행 표의 `PointerPick.Under`는 이름이 바뀌어 **지금은 `PointerPick.HitTile`이다.**

- `Under` → `HitTile`로 개명. 안에서 뒤섞여 있던 "쓸 수 있는 모듈인가" 판단과 "카메라에 얼마나 가까운가" 계산을 각각 `IsUsable(board)`·`DistanceSqr(tile, ray)`로 분리
- `Nearest`도 같은 모양으로 정리 — `IsUsable`을 새로 안 만들고 `HitTile`이 쓰던 걸 그대로 같이 씀(중복 로직 제거), 거리 계산은 `LineDistance(tile, ray)`로 뺌
- 지역변수도 정리: `best`→`frontTile`/`nearestTile`, `bestSqr`→`frontSqr`, `bestDist`→`nearestDistance`
- **이 정리는 이름·역할 가독성 문제이지 "계산 결과 주인 없음" 문제가 아니다.** B2에 적힌 "반복문(`for`) 문제는 그대로 미해결"은 이번에도 동일 — `for`+`if`/`continue` 자체는 안 없앴고, 그 안의 판단에 이름만 붙였다
- 검증 = `refresh_unity`로 강제 재컴파일 3회, `read_console`(error) 매번 0건

## ⚠️ 2026-08-16 갱신(4차) — `AreaPlace` 반복문 완전 제거 (A1/A2 재분류)

아래 75·76행 표의 A1/A2(`AreaPlace.CanPlace`·`AreaPlace.TopY`)는 이번 갱신으로 **판정 자체가 바뀐다.**

- **발견** = `area.Cells`가 다중 칸 폐지로 시스템 전체에서 항상 원소 1개임을 코드로 재확인함(`AreaCalc.GetCells`가 크기 (1,1)이면 무조건 1개만 생성, `PlaceSize.GetSize`가 항상 (1,1)만 반환). 즉 `TopY`의 `foreach`는 "여러 칸 중 최선을 고르는 반복"이 아니라 **`banned-syntax-if-null` 규칙이 말하는 "0/1회만 도는 for = if 위장"** 이었다
- **덤 확인** = `area.Board.TryGetCell(cell, out tile)` 존재검사도 죽은 코드로 확인됨 — `MapBoard.CanPlace`가 내부에서 이미 `_cells.TryGetValue`를 거쳐야 `true`가 되므로(`MapBoard.cs:321~325`), `canPlace == true`인 칸은 `TryGetCell`도 항상 성공한다
- **적용한 안** = `TopY`·`Place`·`Remove` 3개 전부 `area.Origin`(=그 유일한 칸 좌표)을 직접 써서 `foreach`/`for`를 통째로 삭제. 남긴 `if` 1개는 "이 칸에 놓을 수 있는가"라는 실제 게임 상태 분기(점유된 칸 위 미리보기 등 실제로 일어남)라 그대로 둠, 삼항연산자는 `if`/`return`으로 교체
- **재분류** = 이 항목은 "계산 결과 주인 없음"(이 문서의 원래 주제)이 아니라 **"반복 자체가 필요 없었다"**는 별개 문제였음이 드러남. 표의 원래 판정("중복 호출")은 스냅샷으로 남기고 여기 갱신 표시만 덧붙임
- **검증** = 코드 반영 후 `refresh_unity` 강제 재컴파일 → `read_console`(error) 0건. 사용자가 에디터 플레이 모드에서 배치·제거 직접 테스트해 정상 동작 확인함

## ⚠️ 2026-08-17 갱신 — E1 판정 정정(오류) · C4 해결 확인

**계기**: 사용자가 "GetLineTiles 참조 2개 걸린 걸 왜 죽은 코드라 했냐"고 지적, 문서 전체 재검증을 요청함.

| 항목 | 08-05 판정 | 08-17 확인 결과 |
|---|---|---|
| E1 `TileShapeQuery.GetLineTiles` | 🔴 호출부 없음, "정사각 블록 만들고 `Find`로 다시 훑는다" | ❌ **판정 자체가 오류였음이 확인됨.** ①실제 호출부가 `Assets/Script/Hero/Hero.cs:589`(`GetEnemiesInLine`)·`:603`(`GetLineEndPoint`) 두 곳 있다 — Map 폴더(`Assets/Script/Map/**`) 밖이라 이 문서의 원래 조사 범위(§0 "대상 101개 파일")에 안 걸렸을 뿐, 죽은 코드가 아니다. ②코드 자체도 이미 "정사각 만들고 Find" 방식이 아니다 — 지금은 `board.TryGetCell`로 축 위 좌표를 직접 훑는 방식(`TileShapeQuery.cs:32`)이라 §2-E 서술도 낡았다. **삭제 후보에서 제외.** |
| C4 `MapBoard.HeuristicToNearestCore` | 🔴 없음, "A\*가 노드 볼 때마다 코어 전체 훑기" | ✅ **해결됨(2026-08-17 세션).** `MapBoard._coreDistance`(`Dictionary<Tile,int>`, `MapBoard.cs:11`)에 `Build()` 시점 1회만 전 타일 거리를 재 두고, `HeuristicToNearestCore`(`MapBoard.cs:167`)는 그 값을 꺼내기만 한다. 같은 패턴이 `LaneBuilder.BuildCoreDistance`에도 별도로 있었고 그것도 같은 세션에 정리함(구조적 중복 여부는 별도 미결 사안으로 남음) |

**교훈**: "🔴 없음/미사용" 판정에 검색 범위나 GUID 검색 같은 **증거가 안 적혀 있으면 그 판정은 재검증 없이 믿지 않는다.** F1(RouteBranch/TileRoute)처럼 "씬·프리팹 GUID 검색 0건"까지 적어놓은 항목은 이번 재검증에서도 실제로 맞았다 — 증거를 남긴 판정과 안 남긴 판정의 신뢰도가 다르다.

---

## 이 문서의 목적

계산 결과를 만들어 놓고 **아무도 들고 있지 않아서**, 필요한 쪽이 같은 답을 다시 만드는 지점을 모았다.

> 마트에서 물건을 사서 땅바닥에 흩뿌리고, 필요할 때 땅을 훑어 찾는 구조.

반복문·성능은 이 문서의 주제가 아니다. **보관 주체가 없어서 훑는 코드가 생긴다**는 인과가 주제다.
따라서 항목마다 "있어야 할 주인"을 **클래스 이름으로** 지정한다. 주인이 정해지면 훑을 이유가 사라진다.

작업 순서는 항상 이 순서다: **버려진 결과 찾기 → 주인 이름 지정 → 소비자는 읽기만 → 그 다음에 반복문**.

---

## 판정 기준

| 상태 | 뜻 |
|---|---|
| 🔴 **주인 없음** | 계산 결과를 반환만 하고 어떤 객체도 보관하지 않는다 |
| 🟡 **주인 중복** | 같은 결과를 두 곳 이상이 각자 들고 있어 어느 쪽이 진짜인지 코드가 모른다 |
| 🟢 **주인 있음** | 계산 담당이 만들고 보관 담당이 들고 있으며 소비자는 읽기만 한다 |

---

## 1. 감사 표

| # | 계산 결과 | 만드는 곳 | 지금 주인 | 있어야 할 주인 | 재계산 | 08-15 확인 |
|---|---|---|---|---|---|---|
| A1 | 칸별 배치 가능 판정 | `AreaPlace.CanPlace` (AreaPlace.cs:6) | 🔴 없음 | `PlacementArea`에 판정 결과 동봉 | 배치 1회당 **3바퀴** | 🟡 코드는 그대로. 단 **한 칸 고정이라 "3바퀴"가 아니라 "같은 답 3회 계산"** — 규모가 원문보다 훨씬 작다 |
| A2 | 배치물이 설 윗면 높이 | `AreaPlace.TopY` (AreaPlace.cs:33) | 🔴 없음 | 같음 (A1과 한 벌) | A1과 같은 칸을 또 순회 | 🟡 A1과 동일 — 한 칸이라 순회 비용은 사실상 없고, 남는 건 중복 호출뿐 |
| A3 | 놓일 자리 전체(`PlaceData`) | `PlaceFinder.TryResolve` (PlaceFinder.cs:21) | 🔴 없음 | `PlaceData`를 프레임 단위로 보관하는 주체 | **프레임마다 2회** | 🟡 `HoverPlaceData`로 대부분 해결, 클릭 확정만 재계산 남음 |
| B1 | 포인터 아래 타일 | `PointerPick.Under` (PointerPick.cs:46) | 🔴 없음 | `HoveredTileData` (신규) | 프레임마다 호출부 수만큼 | ✅ `HoveredTileData` 실존 확인 |
| B2 | 포인터 아래 자리(`PlacementArea`) | `PointerPick.GetArea` (PointerPick.cs:23) | 🔴 없음 | 같음 | 프레임마다 2회 | 🟡 A1과 같은 경로라 함께 정리 필요 |
| C1 | 스폰→코어 단일 경로 | `MapBoard.GetPath` (MapBoard.cs:104) | 🔴 없음 | `EnemyLanes`로 통합(중복 제거) | **호출마다 A\* 전체** | 🟡 `private`로 내려감, `GetWaypoints` 폴백은 잔존 |
| C2 | 경로의 월드 좌표 목록 | `EnemyLanes.GetPaths` (EnemyLanes.cs:65) → `LaneData.GetPoints` (LaneData.cs:27) | 🔴 없음 | `LaneData`가 월드 좌표까지 완결 보관 | 호출마다 새 `List<Vector3>` | ✅ `_cachedWalkPaths0` 캐시로 해결 |
| C3 | 레인 목록 캐시 | `EnemyLanes.lanes` / `WaveSpawner._allPaths` (WaveSpawner.cs:37) | 🟡 중복 | `EnemyLanes` 하나 | 스포너가 사본을 또 보관 | 🔴 그대로 |
| C4 | 가장 가까운 코어까지 거리 | `MapBoard.HeuristicToNearestCore` (MapBoard.cs:144) | 🔴 없음 | 코어 목록 보관 주체가 미리 계산 | A\*가 노드 볼 때마다 코어 전체 훑기 | ✅ **2026-08-17 해결됨** — `_coreDistance` 사전 계산. 상세는 위 08-17 갱신 참고 |
| D1 | 사거리 칸 목록 | `RangeCalc.GetRange` (RangeCalc.cs:14) | 🟢 `RangeTileData` | 현행 유지 — **기준점** | 1회 | (변동 없음) |
| D2 | 배치된 유닛과 그 자리 | `UnitPlacer.TryPlace` (UnitPlacer.cs:16) | 🟢 `PlacedUnitData` | 현행 유지 — **기준점** | 1회 | (변동 없음) |
| D3 | 타일 이웃 | `TileLink.LinkNeighbors` (TileLink.cs:8) | 🟢 `Tile.NeighborTiles` | 현행 유지 — **기준점** | 보드 조립 1회 | (변동 없음) |
| E1 | 축 방향 타일 목록 | `TileShapeQuery.GetLineTiles` (TileShapeQuery.cs:25) | 🔴 없음 | 좌표 직접 조회로 대체 | 블록 리스트 **만들고 다시 훑음** | ⚠️ **2026-08-17 정정 — 이 판정 자체가 오류.** 실호출부 `Hero.cs` 2곳 있음, 코드도 이미 직접 조회 방식. 상세는 위 08-17 갱신 참고 |
| F1 | 고른 경로의 번호 | `MapMakerWindow.RouteIndex` (MapMakerWindow.cs:653) | 🔴 없음 (좌표로 역조회) | 갈래 참조 보관 (`RouteBranch`) | **프레임마다 레인 전체 훑기** | ✅ **해결됨(다른 방식)** — `RouteData` 참조 비교로 이미 전환. `RouteBranch`는 미사용 |
| F2 | 격자·레인·문제·오버라이드 | `MapMakerWindow.OnGUI` (MapMakerWindow.cs:161~224) | 🔴 없음 | (저작 도구 — 판단 필요) | **마우스 움직임마다 전부** | 🔴 그대로(판단 보류 유지) |

---

## 2. 항목별 상세

### A. 배치 판정 — 같은 답을 3번 만든다

`AreaPlace`의 세 메서드가 **같은 칸에 같은 질문**을 반복한다.

```
CanPlace(area, kind)   → 칸마다 board.CanPlace(cell, kind)      … 1바퀴
Position(area, ...)
  └ TopY(area, kind)   → 칸마다 board.CanPlace(cell, kind) 또   … 2바퀴
Place(data, unit, kind)→ 칸마다 SetOccupant                     … 3바퀴
```

`board.CanPlace`(MapBoard.cs:219)는 매번 `_cells.TryGetValue` + `TilePlacementRule.CanPlace`를 다시 돈다.

**2×2 건물을 커서에 물고 있는 한 프레임의 실제 비용**

| 경로 | 무엇을 | 횟수 |
|---|---|---|
| `MapCommand.FollowGhost` (MapCommand.cs:45) | `TryResolveSlot` → 자리 계산 + 판정 | 1 |
| `TilePaintView.PaintPlacePreview` (TilePaintView.cs:57) | **같은 `TryResolveSlot`을 또** | 1 |
| 그 안에서 `CanPlace` 판정 | 4칸 × (CanPlace + TopY) × 2경로 | **16회** |
| 보드 전체 훑기 (`CellFromRay`/`NearestCellFromRay`) | 포인터 조회마다 | **2회 이상** |

같은 프레임에 같은 답을 두 벌 만들고 둘 다 버린다.

**있어야 할 주인**: `PlacementArea`가 칸 좌표만 들고 있고 판정은 밖에서 다시 한다. 규칙 13(계산 결과는 소비자가 바로 쓸 최종 결과)대로면 **판정 결과와 윗면 높이까지 동봉해서** 한 번만 만들어야 한다. 그리고 그 `PlaceData`를 **프레임에 하나만** 만들어 미리보기·확정·색칠이 공유해야 한다.

#### 🔻 2026-08-15 갱신 — 규모를 낮춰 읽어야 한다

위 "2×2 건물 한 프레임 16회" 계산은 **다중 칸 배치를 전제로 한 수치라 지금은 성립하지 않는다.**
배치물은 전부 한 칸으로 확정됐다(`Place/Area/PlaceSize.cs:6-9`, 상세는 `게임 규칙/게임_규칙_정정_2026-08-15.md` §1-1).

- 실제로 남은 것: 칸 **1개**에 대해 `CanPlace` → `TopY` → `Place`가 각각 다시 묻는 **중복 호출 3회**
- 사라진 것: "칸 수 × 경로 수" 곱셈으로 부풀던 부분 전부
- 프레임 중복(`MapCommand` ↔ `TilePaintView` 두 벌 계산)은 `HoverPlaceData` 도입으로 이미 1벌로 줄었다

→ 따라서 이 항목은 **성능 문제가 아니라 "계산 결과의 주인이 없다"는 원칙 문제**로만 남는다. 급하지 않다.

### B. 포인터 조회 — 읽을 때마다 보드를 훑는다

`PointerPick.Under`는 호출마다 `Physics.Raycast` + 보드별 `MapBoard.CellFromRay`(전체 칸 순회)를 돈다.
`Nearest`는 실패 시 `NearestCellFromRay`가 **한 번 더** 전체 칸을 훑는다.

호출부:

| 호출부 | 위치 |
|---|---|
| `MapCommand.OnPress` | MapCommand.cs:72 |
| `MapCommand.OnRelease` | MapCommand.cs:146 |
| `MapView.HoverTile` (프로퍼티) | MapView.cs:41 |
| `RangeInput.KeepRange` | RangeInput.cs:32 |
| `PlaceFinder.TryResolve` → `GetArea` | PlaceFinder.cs:26 |

⚠️ `MapView.HoverTile`이 **프로퍼티**라 읽는 것처럼 보이지만 매번 전체 훑기가 돈다.
`TilePaintView.SkillOrigin`(TilePaintView.cs:201)이 이 프로퍼티를 읽는다.

**있어야 할 주인**: 프레임 시작에 한 번 정해서 보관하는 주체(가칭 `HoveredTileData`). 소비자는 그 값을 읽는다.

### C. 경로 — 계산이 두 갈래로 갈려 있고 둘 다 주인이 없다

`MapBoard.GetPath`(:104)는 부를 때마다
① 전체 타일의 `EnemyLane`을 지우고 → ② A\*를 처음부터 돌고 → ③ 다시 켠다.
결과 `List<Tile>`은 `GetWaypoints`(:155)가 좌표로 바꿔 반환하고, **아무도 보관하지 않는다.**

경계 밖 소비자(관찰만):

| 소비자 | 위치 | 문제 |
|---|---|---|
| `WaveSpawner.Start` | WaveSpawner.cs:68 | 폴백 경로 1회 |
| `EnemyMovement` | EnemyMovement.cs:105 | **적 한 마리당** 전체 경로 재계산 |

⚠️ `GetPath`는 이름이 Query인데 `EnemyLane`을 쓰는 **Command**다. CQS 위반이며, 부작용 때문에 결과를 보관하기도 어렵다.

`EnemyLanes.lanes`는 레인을 **보관한다(🟢)**. 그런데
- `GetPaths`(:65)가 호출마다 `LaneData.GetPoints`로 새 `List<Vector3>`를 만든다 → 월드 좌표 결과는 주인이 없다
- `WaveSpawner._allPaths`(:37)가 그것을 또 캐시한다 → 주인 중복(🟡)

**있어야 할 주인**: 경로 계산은 `EnemyLanes` 하나로 모으고, `LaneData`가 월드 좌표까지 완결 보관한다. 스포너는 사본을 만들지 않고 조회만 한다. `MapBoard.GetPath`는 통합 후 제거 대상이다.

### D. 제대로 된 사례 — 이걸 기준으로 삼는다

| 사례 | 구조 |
|---|---|
| 사거리 | `RangeCalc`(계산) → `RangeTileData.KeepRange`(보관) → `TilePaintView`(읽기만) |
| 배치 장부 | `UnitPlacer`(생성) → `PlacedUnitData.Add`(보관) → `UnitRemover`·`UnitReplace`가 `TryGetArea`로 읽기만 |
| 타일 이웃 | `TileLink.LinkNeighbors`(1회) → `Tile.NeighborTiles`(보관) → `Pathfinder`가 읽기만 |

세 사례의 공통점은 하나뿐이다. **계산 결과에 이름 붙인 주인이 있다.**
`Pathfinder`가 좌표를 더해 격자를 뒤지지 않는 이유가 D3다.

### E. 만들고 나서 다시 훑는 사례

`TileShapeQuery.GetLineTiles`(:25)

```
board.GetTiles(origin, length, true)  ← 정사각 블록 리스트를 만든다
block.Find(t => t.Coord == target)    ← 그 리스트를 다시 훑는다
```

목표 좌표를 이미 알고 있으므로 `board.TryGetCell(target, ...)`로 단건 접근하면 된다.
리스트를 만들 이유 자체가 없다.

### F. 저작 도구 — 신원이 없어서 훑는다

`MapMakerWindow.RouteIndex`(:653)는 고른 경로를 **스폰 좌표 하나로** 기억하고, 매 프레임 레인 목록을 훑어 첫 일치를 찾는다.

이 때문에 스폰 하나에 레인이 둘이 되면(통행방식 2벌 또는 스폰당 다중 경로) **뒤쪽 절반이 선택되지 않는다.**
같은 좌표 키 가정이 저작 쪽에도 있다:

| 위치 | 좌표 키 사용 |
|---|---|
| `RouteConfig.Rebuild` (RouteConfig.cs:33) | `routeMap[routes[i].Spawn]` — 스폰당 1벌만 담긴다 |
| `RouteEdit.GetRoute` (RouteEdit.cs:156) | 좌표로 찾아 쓴다 → 다른 벌의 저작을 덮어쓴다 |
| `LaneBuilder.GetRoute` (LaneBuilder.cs:79) | 두 벌이 같은 경유점을 따른다 |
| `TileAuthorRule.BlockedNodes` (TileAuthorRule.cs:185) | 검증이 한 벌만 본다 |
| `EnemyPathView.AddLane` (EnemyPathView.cs:113) | 좌표 중복을 지워 절반이 화면에서 사라진다 |

**있어야 할 주인**: 레인의 신원을 좌표가 아니라 **갈래 참조**로 든다.
이번 브랜치의 `TileRoute`(TileRoute.cs) + `RouteBranch`(RouteBranch.cs)가 그 자리다. 갈래 하나 = 컴포넌트 하나이므로 같은 스폰에 몇 개가 붙어도 구분된다. 현재 `RouteBranch.Keep`을 부르는 곳이 없어 미완성이다.

#### ✅ 2026-08-15 갱신 — F1은 이미 해결됨, 단 다른 길로

위 서술(TileRoute/RouteBranch로 해결)은 **채택되지 않았다.** 대신 팀은 더 가벼운 방법으로 같은 문제를 풀었다 — **`RouteData` 객체 참조를 신원표로 쓰는 방식.**

| 계층 | 08-05 상태(좌표 키) | 08-15 실제 코드(참조 키) |
|---|---|---|
| 저장 | 스폰당 1벌 (`routeMap[spawn]`) | `RouteConfig.routeMap`이 `Dictionary<좌표, Dictionary<일차, List<RouteData>>>` — 스폰당 **여러 벌**을 리스트로 보관 (`RouteConfig.cs:13`) |
| 계산 | 두 벌이 같은 경유점을 따름 | `LaneBuilder.AddSpawnLanes`가 벌 수만큼 `LaneData`를 각각 생성, `LaneData.Route`에 원본 `RouteData` 참조를 그대로 담음 (`LaneBuilder.cs:35-46`) |
| 저작 창 편집 대상 추적 | 좌표로 훑어 첫 일치 | `MapMakerWindow.IndexOfRoute`가 `lanes[index].Route == _routeData`로 **참조 비교**. 주석도 "좌표가 아니라 참조로 맞춘다"로 명시 (`MapMakerWindow.cs:858-870`) |
| 검증 | 한 벌만 봄 | `TileAuthorRule.BlockedNodes(lane.Route, cells)` — 그 레인의 route만 검사, 벌 수만큼 반복 호출됨 (`TileAuthorRule.cs:149-174`) |
| 표시 | 좌표 중복 제거로 절반이 사라짐 | `EnemyPathView.AddLane`이 `enemyLanes.Lanes` 전체를 레인 개수만큼 순회. 좌표 중복 제거(`pathCoords.Add`)는 **겹치는 칸을 두 번 안 그리는 표시 최적화**일 뿐 레인 누락이 아님 (`EnemyPathView.cs:93-119`) |

**결론**: `TileRoute`/`RouteBranch`는 검토됐다가 채택되지 않은 설계로 보인다. 두 파일 모두 씬·프리팹 어디에도 부착되어 있지 않고(GUID 검색 결과 0건), `RouteBranch.Keep` 호출부도 여전히 0곳이다. **미완성 코드가 아니라 죽은 코드**이므로, 이후 판단은 "완성시킬지"가 아니라 "삭제할지"다.

`MapMakerWindow.OnGUI`(:161~224)는 마우스 이동마다 타일 수집 · 격자 재구성 · 전 스폰 A\* · 프리팹 오버라이드 비교(`SerializedObject` 타일마다) · 문제 검사를 전부 다시 한다.
파일 상단 주석이 "매 리페인트마다 경로를 다시 계산해도 부담이 없다"고 적어 의도된 선택임을 밝히고 있다. 저작 도구이고 빌드에 들어가지 않으므로 **판단 보류**로 둔다.

---

## 3. 우선순위

예정 작업(스폰당 다중 경로 · 일차별 경로 · 물 통행 적)과 겹치는 순서로 매겼다.

원문의 근거였던 "예정 작업(스폰당 다중 경로 · 일차별 경로 · 물 통행 적)"은 08-15 기준 **이미 다 들어와 있다**. 아래는 08-15 재산정한 순서다.

| 순위 | 항목 | 이유 |
|---:|---|---|
| ~~1~~ | ~~**F1** 레인 신원~~ | ✅ **해결 확인 — 제외** (`RouteData` 참조 키 방식으로 이미 풀림, §5-F 갱신 참고) |
| ~~4~~ | ~~**B1** 포인터 타일~~ | ✅ **해결 확인 — 제외** (`HoveredTileData` 실존) |
| 1 | **C3** 캐시 중복 | `WaveSpawner._allPaths`가 `EnemyLanes` 결과를 또 들고 있다. 유일하게 "주인 둘" 상태로 남은 항목 |
| 2 | **C1** `GetWaypoints` 폴백 | `GetPath`는 private가 됐지만 `GetWaypoints`가 아직 public이고, 부를 때마다 전 타일 `EnemyLane`을 껐다 켜는 **Query 안의 부작용**이 남아 있다 |
| ~~3~~ | ~~**A1·A2** 배치 판정 중복~~ | ✅ **해결 확인 — 제외** (`AreaPlace.CanPlace` 삭제, 판정+높이 한 순회로 통합. §5 08-16 갱신 참고) |
| 3 | **B2** 포인터 아래 자리 재계산 | 프레임 캐시가 `if`·`null` 규칙 위반이라 삭제하고 매 프레임 재계산으로 되돌림. 모듈 6개 고정이라 비용은 무시 가능 — 원칙보다 규칙 준수를 우선한 의도적 선택 |
| ~~4~~ | ~~**C4·E1** 국소 정리~~ | ✅ **C4 해결 확인, E1 판정 오류로 제외 — 둘 다 제외** (2026-08-17 갱신 참고) |
| — | **F2** 저작 도구 | 빌드 제외 파일. 판단 보류 |

⚠️ **순위는 착수 순서 제안이지 승인이 아니다.** 어느 것도 사용자 지시 없이 손대지 않는다.

---

## 4. 검증 항목

각 항목을 고쳤을 때 아래가 성립해야 완료로 본다.

- 같은 계산이 한 프레임에 두 번 호출되지 않는다 (호출부를 세어 확인)
- 계산 담당은 소비자가 그대로 쓸 **최종 결과**를 낸다 — 중간 좌표 목록만 반환하지 않는다
- 실행 담당은 계산 결과를 **다시 순회하지 않는다**
- 검증·실행·제거 단계가 같은 결과를 각자 계산하지 않는다
- 보관 주체가 하나다 — 사본이 없다
- 결과를 만드는 곳과 보관하는 곳이 클래스 이름으로 구분된다
- Query에 상태 변경이 없다 (C1의 `EnemyLane` 부작용 제거 확인)

---

## 5. 범위 밖 관찰 (수정하지 않음)

`Assets/Script/Map/**` 밖이므로 기록만 한다.

| 위치 | 관찰 |
|---|---|
| `EnemyMovement.cs:105` | `board.GetWaypoints(0f)` 폴백 — 적 한 마리당 전체 경로 재계산 |
| `WaveSpawner.cs:37` | `_allPaths`가 `EnemyLanes`의 결과를 또 보관 |
| `WaveSpawner.cs:287` `GetScaleCount` | 스폰 시점과 정보 표시 시점에 같은 공식을 각각 계산 |

Map 쪽이 완결된 결과를 내주는 창구를 열면 위 세 곳은 조회 한 줄로 줄어든다.
