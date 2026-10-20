# Task-20261019-061
- **Title:** UI 씬 통합 · C1/C2/C3/P1 공연 HUD
- **Type:** feat
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** Unity Assets 구조·코드 작업 구역
- **Current Skill:** integrate-editor
- **Updated:** 2026-10-20
## Workspace
- **Integration:** integration/ui-resources
- **Branch:** feat/ui-scene-integration
- 현재 worktree: D:/github/POC_CURTAINCALL-ui-scene-integration
## Goal / Scope
- Assets 정본 C1/PerformanceHud 프리팹을 씬 인스턴스로 적용하고 기존 게임 공개 데이터에 연결한다.
- 사용자 지정 적용 대상: Start/Lobby/WaitingRoomBase/Tightrope/HumanCannon 5개 씬. 첫 단계는 두 공연 씬 HUD이며 백스테이지·탈출로 신규 씬은 만들지 않는다.
- 제작 원본/폰트/PNG/importer/9-slice는 보존. 인계 Packages ZIP 사용 금지.
## Decisions
- 2026-10-20 사용자: 공통 UI는 별도 UI 씬, 공간 종속 UI는 공간 씬, UI 루트로 Hierarchy 정리. C1/C2/C3 공통 씬과 외줄 P1로 분리했다.
- 2026-10-20 사용자: C2는 불의 안전 지대 도달까지 표시만 한다. 성공/실패는 기존 로직을 유지하며 UI는 제어하지 않는다.
- 2026-10-20 사용자: 해당 Task 내용을 이번 작업으로 변경, 현재 환경 유지. 이 checkout의 Task-061을 전환하며 제작 worktree는 수정하지 않는다.
- 기존 제작 기록은 History/Before-scene-integration/ 및 기존 검증 문서에 보존한다. 제작 검증 결과를 통합 검증으로 간주하지 않는다.
- 실제 게임 상한을 그대로 표시한다. C3 1인 미지원은 가짜 플레이어 없이 미수신 표시로 유지한다.
## Open Decisions
- C3 정본의 1인 지원은 제작 측 변경 필요. 이번에는 정본 API 계약 유지.
- 2026-10-20 사용자: 현재까지 commit/push 승인. PR/병합은 이번 요청에 포함하지 않는다.
