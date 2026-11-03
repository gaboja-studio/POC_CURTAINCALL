# 탈출로 A 원본 재사용·UI 차이 조사

2026-11-03 / @RagSpirin / feat/exit-route/design-map.
사용자 요청: 균형 UI처럼 원본과 다른 부분을 전반적으로 조사한다. 이번에는 게임 코드·씬·설정을 변경하지 않고 확인 결과를 남긴다.

후속 사용자 승인으로 아래 차이를 보수했다. 현재 반영 결과와 추가 조작 수정은 [ui-reuse-fixes.md](ui-reuse-fixes.md)를 따른다. 아래 표는 수정 전 조사 기록이다.

## 조사 범위와 방법

- origin/dev 및 작업 브랜치 fetch. 현재 HEAD와 origin/dev 차이0/0.
- 실제 저장된 A, Tightrope, HumanCannon, WaitingRoomBase와 CommonMenuUI/CommonInteractionUI/CommonPerformanceUI/CommonWaitingRoomUI 총8개 씬을 읽었다. 원본은 PreviewScene으로 열고 저장 없이 닫았다.
- 프로젝트 MonoBehaviour845개, UI RectTransform1,451개를 수집했다. 누락 스크립트0, 네이티브 검사에서 감지된 끊긴 ObjectReference0. 선택적 null과 기능별 미사용 참조는 결함으로 세지 않았다.
- 프리팹 경로뿐 아니라 실제 씬의 크기·색·폰트·스케일·활성 상태·UI 바인딩과 런타임 로드 조건을 비교했다. 필요한 UI 씬은 Build Settings에 모두 활성화돼 있다.
- 타이머는 원본/A 복제본에 같은480/120/20/0초 스냅샷을 전달해 표시 데이터를 추가 비교했다. 임시 편집 씬만 사용했다. 실제 Play/MPPM은 실행하지 않았다.
- 증거: `.harness/local/reuse-audit/`의 씬별 JSON8개, `timer-states.json`. 조사 후 A만 열려 있고 dirty0이다.

## 확인된 불일치·공통 결함

| ID | 대상 | 원본 / 현재 A | 영향·처리 우선순위 |
|---|---|---|---|
| U01 | 탈출 타이머 CanvasScaler | 원본 BackstageHudCanvas는 Expand, A EscapeTimerCanvas는 MatchWidthOrHeight(.5) | 16:9 외 화면에서 균형 UI와 타이머가 서로 다른 비율로 커진다. 원본 방식으로 맞출 후보 |
| U02 | 장치 작동 알림 CanvasScaler | 원본 WaitingRoomNotificationsCanvas는 Expand, A 장치 작동 알림은 MatchWidthOrHeight(.5) | 같은 알림 프리팹이어도 화면 비율에 따라 크기가 달라진다. 원본 방식으로 맞출 후보 |
| U03 | 균형 게이지 키 재지정 표시 | 원본과 A 모두 SetBalance에 문자열 A/D를 직접 전달 | 균형 키를 바꿔도 화면 키 표시는 A/D다. A만 다른 문제가 아닌 공통 표시 결함. 실제 입력 Reader는 별도로 적용되므로 판정보다 표시 연결을 고칠 대상 |

CanvasScaler의 설치된 UGUI 코드에서 Match는 로그 보간, Expand는 가로/세로 배율 중 작은 값을 사용한다. 예를 들어2560×1080에서는 Match(.5)가 Expand보다 약15.5% 크게 나온다. 1920×1080과 같은 기준 비율에서는 차이가 드러나지 않는다.

근거:
- `.harness/local/reuse-audit/escape_route.json`, `CommonWaitingRoomUI.json`의 canvases.
- `Assets/Scripts/EscapeRoute/EscapeRopeBalanceDisplay.cs:42`, `Assets/Scripts/UI/SceneIntegration/PerformanceSceneBinding.cs:252`.
- `Library/PackageCache/com.unity.ugui@f3ea87521141/Runtime/UGUI/UI/Core/Layout/CanvasScaler.cs:320`.

## 확인된 연결 차이 — 표시 범위를 정해 연결해야 할 항목

| ID | 대상 | 현재 연결 | 판단 |
|---|---|---|---|
| U04 | 사망·관전 C11 | 원본은 PerformanceSceneBinding→CommonPerformanceUI→PerformanceSpectatorBinding. A에는 PerformanceSceneBinding이 없어 C11 경로가 없다 | 원본 관전 UI와 다르게 나오는 구조가 확정됐다. 캠페인은 대기실 카메라를 옮겨 관전 동작을 이어갈 수 있지만 정식 C11 대신 기존 CameraSpectator IMGUI 표시로 남는 코드 경로다. 직접 A 실행은 CameraSpectator 자체가 비활성. 두 실행 경로를 구분해야 한다 |
| U05 | 외줄 진입·이동·옆줄 점프 설명 | 원본에는 TightropeControlsPanel과 C8 안내 배너, A에는 해당 컴포넌트가 없다 | A의 균형 게이지는 같아도 순차 조작 설명은 동일하지 않다. 원본 배너는 첫날 Performance/Tightrope에서만 켜지므로 그대로 복사해도 Escape에서는 표시되지 않는다. 탈출 진행용 표시 조건이 필요 |
| U06 | 대포 C7/C15 보조 가이드·툴팁 | 원본 HumanCannonUiBinding은 CommonPerformanceUI의 Guide/Tooltip을 사용한다. A는 그 바인딩이 없고 C6 CommonInteractionUI를 사용 | 탑승·그네 도약·운반 안내가 전부 사라진 것은 아니다. C6에서 이미 같은 입력/가능 조건을 안내한다. 원본의 구간·리프트 전용 설명까지 가져오면 A에 없는 코스 설명이 섞이므로 필요한 안내만 연결할 대상 |

U04~U06은 서로 같은 CommonPerformanceUI 등록 의존성에서 비롯된다. PerformanceSceneBinding을 A에 통째로 붙이면 공연 호응·진행·보상 UI까지 따라올 수 있으므로 표시용 연결을 나누는 편이 적절하다. 관전의 최종 화면은 실제 다인 플레이로 별도 확인해야 한다.

근거:
- `Assets/Scripts/UI/SceneIntegration/CommonUiSceneLoader.cs:146`, `:154`, `:163`.
- `Assets/Scripts/UI/SceneIntegration/PerformanceSpectatorBinding.cs:27`, `:32`.
- `Assets/Scripts/Systems/Camera/CameraSpectator.cs:26`, `:147`.
- `Assets/Scripts/EscapeRoute/EscapeSceneTransfer.cs:153` — 대기실 카메라/리그를 목적 씬으로 이동.
- `Assets/Scripts/UI/SceneIntegration/TightropeControlsPanel.cs:108` — 첫날·공연·Tightrope 조건.
- `Assets/Scripts/UI/SceneIntegration/HumanCannonUiBinding.cs:52`, `:85`.

## 별도 스타일·저장 상태 차이

| ID | 대상 | 실제 차이 | 판단 |
|---|---|---|---|
| U07 | 장치 알림 토스트 | A 패널580/본문500px, 원본384/312px. A는 a-mechanism-notifications 폰트, 원본은 Notifications/Pretendard-Medium | A 작성 도구가 긴 한글 장치 문구·글리프 보완을 위해 명시적으로 만든 설정이다. 자동으로 원본 폭/폰트로 되돌릴 결함은 아니다. 다만 원본 폰트 개선이 자동 전파되지 않는 별도 에셋이다 |
| U08 | 타이머 글자 크기 | 저장된 Countdown fontSize A36.85, 원본40. 양쪽 자동 크기28~40은 같음 | 같은 네 가지 스냅샷을 준 편집 복제본에서도 저장 글자 크기 필드 차이는 유지됐다. 실제 Canvas의 런타임 레이아웃까지 검증하지 않았으므로 항상 작게 보인다고 확정하지 않는다. 표본 숫자/색/아이콘 차이 상당수는 SetSnapshot이 갱신하는 상태값이다 |

U07 근거: `Assets/Scripts/EscapeRoute/Editor/EscapeAIndependentControls.cs:88`, `:91`, `:93`.
U08 근거: `timer-states.json`, `RestTimerView.ApplyContent`의 숫자·색·낮밤 아이콘 갱신 코드.

## 같은 경로이거나 의도적으로 유지한 부분

| 대상 | 현재 확인 결과 |
|---|---|
| 균형·추락 위험 게이지 | 직전 수정 이후 그래픽/배치32개와 Expand 설정 일치. 같은 PerformanceHud·BalanceRiskBlink·BalanceZoneColors·HudLayout 사용 |
| 균형 수치·매달림 위험 | 양쪽 모두 PlayerBalance/GreenLimit/WarningLimit/RedTimeRatio 및 HangDangerRatio를 전달. A는 자기 외줄 규칙 적용 중에만 표시 |
| F 안내·외곽선·구조 진행 링 | 같은 CommonInteractionUI 씬·InteractionPromptView 사용. A InteractionSceneBinding 유효. 봉/대포/운반·배선/버튼 안내도 여기서 처리 |
| F·점프 키 재지정 안내 | C6는 BindingLabel/BindingPartLabel로 키/도움말 치환. U03의 균형 키 글자는 이 별도 경로를 쓰지 않는 예외 |
| 인벤토리·장비 쿨타임·내구도 | 같은 CommonMenuUI→CommonInventoryUI/InventorySlot 계열 사용. escape_route도 활성 공간 목록에 포함. 별도 A 복제 슬롯 없음 |
| ESC 메뉴·설정·음성 상태 | 같은 CommonMenuUI/CommonSettingsUI/VoiceStatusUI 사용. A도 공용 메뉴 로더의 지원 공간에 포함 |
| 손에 든 도구 외형 | A에 HeldPropsView가 없는 것은 발견했지만 실제 HumanoidPlayerPresentation.Items가 선택 도구를 생성한다. HeldPropsView는 Humanoid가 없는 캐릭터용 대체 표현이므로 누락 결함으로 세지 않음 |
| 탈출 타이머 | 공용 RestTimerV28 뷰/어댑터를 사용하며480초·철창 진입 시작·호스트 시각은 A 전용 확정 규칙. 대기실 제한시간으로 덮어쓸 대상 아님 |
| 공연 호응·진행·보상 HUD | C1/C3 등은 A에 자동 적용하지 않은 의도된 분리. 탈출 성공 조건은 기존 전원 도착/리타이어 집계 유지 |
| 절단 실루엣 HUD | 원본 공통에 없던 A 전용 HUD는 앞선 지시로 제거된 상태. 재설치 대상 아님 |
| 착지 발밑 표시 | 같은 HumanCannonGroundMarkers·재질·반경. 안전 착지 색 보완은 A true/원본 false인 의도된 맵 설정 |
| 대포·그네·외줄·목마·운반 | 같은 HumanCannon/Tightrope 공통 컴포넌트 경로. A 활성 봉15개, 대포4개, 트램폴린1개 및 외줄/매달림/목마/받기 연결 존재. 위치·좌석·반동·잡기·공중 보정 수치 차이는 맵 튜닝으로 분리 |

참고: A 공중 보정 속도는 봉13개/대포4개가6m/s, 봉2개가3m/s다. 원본 HumanCannon 활성 봉4개/대포10개는4.5m/s다. A 컨베이어6개 상한8.5m/s·다른 봉은 공통 상한 사용. 이것을 일괄 복사하면 동선 난도가 바뀐다. 이번 조사에서 변경하지 않았다.

## 후속 순서

1. U01/U02 화면 스케일을 원본과 맞추기. A와 작성 도구만 보수할 수 있다.
2. U03 균형 키 표시를 공용 Reader 바인딩에 연결하기. 원본/A 양쪽 표시 수정 대상이다.
3. U04 관전 전용 UI 연결을 공연 점수 HUD에서 분리하기.
4. U05/U06은 탈출로에 필요한 설명만 추려 연결하기. 원본 공연의 첫날/구간 설명을 통째로 적용하지 않는다.
5. U08은 같은 실제 화면 크기에서 타이머 렌더/레이아웃 확인 후 판단. U07의 넓은 알림과 글리프 보완은 유지한다.

이번 조사는 정적 연결·설정·편집 표시 데이터 조사다. 실제 다인 관전/가림/안내 전환·플레이 체감 검증을 완료한 기록이 아니다.
