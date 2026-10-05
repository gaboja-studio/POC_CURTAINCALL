# Plan

## Context

플레이어 간 협동 묘기. 실제 네트워크 플레이어로 결합·해제와 호스트 판정·동기화를 함께 구현했다. 2026-10-05 사용자 회귀 및 톱날·발판·신체 손상 연계 Play 통과 보고 후 종료 정리 승인. Base 임시값 원복·임시 로그 제거 후 최종 검사하며 PR·통합 완료와는 구분한다.
원문: `Docs/References/tightrope-prototype-brief.md`
요약: `Docs/References/tightrope-keymap-summary.md`, `Docs/References/tightrope-plan-summary.md` · 규칙 결정: `Harness/Project/Decisions/tightrope-rules.md`

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 테스트 어셈블리는 만들지 않는다. AI는 컴파일만 검사한다.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

세부 순서:
- 실제 플레이어 프리팹(007 적용)으로 시험한다. 더미 캐릭터를 만들지 않는다.
- 목마 연결: F 짧게를 누른 사람이 가까운 동료(목마면 맨 위) 위로 올라간다. 올라가는 사람은 단독이어야 한다. 해제: F 길게는 내 위에 사람이 없을 때 본인만.
- SB: 1층과 맨 위만 점프(중간층 불가). 1층 = 목마 전체 점프, 맨 위 = 분리 점프, 맨 위 W + SB = 앞으로 멀리 뛰며 분리 (`Harness/Project/Decisions/tightrope-rules.md` 7번).
- 모든 층이 A/D로 자기 균형, 위층 기울기가 아래층에 전달.
- 연결 중에는 위 캐릭터가 아래 캐릭터를 따라 움직인다.
- 거부할 상황(너무 멂, 이미 연결됨 등)을 막는다.
- 목마 중 추락·피격(2026-10-04 PM): 게이지는 각자. 받치던 사람이 없어지면 그 위 전원 추락, 바로 아래층은 반동 1회(임시 ±10, 세팅 칸), 사망 래그돌도 같게, 호스트가 한 번에 판정 (`Harness/Project/Decisions/tightrope-balance.md` 2026-10-04).
- 외줄 균형에 영향을 줄 수 있게 목마 상태를 공개 진입점으로 둔다. 004 `SetStackSize`·`SetUpperBalanceSum`에 연결하고, 아래층 추락 시 위층 연쇄 추락.
동기화(2026-10-02, `Harness/Project/Decisions/feature-with-network.md`):
- F 신호는 004 `PlayerInteraction`(`InteractRequested`/`ReleaseRequested`)을 받는다.
- 연결·해제·점프 분리 요청은 **호스트가 판정**하고 007 판정 통로로 공유한다. 목마 중 위치는 아래 사람을 따라가도록 맞춘다.

2026-10-03 PM(`tightrope-course-rules.md` #5·#7): 목마 유지. 옆줄 착지가 동료와 겹치면 합체(안 되면 011의 뒤쪽 착지). 수직 톱날은 목마를 쓰게 하는 장애물 — 2층 이상이 앞으로 뛰어(맨 위 W+SB) 톱날을 넘고 뒤의 버튼을 눌러 내린다(버튼은 012). 캐릭터 충돌체 높이 1.5m 기준.

## Files

- 수정: `setup.md`의 목마·카메라 구역 및 배정된 공용 파일. 종료 정리는 `PiggybackSystem.cs`, 승인된 `BaseGameSettings.asset`, 이 Task 문서.
- 읽기 전용 참고: `Docs/Domains/tightrope-course.md`, 관련 Project 결정·Unity 에셋 소유권 정책.

## Risks

- 기획 미정 수치를 새로 확정하지 않는다. Base 원복은 기존 기본값 15/5/0.5이며, 그 값에서 새 Play는 아직 하지 않았다.

## Auto Verification

- 구역 검사: 종료 정리 전 OK; 변경 후 최종 결과는 `handoff.md`.
- 1 문서/규칙: 종료 정리 전 Fast OK; 변경 후 최종 결과는 `handoff.md`.
- 2 컴파일: 종료 정리 전 PASS; 임시 로그 제거 후 최종 결과는 `handoff.md`.
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정)
- 4 실행 로그: 사용자 Play 로그 AI 미검증. 기존 콘솔 인증 오류 해소 미검증.

## 공동 테스트 항목

PM·작업자 사전 합의 이력은 별도 확인한다. 다음은 기존 확인 기준이며, 제출·병합 후 공동 테스트 완료를 뜻하지 않는다.
**작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** 2026-10-05 회귀 1~4 및 연계 3개 사용자 통과 보고는 `handoff.md`·`log.md`에 기록했다(Base 임시값 상태).

1. 여러 명 접속한 실제 플레이어 사이에 목마가 연결되고 해제되며, 모든 화면에서 같은 연결 상태를 본다.
2. 목마에 속한 사람이 F 짧게를 누르거나 너무 멀면 올라가지 못하고, 내 위에 사람이 있으면 F 길게로 내려오지 못한다.
3. 목마 상태에서 아래 캐릭터가 움직이면 위 캐릭터도 따라간다.
