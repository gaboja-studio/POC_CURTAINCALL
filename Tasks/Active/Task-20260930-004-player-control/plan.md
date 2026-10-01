# Plan

## Context

모델·애니메이션·연출 없이 기능만 만든다. 입력을 '명령'으로 분리해 나중에 온라인·외줄 작업이 받아 쓸 수 있게 한다.
원문: `Docs/References/tightrope-prototype-brief.md`
요약: `Docs/References/tightrope-keymap-summary.md`, `Docs/References/tightrope-plan-summary.md` · 규칙 결정: `Harness/Project/Decisions/tightrope-rules.md`

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 테스트 어셈블리는 만들지 않는다. AI는 컴파일만 검사한다.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

세부 순서:
- 새 입력 에셋을 `Assets/Resources/` 아래(setup.md 경로)에 만든다. 기존 `InputSystem_Actions`는 건드리지 않는다.
- F 짧게/길게, Q/E 홀드 + Space 조합을 구분한다.
- 입력 → 명령(이동·자세·액션·방향·상호작용·해제) 구조로 분리한다.
- 플레이어 프리팹에 모델 슬롯을 두고 디폴트 캡슐을 넣는다.

## Files

- 수정 예정:
- 읽기 전용 참고:

## Risks

- 세부 입력 규칙(홀드 판정 시간 등)은 Confluence 키맵핑 문서 반입 후 확정한다. 문서 전에는 인스펙터 값으로 열어 둔다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정)
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
**작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** (초안 — 인계 전 PM·담당자 합의)

1. 모든 키가 키맵핑 요약의 공통 입력 역할대로 반응한다 (Q/E는 홀드 중에만 방향 지정, 홀드 + Space로 옆줄 이동 명령).
2. F 짧게 누름과 길게 누름이 구분된다.
3. 디폴트 모델을 다른 모델로 바꿔도 조작이 그대로 된다.
