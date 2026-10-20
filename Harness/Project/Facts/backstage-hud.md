---
summary: 백스테이지 W1·C4·C5·W6/로비 L1 정본·표시 API·원본 보존. HUD 배치나 게임 데이터 연결 전에 읽기
status: active
updated: 2026-10-20
source: Task-20261019-061, PM 백스테이지·로비 HUD 계획 승인
---

# 백스테이지·로비 HUD

- 원본: `Assets/_ThirdParty/curtaincall_backstage_hud_v1/`. 제공 Runtime/조립기는 private 원위치 유지, namespace `CurtainCall.UI.BackstageHudV1`/`.Editor`.
- 정본 폴더: `Assets/Resources/Prefabs/UIs/BackstageHud/`. `BackstageHud.prefab`은 W1 시계/C4 공동 자금/C5 소지 슬롯/W6 집결, `LobbyHud.prefab`은 L1 방 정보, `InventorySlotTemplate.prefab`은 가변 슬롯 컨테이너다.
- 독립 확인 씬: `Assets/Scenes/Tests/BackstageHud/BackstageHud.unity`. 실제 대기실/로비 씬과 연결하지 않는다.
- 공개 코드: `Assets/Scripts/Systems/BackstageHud/`의 안전 조립 wrapper·합성 확인 제어·PlayMode 테스트. 제공 코드를 공개 복제하지 않는다.
- TMP 금액·집결·수량은 원래 영역 높이보다 폰트 줄 높이가 커 Ellipsis가 문장 전체를 숨길 수 있다. Rect 크기/위치는 유지하고 부족한 세로 줄 높이만 음수 bottom margin으로 보정했다. 전용 Medium에는 확인 버튼의 ‘템플릿’ 글리프도 포함한다.
- 전용 Static TMP Medium/Bold: `Assets/Resources/Fonts/BackstageHud/`. 기존 공유 폰트를 수정하는 방식은 사용하지 않는다. 기존 공유 폰트2개는 시작부터 dirty이며 동적 TMP(AtlasPopulationMode1)다. baseline 추가 SHA 차이/Play 캐시 변화 여부는 Task 검증 기록의 미결 항목이며 임의 복구하지 않는다.
- Play 확인 버튼11개: 평소/멈춤/소집/미수신/가변 슬롯/하단 우선/로비/긴 값/비활성/슬롯 템플릿/초기화. 긴 값은 로비 긴 값(7)↔자금 긴 값(10) 순환이며 초기화는 평소 합성값으로 돌아간다.
- `BackstageHUDView.ResetForRuntime()`은 미수신·숨김 초기화다. 확인 씬은 Start 이후 합성값을 주입한다. Inspector 편집 샘플을 실게임 초기 데이터로 쓰지 않는다.
- `SetContext(backstageActive, breakActive, inGatherArea, bottomSlotAvailable)`: W1은 대기실 쉬는 시간, C4는 대기실, C5는 대기실에서 외부 표시 요청, W6는 집결 구역·하단 사용 가능일 때다. 메뉴를 열었다는 이유만으로 backstageActive를 끄지 않는다.
- W1 `SetClock(double? remainingSeconds, float? remainingFraction, BackstageClockState state)`: 초 올림/m:ss, 표시 범위0~5999초·비율[0,1]. null/음수/비유한/범위초과는 —와 채움 숨김. Normal/Paused/Alarm은 호출자가 지정하며 자체 카운트다운·임계값·분모 계산은 없다.
- C4 `SetAmount(long?)`: 천 단위 구분, 음수도 그대로, null은 —. 긴 값은 Ellipsis이며 경제 규칙·상한을 추가하지 않는다. `SetChangeAccent(None/Increase/Decrease)`는 외부가 시작/해제를 결정하며 자체 차액 추론·자동 타이머가 없다.
- C5 `SetSlots(IReadOnlyList<InventorySlotData>)`: 데이터 수만큼64px 슬롯·간격8px, null/0개는 숨김. `ShowInventory()`/`HideInventory()`는 외부 호출이며 소지 제한·선택 수·수량 규칙을 정하지 않는다. 슬롯 증감은 이 컨테이너가 만든 자식만 관리한다.
- W6 `SetCounts(int? current, int? target)`: 0 이상 실제 집계만 표시, 무효는 — / —. 현재가 목표 이상이어도 게임 완료를 판단하지 않는다. 하단 우선순위도 외부 입력이다.
- L1 `SetContext(bool)`/`SetRoomInfo(code, host, currentMembers, targetMembers)`/`SetControlsEnabled(bool)`: 기본 코드 가림, 코드 변경·미수신·비활성·문맥 해제 시 다시 가린다. 공개 토글은 유효 코드·활성 문맥에서만 허용한다. 코드/방장 텍스트는 richText=false.
- L1 `CopyRequested`와 `OnCopyRequested`는 각각 C# 이벤트/UnityEvent다. 호스트는 원하는 경로 하나만 구독해 요청을 처리한다. 확인 씬은 C# 경로로 요청 횟수만 세며 클립보드 쓰기·복사 성공 토스트·네트워크 처리는 하지 않는다.
- 표시 입력32 PNG = 신규14 + 재사용18(C1 4/공연 HUD 받침5/공용 아이콘9). 재사용은 정본 바이트를 대조해 읽기 전용 참조, 신규14만 importer 설정 대상이다.
- 전체88실파일(meta 제외)/전체 PNG45를 보존한다. 표시32 PNG와 미리보기·생성 원본·참고 그림13 PNG를 혼동하지 않는다. Source/Tools/Docs/시트/목업/폰트·라이선스는 제작·확장 후보이며 런타임 미참조여도 삭제하지 않는다.
- 조립 메뉴: `Tools/Curtain Call/백스테이지 HUD/프리팹과 확인 씬 생성`. 기존 출력·dirty 씬·컴파일/임포트·Play·Prefab Stage를 거부한다. 기존 UI 정본을 재생성하지 않는다.
- 전수 경로·최신 SHA256/GUID·정본: Task `backstage-hud-inventory.csv`. 검사·미결: `backstage-hud-verification.md`. 테스트 결과와 사용자 최종 외형 승인은 구분한다.
- 현재 Task working/사용자 최종 확인 대기. 삭제·Git 저장·실게임 연결·제출·병합 없음.
