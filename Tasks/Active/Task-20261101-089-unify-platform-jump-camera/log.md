# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-01 환경: origin/dev 최신(f4712c04). doctor FAIL 2(기존 Task 번호 중복, Unity CLI 설치 목록) — 이번 작업과 무관. 이 worktree에 연결된 에디터 없음(열린 에디터는 메인 `D:\github\POC_CURTAINCALL`).
- 2026-11-01 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
  - 문서/규칙 FAIL 원인: 다른 Task `Task-20261029-079-waiting-room-m2/log.md` 138줄(hard 130). 이번 변경과 무관, 담당 Task 문서라 수정하지 않음.
  - 컴파일 단독 실행(`verify-unity.ps1 -Compile`): ENV_BLOCKED(editor not visible). worktree에 Library 없음 → 에디터로 열어 import 후 재검사 필요.
  - 대신 정적 확인: 바뀐 필드·리플렉션 이름(`platformView`, `flightView`, `seatedView`) 참조와 제거한 `firstView`/`firstPersonView` 사용처 없음 확인.
- 다음: 이 worktree를 Unity로 열어 컴파일 확인 → 사람 플레이 확인(대기실·외줄 출발/도착·인간 대포·탈출로 점프 공중 조작과 자유 3인칭, 대포 비행 3인칭, 탈출로 계단 끝 전환).
- 2026-11-01 추가 지시: 카메라 벽 설정을 탈출로에도 동일 적용. 대기실 전용 씬 이름 판정 2곳(디렉터 충돌·가림 반투명 제외)을 `UsesWallCollision`으로 묶고 `escape_route*` 추가. 탈출로는 진입 시 활성 씬이 되므로 활성 씬 판정 유지. 컴파일 미확인(ENV_BLOCKED 동일).
- 2026-11-01 에디터 연결 후 검사(이 worktree 전용 에디터):
  - 컴파일: PASS (`verify-unity.ps1 -Compile`, 콘솔 compilationFailed=false. 콘솔 오류는 첫 import의 서드파티 ShaderGraph·SVG 임포트 오류뿐).
  - EditMode: TightropeOrbitFramingTests 16/16, TightropeMovementFeedbackTests 11/11, ControlRulesTests 17/18.
    실패 1 `Locomotion_DefaultSettings_AreConfirmedValues`(달리기 배율 기대 1.5, 실제 2.0)는 이 브랜치 미변경 설정. 마지막 변경은 dev 커밋 1673f9d7 → 기존 dev 문제.
  - PlayMode: CameraViewsPlayTests 9/9(대기실 벽 충돌 포함), 외줄 `WaitingStartCursor…`·`CameraAutomaticallyUsesOrbitThirdPersonOnRope…`,
    대포 `OriginalCourse_CarryCamera…`·`OrdinaryJump_CanStartAndReverseMovementWhileAirborne…`, 공연 연결 `Standalone_CannonKeepsGroundSeatAndFlightCameraPolicy`·`Managed_CannonLastSurvivorDeath…`, 탈출로 `DevelopmentSessionTests` 모두 PASS.
  - MCP 주의: PlayMode 테스트 요청 두 개를 동시에 보내면 400과 함께 실행이 중단된다. 하나씩 async로 실행할 것.
- 2026-11-01 추가 지시: Dev 탈출로 바로 시작 + 공연 테스트 치트.
  - 구현: `RunStartPhase.Escape`(쉬는 시간→`StartEscape`), F8 창 "탈출로" 버튼, `EscapeDevelopmentSession` Dev 진입 시 출발점 배치·전원 쓰러짐 1초 후 재시작.
    `DevCheats`/`DevCheatPanel`(F9): 순간이동(`PlayerMover.Relocate`, 재시작 위치 유지), 추락 무시(기존 `FallConsequencesSuppressed` 경로 재사용), 균형 중앙, 모두 되살리기(호스트), 모두 재시작.
    지점 공급자: `TightropeDevTeleports`(출발·구간 시작·도착, 끊긴 줄 회피), `HumanCannonDevTeleports`(대포별 탑승·최종 목적지, 탈출로 대포 포함).
  - 검사: 컴파일 PASS. EditMode DevStartPointTests 20/20, TightropeSurfaceControlTests 29/29.
    PlayMode 신규 3개 PASS(탈출로 Dev 진입·복구·순간이동 / 외줄 지점·줄 위 이동 / 대포 지점·목적지 이동).
    `DevStartLauncherPlayTests` 클래스 전체 70개 중 57 PASS·13 FAIL. Dev 시작 관련은 모두 PASS.
    FAIL 13개는 대기실 M2 테스트의 기존 dev 문제(예: `ExplorationReach.From` 인자 추가 2e906f11 뒤 테스트 미갱신, `PropItem` 생성자, 단계 안내 문구 변경). 이 브랜치는 해당 코드·테스트 미변경.
  - 주의: PlayMode 실행으로 `Assets/Scenes/Levels/EscapeRouteMaterials/escape-labels.asset`(TMP 동적 아틀라스)이 바뀜. 작업 결과물 아님 → 커밋 제외/복원 필요(사용자 결정 대기).
