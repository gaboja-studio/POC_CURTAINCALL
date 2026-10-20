# Task-20261019-061
- **Title:** UI 씬 통합 · 공연 UI 및 대기실 기본 HUD 연결
- **Type:** feat
- **Status:** human-verify
- **Assignee:** @MoHoDu
- **Domain:** Unity Assets 구조·코드 작업 구역
- **Current Skill:** integrate-editor
- **Updated:** 2026-10-21
## Workspace
- **Integration:** integration/ui-resources
- **Branch:** feat/ui-scene-integration
- 현재 worktree: D:/github/POC_CURTAINCALL-ui-scene-integration
## Goal / Scope
- 2026-10-21 사용자: 상점/스케줄 등 입력을 점유하는 창을 열면 커서 자동 표시, 종료하면 마우스 시선 조작과 숨김/잠금 복귀. 기존 공통 입력 차단 요청에 연결.
- 2026-10-21 사용자: 수화기 위치/표현을 먼저 복구하고 Assets 완성 WorldLabels로 W4·W5 실제 데이터 연결 및 대체 임시 이름표 정리.
- Assets 정본 C1/PerformanceHud 프리팹을 씬 인스턴스로 적용하고 기존 게임 공개 데이터에 연결한다.
- 사용자 지정 적용 대상: Start/Lobby/WaitingRoomBase/Tightrope/HumanCannon 5개 씬. 첫 단계는 두 공연 씬 HUD이며 백스테이지·탈출로 신규 씬은 만들지 않는다.
- 제작 원본/폰트/PNG/importer/9-slice는 보존. 인계 Packages ZIP 사용 금지.
## Decisions
- 사용자 추가 지시: Dev 모드에서 특정 일차/콘텐츠부터 바로 검증. 기존 Dev 배지/F8 창, 새 테스트 세대, 실제 LAN 호스트 진입으로 구현했다.
- 2026-10-20 사용자: 다음 적용은 HumanCannon부터 진행. 공통 C7/C15 정본과 인간대포 공간 상태 연결을 우선한다.
- 2026-10-20 사용자: 공통 UI는 별도 UI 씬, 공간 종속 UI는 공간 씬, UI 루트로 Hierarchy 정리. C1/C2/C3 공통 씬과 외줄 P1로 분리했다.
- 2026-10-20 사용자: C2는 불의 안전 지대 도달까지 표시만 한다. 성공/실패는 기존 로직을 유지하며 UI는 제어하지 않는다.
- 2026-10-20 사용자: 해당 Task 내용을 이번 작업으로 변경, 현재 환경 유지. 이 checkout의 Task-061을 전환하며 제작 worktree는 수정하지 않는다.
- 기존 제작 기록은 History/Before-scene-integration/ 및 기존 검증 문서에 보존한다. 제작 검증 결과를 통합 검증으로 간주하지 않는다.
- 2026-10-20 사용자: HTML 4-1 임시 호응 규칙을 공통 로직으로 새로 구현. 두 공연 상한100/절단30, 실제 이벤트로 증감. 기존 상한 유지 결정을 이 범위에서 대체한다.
- C3 1인은 정본 그래픽을 재사용하는 씬 어댑터로 표시. 정본 프리팹/API는 보존한다.
## Open Decisions
- 근접 회피0.3m·불 직전 도착3m 등 문서 미기재 수치는 설정으로 분리했으며 실플레이 튜닝이 필요하다.
- 2026-10-20 사용자: 현재까지 commit/push 승인. PR/병합은 이번 요청에 포함하지 않는다.

## 공연 종료 추가 지시
- 2026-10-20 사용자: 두 공연 로컬 사망 알림, 결과 표시 대기, 별도 검은 전환 씬을 요청했다. 기존 정산/판정을 유지하고 표시와 공간 전환 사이에 대기를 추가한다.

## 최신 공연 UI
- 2026-10-20 사용자: origin/dev를 먼저 받은 뒤 새 Assets 프리팹으로 공연 UI/로직 연결을 고도화한다. dev864ed37 수신 후 C11/H1/C8/C12 연결 완료, 상세는 performance-upgrade.md 및 upgrade-verification.md.
