# Integration-tightrope-prototype
- **Feature:** 멀티플레이 기반 + 외줄타기 묘기 프로토타입
- **Status:** to-dev
- **Branch:** integration/tightrope-prototype
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 2026-10-06 후속 승인 — 개발·자동 검증 2작업일 예상(확정 약속 아님), 실제 4인 공동 테스트 별도, dev 승격 제외
- **Updated:** 2026-10-08

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.
## 목표

- 2026-10-08 PM 결정: 여기까지로 통합 종료, 모든 Task done 처리 후 dev로 올림. 미구현·미검증분(008·009·010 네트워크 확장, 017·018·019 에셋, 021 배치 편집기, 실제 4인 검증)은 공간별 통합(`integration/main-stage` 등)에서 새 Task로 다시 계획한다. 이후 공간 분리는 `Harness/Project/Decisions/space-workspaces.md`.
- 4명이 온라인 방에 모여 외줄 묘기 플로우(대기 → 묘기 시작 → 톱날·불을 피해 100m 코스 → 클리어/실패 결과 → 재도전·개인 행동 JSON (보상 제외))를 함께 테스트할 수 있다.
- 008 보류·017/018/019 에셋 대기. 013 완료, 후속 순서는 아래 2026-10-06 승인 기준.
- 쉬운 설명·작업 공간 전체: [plan.md](plan.md)
- 2026-10-06: 이전 4시간 작업은 종료. 별도 후속 계획 승인으로 015 결과 → 023 개인 JSON → 016 데모 UI → 제출된 021 관문 → 022 지정 씬 연결·macOS 빌드를 순차 진행한다. 예상 개발·자동 검증 2작업일, 사람 4인 검증은 별도. 보상·외부 업로드·새 패키지·dev 승격 제외.
- Fire 사용자 보고: 테스트에서 잘 동작함. 인원·플랫폼 미상이며 실제 4인 전체 검증 PASS로 확대하지 않는다.
- 최신 절차 승인: 현재 integration에서 PM 일괄 로컬 구현, Task별 브랜치/인계/PR·중간 검증 생략, 마지막 통합 검증. 텍스트 Read/Edit/Write·현재 에디터 컴파일은 이번 작업만 예외. 구현 전체 commit/push/PR·dev 승격은 하지 않는다. 기존 폰트/재질·proto 원본은 보존하며 이미 승인된 별도 에셋 보존 커밋과 분리한다.
## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20260930-003 | feat | @MoHoDu | feat/network-session | #6 | done (2026-10-08 통합 종료) |
| Task-20260930-004 | feat | @MoHoDu | feat/player-control | #7 | done (2026-10-08 통합 종료) |
| Task-20260930-005 | feat | @MoHoDu | feat/tightrope-core | #18 | done (2026-10-08 통합 종료) |
| Task-20260930-006 | feat | @MoHoDu | feat/tightrope-piggyback | #23 | done (2026-10-08 통합 종료) |
| Task-20260930-007 | feat | @MoHoDu | feat/network-player-sync | #11 | done (2026-10-08 통합 종료) |
| Task-20260930-008 | feat | @MoHoDu | feat/network-prop-sync (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20260930-009 | feat | @MoHoDu | feat/tightrope-network (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20260930-010 | feat | @MoHoDu | feat/piggyback-network (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261001-011 | feat | @MoHoDu | feat/tightrope-rider | #21 | done (2026-10-08 통합 종료) |
| Task-20261003-012 | feat | @MoHoDu | feat/tightrope-saws | #20 | done (2026-10-08 통합 종료) |
| Task-20261003-013 | refactor | @MoHoDu | refactor/game-settings | #19 | done (2026-10-08 통합 종료) |
| Task-20261003-014 | feat | @MoHoDu | feat/tightrope-fire | #24 | done (2026-10-08 통합 종료) |
| Task-20261003-015 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261003-016 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261003-017 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261003-018 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261003-019 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261003-020 | feat | @MoHoDu | feat/body-damage | #22 | done (2026-10-08 통합 종료) |
| Task-20261005-021 | feat | @MoHoDu | feat/obstacle-layout-editor | - | done (2026-10-08 통합 종료) |
| Task-20261005-022 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |
| Task-20261006-023 | feat | @MoHoDu | (인계 때 생성) | - | done (2026-10-08 통합 종료) |

2026-10-05 PM 요청: 003·004·007·011·012·013·020은 `Tasks/Done/`으로 완료 보관. dev 반영·통합 종료는 미실행. 012 부분 통과 항목은 021에서 재확인하며, 005는 공동 테스트 결과 정리 대기로 Active 유지. 테스트 씬 처리 결정은 변경하지 않음.
## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Resources/Prefabs/Controllers/Network/` | Task-20260930-003 → 007 | NetworkManager 프리팹 (007이 플레이어 프리팹 지정·목록 등록) |
| `Assets/Resources/Input/` | Task-20260930-004 → 007 → 이후 PM 배정 | 조작 입력 에셋 |
| `Assets/Resources/Prefabs/Characters/Players/` | Task-20260930-004 → 007 → 이후 PM 배정 | 플레이어 프리팹 (007이 네트워크 적용, 이후 Task가 차례로 기능 추가) |
| `Assets/Resources/Prefabs/UIs/Player/` | Task-20260930-004 → 007 → 이후 PM 배정 | 플레이어 UI(균형 게이지) 프리팹 |
| `Assets/Resources/Fonts/` | Task-20260930-004 → 이후 PM 배정 | 임시 UI 폰트(TMP) |
| `Assets/TextMesh Pro/` | 없음 (공용, 수정 금지) | TMP 기본 리소스 (2026-10-01 004에서 가져옴) |
| `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | Task-20260930-005 | 외줄 코스 스테이지 (다른 Task는 배치만) |
| `Assets/Resources/Prefabs/Objects/Tools/` | Task-20260930-008 (보류) | 도구 프리팹 |
| `Assets/Resources/GameSettings/`, `Assets/Scripts/Settings/` | Task-20261003-013 → 이후 칸 추가 Task PM 배정 | 기획 조정값 세팅 파일 (`Harness/Project/Decisions/game-settings.md`) |
| `Assets/Scripts/Settings/TightropeSettings.cs`, `Assets/Resources/GameSettings/Tricks/TightropeSettings.asset` | Task-20261003-014 (2026-10-05 PM) | Fire 칸만 추가, 021 설정 편집 점유 해제 확인 |
| `Assets/Scripts/Tightrope/Rope/TightropeRun.cs` | Task-20261003-015 (2026-10-06 PM, 014 병합 후 인계) → 015 병합 후 022 | 종료 전 불변 결과 경계·이유만, 도착 → Fire → 실패 순서 유지 |
| `Assets/Scripts/Tightrope/Rope/TightropeCourse.cs` | 없음 (014 병합 완료, 읽기 전용) | 구간 끝 제외 선택 유지, 추가 수정은 PM 배정 |
| `Assets/Scripts/Camera/`, `Assets/Resources/Prefabs/Controllers/Camera/` | Task-20260930-006 (2026-10-04 PM, 카메라 가림·구도) | `camera-review-20261004.md` |
| `Assets/Plugins/` (DOTween Pro), `Assets/Resources/DOTweenSettings.asset` | PM (2026-10-05 통합 브랜치에 설치, 원본·예제 수정 금지) | 트윈 라이브러리 |
| `Assets/Scripts/Network/PlayerSync/` | Task-20260930-007 (병합 완료), 아래 022 배정 파일 제외 읽기 전용 | 테스트 부품 `TestRoundRestart.cs`는 005, `TestLaneLanding.cs`는 011이 대체·삭제 |
| `Assets/Resources/Prefabs/UIs/Tightrope/`, `Assets/Scripts/Tightrope/UI/` | Task-20261003-016 → 016 병합 후 022 (코드만) | 데모 UI 전용, 기존 폰트/게이지 수정 금지 |
| `Assets/Scripts/Tightrope/Results/` | Task-20261003-015 → 015 병합 후 022 | 고정 결과·참가자 ID 연결 |
| `Assets/Scripts/Telemetry/`, `Assets/Scripts/Tightrope/Telemetry/` | Task-20261006-023 → 023 병합 후 022 | 개인 JSON·최종 행동 hook |
| `Assets/Scenes/Prototypes/`, `Assets/Scripts/Tightrope/Prototype/` | Task-20261005-022 | 지정 씬/GUID 보존, 최종 연결 |
| `Assets/Scripts/Network/Session/NetworkSessionManager.cs`, `Assets/Scripts/Network/PlayerSync/NetworkPlayer.cs`, `Assets/Scripts/Network/PlayerSync/LocalPlayerViews.cs` | Task-20261005-022 (선행 병합 후) | 호스트 retry·공유 ID·참가자/개발 화면 연결 |
| `Assets/Scripts/Player/PlayerInputReader.cs`, `Assets/Scripts/Player/PlayerCommandDebugHud.cs`, `Assets/Scripts/Player/PlayerMover.cs`, `Assets/Scripts/Player/TightropeControlScheme.cs` | Task-20261005-022 (선행 병합 후) | 전체 입력 gate·예약 정리·행동 연결 |
| `Assets/Scripts/Tightrope/Rope/CourseControlSwitcher.cs`, `Assets/Scripts/Tightrope/Rope/TightropeRunDebug.cs` | Task-20261005-022 (선행 병합 후) | 개발 명령 gate·retry override 제거 |
| `Assets/Scripts/Tightrope/Piggyback/PiggybackSystem.cs`, `Assets/Scripts/Tightrope/Piggyback/PiggybackFollow.cs` | Task-20261005-022 (006 병합 후) | 성공 연결 해제·Follow 중단 |
| `Assets/Scripts/Tightrope/Saws/TightropeSaws.cs` | Task-20261005-022 (021 제출 관문/소유 충돌 확인 후) | 발판 최소 hook만, 021 충돌 시 중단 |

`Packages/`, `ProjectSettings/`는 PM이 인계 전 이 브랜치에서 직접 설치·설정한다(모든 Task 수정 금지).
## Decisions
- Open: 005~010 담당자 GitHub 계정 (미배정, 작업 상황 보고 PM이 결정). 011은 @MoHoDu 확정
- 초기 스택·분할·병합 순서 결정: [History/001-early-decisions.md](History/001-early-decisions.md)
