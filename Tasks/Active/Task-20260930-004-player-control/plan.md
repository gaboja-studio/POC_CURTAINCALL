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

## 단계 (씬에서 매 단계 플레이 가능하게, 2026-10-01 @MoHoDu 갱신)

1. [x] W/S 앞뒤 이동 — 입력 에셋, 명령 구조체, 코스 방향 이동, 플레이어 프리팹, 테스트 씬
2. [x] 디버깅 표시 — 명령 HUD(P 토글, 기능 코드와 분리)
3. [x] A/D 자세 값 — Posture(-1~1) → HUD
4. 균형 게이지 — 균형 값을 A/D로 밀고, 하단 게이지(초록·노랑·빨강)와 바늘 표시. 흔들림 없음
5. 흔들림·관성 — 기본 흔들림 + 걷기 흔들림, 노랑·빨강에서 바깥으로 미는 관성. 목마 인원 흔들림은 인스펙터 테스트 값 + 외부 설정 함수
6. 무너짐 — 빨강 3초 → 무너짐 신호(이벤트)·HUD 표시, 디버그 재시작 키. 실제 낙하는 005
7. 제자리 점프 — Space 점프 명령 + 점프 흔들림 (실제 점프는 005)
8. Q/E 홀드 방향 — 누르는 동안만 방향(-1/0/+1)
9. Q/E 홀드 + Space 옆줄 건너기 — 옆줄 이동 명령 + 옆줄 흔들림 (실제 이동·착지는 005)
10. F 상호작용/해제 — 짧게/길게 구분(기준 시간 인스펙터), 함수와 디버그 표시까지
11. 모델 변경 — 프리팹 모델 슬롯, `Models/`에 대체 모델 1개

결정(2026-10-01 @MoHoDu): 균형 시스템(값·흔들림·관성·게이지·무너짐 신호)은 004에서 만든다. 규칙은 `Harness/Project/Decisions/tightrope-balance.md`. 점프·옆줄의 실제 동작과 낙하·대기·재시작은 005가 담당한다.
결정(2026-10-01 @MoHoDu): 이동 기준은 카메라가 아니라 월드 코스 방향(목적지 쪽). 카메라는 3인칭·다른 플레이어도 잡을 수 있어 이동과 분리한다.

## Files

- 수정 예정:
  - `Assets/Resources/Input/PlayerControls.inputactions` — 맵 `Common`: Move(W/S), Posture(A/D), Action(Space), DirectionLeft(Q), DirectionRight(E), Interact(F)
  - `Assets/Scripts/Player/PlayerCommand.cs` — 프레임별 명령 구조체
  - `Assets/Scripts/Player/PlayerInputReader.cs` — 입력 → 명령 (공개 진입점 `Current`)
  - `Assets/Scripts/Player/PlayerMover.cs` — 코스 방향 기준 이동 (CharacterController)
  - `Assets/Scripts/Player/PlayerCommandDebugHud.cs` — 테스트용 명령 HUD (기능 코드가 참조하지 않음, P 토글)
  - `Assets/Scripts/Player/PlayerBalance.cs` (예정) — 균형 값·흔들림·관성·무너짐 신호 (공개 진입점)
  - `Assets/Scripts/Player/BalanceGaugeView.cs` (예정) — 하단 균형 게이지 표시
  - `Assets/Resources/Prefabs/Characters/Players/Player.prefab` — 플레이어 프리팹
  - `Assets/Scenes/Tests/PlayerControl/PlayerControl.unity` — 테스트 씬 배치
- 읽기 전용 참고: `Docs/References/tightrope-keymap-summary.md`, `Harness/Project/Decisions/tightrope-balance.md`, `Docs/References/ref_001.png`

## Risks

- 균형 열린 점(빨강 타이머 초기화 여부, 점프·옆줄 흔들림 형태)은 가정으로 구현하고 테스트 후 확정한다.
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
