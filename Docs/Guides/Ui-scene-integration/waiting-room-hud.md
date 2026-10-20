# 대기실 기본 HUD 적용
2026-10-20 · 최신 dev4d75f7e/병합981fe7f 위의 추가 작업. 저장된 Assets 정본 프리팹을 인스턴스로 사용한다. 원본 프리팹·PNG·폰트·9-slice 및 외부 인계 package는 변경하지 않는다.

## 씬과 편집
- 공간: `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity` → `UI/WaitingRoomUiBinding`, `UI/ExplorationAnnouncements`.
- 공통: `Assets/Scenes/UI/CommonWaitingRoomUI.unity` → `UI/BackstageHudCanvas`, `UI/BroadcastCaptionCanvas`, `UI/WaitingRoomNotificationsCanvas`.
- `CommonUiSceneLoader`가 공간 등록에 따라 로컬 additive 로드/해제한다. NGO의 동기화 대상에서는 공통 UI 씬을 제외한다. UI에는 NetworkObject/새 EventSystem/입력 차단기가 없다.
- 공통 씬을 Additive로 열어 해당 Canvas 또는 자식을 선택한 뒤 **Tools → Curtain Call UI → Preview selected UI in Scene View**로 보면서 위치를 편집한다. Layout 미리보기는 게임 값과 무관하며 저장/Play 진입 시 원래 숨김 상태로 복원된다.
- 최초 설치용 `WaitingRoomUiInstaller.Apply`는 이미 설치된 씬을 재생성하지 않는다. 이후에는 씬 인스턴스를 직접 편집한다.

## 정본과 데이터 연결
| 표시 | Assets 정본 | 실제 원천·동작 |
|---|---|---|
| W1 탐색 시계 | `BackstageHud/BackstageHud.prefab` | CampaignRun.State.TimeRemaining/Timer/AlarmRaised, GameSettings.Base.Run의 RestDuration/GuideRestDuration. 출구 전 멈춤, 탐색/소집 상태. 밤에는 숨김. 시간·실패 판정은 변경하지 않음. |
| C4 공동 자금 | 같은 HUD | PerformanceRewards.Balance. 쉬는 시간·밤 표시, 감소 시 짧은 강조. 값 미수신은 unavailable. |
| C5 내 소지 슬롯 | 같은 HUD/InventorySlotTemplate | PropCarry.LocalClientId/Held/Slots/HasBackpack. 내 소지품·팔 상태·배낭 한도 변경을 반영하고 4초 후 숨김. 마지막에 든 기물이 실제 내려놓기 대상이라 강조. 타인 목록은 표시하지 않음. |
| W6 집결 | 같은 HUD | StageEntranceGather.InsideCount/AliveCount. 로컬 생존자가 실제 집결 BoxCollider 안에 있고 쉬는 시간일 때만 표시. 방송 자막 중 하단 겹침 방지. |
| C9 방송 | `Captions/Captions.prefab` | ExplorationTimerAnnouncer.Announced. 30/10초 방송과 소집 경보를 4초 표시. 기존 종소리 유지. |
| C8 알림 | `Notifications/SystemNotifications.prefab` | 소집 경보 상단 표시, 밤 진입 알림, 기물 요청 결과/소지 변경 토스트. 반복된 동일 실패도 RequestAnswered 이벤트로 다시 표시. |

프리팹 경로의 앞부분은 모두 `Assets/Resources/Prefabs/UIs/`이다. 모든 표시 시간은 CommonWaitingRoomUI Inspector의 직렬화 필드로 조정한다.

## 이번에 유지한 미완성 게임 기능
- 상점은 아직 tool-a/tool-b/escape/backpack 임시 상품이다. bread/water/bandage 등 일치하는 ID는 CommonIconCatalog 정본 아이콘을 사용하고, 없는 ID는 delivery 상자 아이콘과 실제 상품명 토스트로 표시한다. 상품과 효과를 임의로 바꾸지 않았다.
- 숫자키 1~5 선택 API가 없어 이번에 추가하지 않았다. 슬롯에는 작동하지 않는 숫자키를 표시하지 않는다. 기존 **F 길게 = 마지막 기물 내려놓기** 규칙을 유지한다.
- W2 수화기 패널, W3 스케줄표 작성창, W4/W5 월드 이름표의 기존 UI는 아직 대체하지 않아 유지한다. C6 근접 안내는 후속 작업에서 정본으로 교체했다(`interaction-highlights.md`). 신규 백스테이지·탈출로 씬은 만들지 않는다.

## 임시 표시 정리
- `ShopStorageViews`의 구형 SharedFundsView 컴포넌트만 제거. 같은 오브젝트의 FloorPropsView/HeldPropsView는 보존한다.
- ExplorationTimerView의 `showLegacyUi=false`: 구형 ExplorationTimerCanvas를 생성하지 않는다. Announcer와 종소리 AudioSource는 유지한다. 해당 오브젝트를 UI/ExplorationAnnouncements로 이동했다.
- PropCarry의 `showHud=false`, StageEntranceGather의 `showStatus=false`. 소지 장부·네트워크·집결 판정 컴포넌트는 유지한다.
- ShopStorage는 DontDestroyOnLoad 관리자이므로 최상위에 유지한다. UI 정리 때문에 UI 하위로 옮기면 안 된다.

## 실제 수정 파일
- `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`
- `Assets/Scenes/UI/CommonWaitingRoomUI.unity` 및 meta (신규)
- `Assets/Scripts/UI/SceneIntegration/CommonWaitingRoomUI.cs`, `WaitingRoomUiBinding.cs` 및 meta (신규)
- 같은 폴더 `CommonUiSceneLoader.cs`, `UiRuntimeFontFallback.cs`
- 같은 폴더 `Editor/WaitingRoomUiInstaller.cs`, `Tests/PlayMode/WaitingRoomHudPlayTests.cs` 및 meta (신규)
- `Assets/Scripts/WaitingRoom/ExplorationTimer/ExplorationTimerView.cs`
- `Assets/Scripts/WaitingRoom/ShopStorage/PropCarry.cs` (표시 속성·요청 결과 이벤트만)
- `ProjectSettings/EditorBuildSettings.asset` (공통 UI 씬 등록)
- 현재 Task setup/plan/todo/handoff 및 이 가이드/검증 기록.

## 사용자가 Unity에서 확인할 3가지
1. **Start → 공연 → 대기실:** 상단 시계와 우측 자금이 한 번만 뜨는지, 문 밖으로 나간 뒤 시간이 줄고 소집 때 빨간 시계·방송·종소리가 나오는지 확인한다.
2. **기물·집결:** 수령/내려놓기/배낭으로 실제 슬롯 수와 잠깐 표시가 맞는지, 일부 단원만 통로에 있을 때 n/m, 전원이 모이면 공연으로 넘어가며 대기실 HUD가 사라지는지 확인한다.
3. **밤 → 다음 아침:** 시계 숨김, 스케줄표 앞 F를 짧게 눌렀다 떼기/ESC, 전원 준비 완료 후 문 해제·아침 시계·이동 입력을 확인한다. 2~4인 호스트/클라이언트와 재진입에서도 반복한다.

검증 결과와 미실행 항목은 같은 폴더 `waiting-room-verification.md`에 기록한다.
