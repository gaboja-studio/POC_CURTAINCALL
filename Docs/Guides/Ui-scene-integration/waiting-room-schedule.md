# W3 스케줄표 적용
2026-10-21. 실제 Start→Lobby→공연/WaitingRoomBase 경로만 대상으로 한다. 과거 임시 씬은 적용·검증하지 않는다.

## 구성
- Assets/Resources/Prefabs/UIs/ScheduleUi/SchedulePanel.prefab 정본을 CommonWaitingRoomUI의 UI/SchedulePanelCanvas로 배치했다. 원본 파일·9-slice·폰트는 보존한다.
- 정본 카드의 씬 인스턴스에 Button을 추가해 공연 항목 자체를 클릭하면 묘기→다음 묘기→미편성을 순환 선택한다. 별도 하단 선택 버튼은 제거했다. 확정·준비만 CommonButtons 정본 인스턴스를 사용한다.
- 일정표는 1~3일차 두 공연·난도·확정 여부·전문가 일정을 표시한다. 현재 일차 표시와 편집 가능 여부는 별도다. 기존 난도는 묘기의 출연 순번을 기준으로 한다.
- 확정/준비는 패널 아래 우측에 유지한다. 편집 가능한 카드만 클릭·호버 강조하며 고정/확정/참가자 카드는 계속 표시하고 클릭을 막는다. 원본 레이아웃에서 footer 너비만 씬 override로 줄여 새 버튼과 겹침을 막았다.
- 스케줄표 F 상호작용, 클로즈업 카메라, 로컬 캐릭터 숨김, 커서/입력 차단은 기존 ScheduleBoardView가 담당한다. 닫기/ESC/거리 이탈/씬 교체 때 복귀한다.
- WaitingRoomBase에서 showLegacyUi=false로 구형 IMGUI 스케줄 창을 대체했다. ScheduleBoardView 및 실제 스케줄표 사물·카메라는 유지한다. 과거 테스트 씬의 표시 코드는 건드리지 않는다.

## 실제 연결과 4일차 처리
- WaitingRoomScheduleUI → ScheduleBoard.Set/Confirm/RequestReady → 기존 호스트 일정 공유·CampaignRun.SetSetlist/SetReady 흐름을 사용한다.
- 호스트만 편집·확정, 참가자는 표시와 자신의 준비/취소를 사용한다. 확정 전 빈 칸은 기존 판정으로 거절한다. 1일차는 고정,1일차 밤에는2·3일차 작성 후2일차 잠금,2일차 밤에는3일차 작성 후 잠금이다.
- 3일차 밤은 편성할 다음 공연이 없으므로 확정 버튼 없이 준비 가능하다. 4일차 아침과 기존 최종 진행은 유지한다.
- ScheduleBook의 네트워크 배열 길이4일×2칸은 유지하지만 편집 대상과 확정 잠금 대상에서4일차를 제거한다. 수신 데이터에 남은4일차 항목도 비우고3일차 밤의 확정 불필요 상태를 복원한다. 혼합 버전 클라이언트 지원을 의미하지 않으며 같은 빌드를 사용한다.
- 새 Dev 런에서2일차 이상으로 바로 시작하면 이전 일정이 비어 있을 수 있다. 표시를 위해 가짜 과거 공연을 채우지 않는다.

## 변경 파일
- 씬: Assets/Scenes/UI/CommonWaitingRoomUI.unity, Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity.
- 신규: Assets/Scripts/UI/SceneIntegration/WaitingRoomScheduleUI.cs, Editor/WaitingRoomScheduleInstaller.cs, Tests/PlayMode/WaitingRoomSchedulePlayTests.cs 및 meta.
- 기존: UiRuntimeFontFallback.cs, NightSchedule/ScheduleBoardView.cs·ScheduleBoard.cs·Logic/ScheduleBook.cs·Tests/EditMode/ScheduleBookTests.cs.
- 설치기는 최초 배치용이며 이미 설치된 씬에서 중복 실행을 거절한다. 이후 위치는 공통 UI 씬에서 수정한다.

## 사용자 확인
1. WaitingRoomBase Play→Dev 배지 또는 Ctrl+Shift+U(Mac ⌘+Shift+U)→1일차 밤 진입→스케줄표 앞 F.
2. 2·3일차의 공연 카드 네 항목을 클릭해 공연 선택, 일정 확정.2일차만 잠기고 준비 완료가 활성화되어야 한다.1인 Dev에서 준비하면 즉시 다음 아침으로 진행한다.
3. 2일차 밤에는3일차만 수정·확정.3일차 밤에는 편집/확정 없이 준비 가능, 준비 후4일차 마지막 아침으로 진행해야 한다.
4. 다인에서는 참가자 편집 불가, 호스트 변경 공유, 준비/취소·인원수 동기화와 전원 준비 후 전환을 확인한다. ESC/닫기 후 카메라·커서 복귀도 확인한다.

## 검증
- 컴파일 PASS. 실제 LAN 호스트 WaitingRoomBase의 W3 핵심 PlayMode1/1 PASS(`.harness/local/w3-flow1.json`): 버튼 편집·빈칸 확정 거절·잠금·준비/다음 아침·닫기 입력 해제·3일 밤 추가 편성 없음·구형4일 데이터 정리·4일 마지막 아침 진입. 이전 임시 씬/전체 회귀·별도 EditMode suite는 실행하지 않았다.
- 이전 버튼 방식의1920×1080 실제 화면에서3열·한글·확정/준비 배치 확인(`.harness/local/w3-schedule.png`). 정본9-slice/폰트 원본 유지. 실제 물리 마우스 입력 대신 자동 검사에서 프리팹 버튼 이벤트를 호출했다.
- 미실행: 실제 다인 동시 편집/준비 취소·재접속, Mac 물리 입력, 전체 Start부터 완주·빌드·다른 화면비. 이전 카메라/SpaceLink 실패는 과거 씬을 포함하므로 이번 검증으로 확대하지 않는다.
- 카드 클릭 변경 후 컴파일 PASS·핵심 PlayMode1/1 PASS(w3-card-click1.json). 화면 카드 중심에서 실제 UI Raycast→PointerClick으로4칸 선택, 하단 버튼 없음·확정 후 카드 표시/클릭 잠금·준비/최종아침 확인. 이번 물리 마우스·추가 화면 캡처는 미실행.

## M2 변경 (2026-10-29, Task-079)
- 준비 완료/취소는 스케줄표에서 빼고 숙소 침대 F(`NightBed`)로 옮겼다. 스케줄표 화면의 준비 버튼은 숨기고(`readyButton` 비활성), 호스트의 "확정 뒤 준비" 조건도 없앴다. 위 준비 관련 설명은 이전 동작이다.
- 공연 선택·편성 UI는 보류 상태로 그대로 둔다. 편성하지 않은 2일차 이후 공연은 외줄타기로 진행한다.
