# 공연 데이터 연결 검증
2026-10-20, Unity6000.3.25f1/Pipeline CLI, 현재 worktree.
## 실제 변경 파일
- Assets/Scenes/UI/CommonPerformanceUI.unity: 정본 C3 인스턴스에1인 어댑터/마커 참조 추가.
- Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity 및 HumanCannon/HumanCannon.unity: 공간 PerformanceAudience 배치/게임 참조.
- Assets/Scripts/UI/SceneIntegration/PerformanceSceneBinding.cs: 인간대포 실제 불 전방,1인 분기,활성 벽/완료 시간 표시.
- 같은 폴더 SoloPerformanceProgress.cs(+meta), Editor/PerformanceDataInstaller.cs(+meta): 정본 그래픽1인 표시, 에디터의 반복 가능한 연결.
- 같은 폴더 Tests/PlayMode/PerformanceSceneBindingPlayTests.cs:1인/2인/4인 전환·마커/무효값 회귀.
- Assets/Scripts/Systems/Reward/PerformanceRewards.cs 및 Logic/PerformanceRewardModel.cs: 가변 환호/야유 증감, 정산 전 도착 관찰, 두 공연 임시 cap/cut 설정.
- 같은 Reward 폴더 PerformanceAudience.cs(+meta), Logic/AudienceEncounter.cs(+meta): 호스트 플레이 관찰·중복 방지·연속 조건.
- 같은 Reward 폴더 Tests/EditMode/AudienceReactionTests.cs(+meta):점수/사망/절단/정산/반복 판정9개 추가.
- Assets/Scripts/Systems/Settings/AudienceReactionSettings.cs(+meta), Assets/Resources/Settings/GameSettings/Tricks/Rewards/temporary-audience.asset(+meta):문서 임시 값과 판정 거리.
- Assets/Scripts/MainStage/HumanCannon/HumanCannonTrampoline.cs 및 HumanCannonTrampoline.GroundBounce.cs:호스트 승인 반동 이벤트만 공개.
- Assets/Scripts/MainStage/Tightrope/Saws/TightropeSaws.cs:수직 톱 출현 ID·협동 관문 위치 읽기 API만 공개.
- Docs/Guides/Ui-scene-integration/audience-rules.md, performance-hud.md, next-plan.md 및 Task 메타/범위/계획/todo/handoff/이 문서.
## 자동 검증
- Compile PASS. EditMode CurtainCall.Reward.Tests40/40(기존31+신규9), PlayMode CurtainCall.UI.SceneIntegration.Tests13/13.
- .harness/local/test-results-editmode.json 및 test-results-playmode.json.
- verify-fast: 첫 제한 환경 호출은 submodule Git 메타 접근 거절로 FAIL, 권한 있는 재호출은 Fast OK(경고9).
- check-work: scope FAIL289, 자체 후속 단계 SKIPPED. 개별 -Base HEAD FAIL14(작업11+기존 정책3). 위 Unity 검증은 개별 실행 결과다.
- Task044/062의 광범위한 활성 구역 등록과 겹친다. 사용자 공통 규칙 구현 지시를 setup에 명시했으나 검사 정책 자체는 수정/우회하지 않았다. 저장/병합 전 담당 구역 조정이 남는다.
## 호스트 실행
- 외줄: 균형을 테스트 중에만 꺼 위치5m→15m 주입. 실제 관찰 LateUpdate에서 회피5, ServerCut 후35, 야유 API 후20, ServerKill 후0/Dead. 프레임3193→3379. C2 8:18→8:17.
- 인간대포: 기존 Campaign API로 해당 공연 진입. Arm 호출 후 C2 2:10→2:08, 프레임81→178, 개인 cap100,1인 C3/불 표시.
- 반동: 처음 단순 요청은 대기 요청 식별자가 없어 거절(0점). 테스트에서 올바른 대기 sequence/epoch를 준비한 재실행은 호스트 승인 반동1회/15점. 도착 judgements 주입으로 정산 전 환호 후30/수입30.
- 근거 .harness/local/audience-tightrope.txt, audience-cannon.txt, 두 png. 실제 입력/목적지 착지가 아닌 합성 상태 검증임을 유지한다.
- 최근 인간대포 실행 시 Console groundTruth error0/warning0. Pipeline 버퍼의 TMP Destroy 오류는08:42 이전 실행 이력이며 이번 실행09:35와 구분했다.
## 남은 확인
- 실제2~4클라이언트 동기화/재접속·목마/봉/아래층2인 조작·근접 회피/도착 경계·불 접촉·전 코스/해상도는 미실행.
- source SetProgress의 데이터 누락 정책 유지: 늦은 참가로 과거 사망 위치를 관측하지 못한 경우 C3 전체가 unavailable일 수 있다. 실제 다인 재접속 검증에서 확인한다.
- 사용자 확인 절차와 임시 가정은 audience-rules.md. 결과창·환호 팝업·추락 알림의 새 화면은 후속.
- 이번 작업에서 commit/push 없음. 기존 로컬 폰트/프리팹/정책/서드파티 변경은 보존했다.
