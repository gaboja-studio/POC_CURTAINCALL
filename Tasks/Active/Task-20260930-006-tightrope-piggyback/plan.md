# Plan

## Context

플레이어 간 협동 묘기. 로컬에서 더미 캐릭터로 결합·해제와 예외 처리를 먼저 완성한다.
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
- 외줄 균형에 영향을 줄 수 있게 목마 상태를 공개 진입점으로 둔다. 004 `SetStackSize`·`SetUpperBalanceSum`에 연결하고, 아래층 추락 시 위층 연쇄 추락.
동기화(2026-10-02, `Harness/Project/Decisions/feature-with-network.md`):
- F 신호는 004 `PlayerInteraction`(`InteractRequested`/`ReleaseRequested`)을 받는다.
- 연결·해제·점프 분리 요청은 **호스트가 판정**하고 007 판정 통로로 공유한다. 목마 중 위치는 아래 사람을 따라가도록 맞춘다.

2026-10-03 PM(`tightrope-course-rules.md` #5·#7): 목마 유지. 옆줄 착지가 동료와 겹치면 합체(안 되면 011의 뒤쪽 착지). 수직 톱날은 목마를 쓰게 하는 장애물 — 2층 이상이 앞으로 뛰어(맨 위 W+SB) 톱날을 넘고 뒤의 버튼을 눌러 내린다(버튼은 012). 캐릭터 충돌체 높이 1.5m 기준.

## Files

- 수정 예정:
- 읽기 전용 참고:

## Risks

- 목마 규칙(몇 명까지 쌓는지, 균형 영향)은 Confluence 기획 문서 기준. 반입 전 임의로 확정하지 않는다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정)
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
**작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** (초안 — 인계 전 PM·담당자 합의)

1. 더미 캐릭터 위로 목마가 연결되고 해제된다.
2. 목마에 속한 사람이 F 짧게를 누르거나 너무 멀면 올라가지 못하고, 내 위에 사람이 있으면 F 길게로 내려오지 못한다.
3. 목마 상태에서 아래 캐릭터가 움직이면 위 캐릭터도 따라간다.
