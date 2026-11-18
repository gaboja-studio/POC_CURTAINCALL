# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-19
- PaidAssets: `resource/ui-sounds`(8e994627) push, PR https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/30. 병합은 자동 권한 분류기가 막아 사용자 확인 대기.
- 부모 `resource/sounds`의 포인터 커밋 f34a8137 push는 pre-push 훅(verify-third-party)이 거부: 포인터가 private main에 없음 + 서브모듈 미커밋 6개(Conveyor Works·Synty·ithappy 머티리얼/FBX meta 재직렬화, 사운드와 무관, 보존).
- 클립 측정(0.25초 피크): bell 5.1초 자연 감쇠 → UI-104 3초+0.8초 페이드(시트 0:03), rumble 5초 → UI-905 1.5초+0.5초 페이드, poof·Back 1은 전체.
- 검사: EditMode `SoundCueTests` 6/6 PASS. 플레이 모드 eval로 4개 큐 재생·없는 ID false·재사용 간격 차단·UI-104 3초 페이드 후 정지 확인.
- `WaitingRoomHudPlayTests`는 SetUp이 삭제된 `Assets/Scenes/Start_Lobby/Start.unity`를 불러 실패(기존 문제, 이번 변경 전부터). 소집 종소리 단언만 카탈로그 UI-104 확인으로 바꿨다.
- `Assets/Resources/Sounds/EscapeRouteMaterials/escape-labels.asset`가 카탈로그 저장(SaveAssets) 때 함께 기록됨(에디터에 남아 있던 탈출로 텍스처 변경). 이 Task 변경 아님 — 커밋에서 제외.

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- check-work: 안전성·문서·컴파일 PASS, 테스트 SKIPPED(위 SoundCueTests는 MCP로 따로 실행).
- 확인 씬 `Assets/Scenes/Tests/Systems/Sound/UiSoundTest.unity` + `SoundCuePreview`(카탈로그 전체 버튼·숫자키, 실제 RestTimer 프리팹으로 35초/12초 재현·멈춤·x3). 플레이 확인: 31초 시작 시 Warning 진입과 함께 UI-905, Critical(0:07)에서 UI-906.
- UI-905·906 음량이 02:46에 1.0으로 저장돼 있음(사용자 창 조절로 보고 유지).

- 2026-11-19 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2차: 실제 연결 재점검 — UI-104 대기실 두 씬 ExplorationAnnouncements(showLegacyUi=false, 활성) 확인. 탈출로 내 사망은 카드가 없어 소리 누락 → EscapeLocalDeathSound 추가.
- ExplorationTimerView.Start는 옛 시계를 그릴 때만 CampaignRun을 만든다(테스트 씬에서 런 진행이 생기지 않게).
- 카탈로그에 기본 상황 6개 추가(SaveAssetIfDirty로 이 에셋만 저장, 사용자 조정 음량 유지).
- 검사: SoundCueTests 9/9 PASS, 코드 위치 검색이 6개 상황 모두 실제 줄을 찾음, 테스트 씬 플레이에서 소집(ExplorationTimerView.Show:92)·공연 사망(HudUIManager.ShowDeathWarning)·탈출로(EscapeLocalDeathSound.Notify) Played, 동료 사망 무음 확인. check-work 컴파일 PASS.
- 2026-11-19 제출 전 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=EditMode SoundCueTests 9/9 PASS (WaitingRoomHudPlayTests는 삭제된 Start_Lobby 씬 참조로 SetUp 기존 실패)
