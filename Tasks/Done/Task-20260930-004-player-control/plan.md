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

## 단계

- 14단계 모두 완료(2026-10-02, 작업자 플레이 확인). 단계별 내용: [History/001-steps-and-files.md](History/001-steps-and-files.md)

결정(2026-10-01 @MoHoDu): 균형 시스템(값·흔들림·기울기 가속·게이지·추락 신호·목마/충격 입력 함수)은 004에서 만든다. 규칙은 `Harness/Project/Decisions/tightrope-balance.md`, 수치는 `Docs/References/tightrope-balance-summary.md`. 혼자 하는 제자리 점프는 004(2026-10-01 변경). 옆줄 이동의 실제 동작과 낙하·대기·재시작은 005, 목마 중 점프는 006이 담당한다.
결정(2026-10-01 @MoHoDu): 게이지 같은 UI는 디자인을 교체할 수 있게 Canvas 프리팹으로 만든다. 위치 `Assets/Resources/Prefabs/UIs/Player/`(004에 추가 배정). 스크립트는 값만 반영한다.
결정(2026-10-02 @MoHoDu): 점프 중(제자리·옆줄) 수평 움직임은 뛴 순간 고정, 공중에서 W/S·Q/E로 바꾸지 않는다. 옆줄 점프의 실제 움직임은 004, 착지 판정은 005.
결정(2026-10-02 @MoHoDu): 조작은 3층 구조(입력 → 조작 규칙(전략) → 동작 부품). `Harness/Project/Decisions/player-control-architecture.md`. 9~11단계 판정은 입력 층(짧게/길게·홀드)과 외줄 규칙(`TightropeControlScheme`, Q/E+Space 조합 등)에 둔다.
결정(2026-10-01 @MoHoDu): 이동 기준은 카메라가 아니라 월드 코스 방향(목적지 쪽). 카메라는 3인칭·다른 플레이어도 잡을 수 있어 이동과 분리한다.

## Files

- 파일 목록·공개 함수: `Docs/Domains/player-control.md` (변경 당시 목록: [History/001-steps-and-files.md](History/001-steps-and-files.md))

## Risks

- 기획 문서의 (제안) 4개(빨강 타이머 초기화, 위층 전달 방식, 점프 중 균형, 동료 근처 범위)는 제안값으로 구현하고 플레이테스트 후 확정한다.
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
