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

## 단계 (씬에서 매 단계 플레이 가능하게, 2026-10-01 @MoHoDu 갱신 — 기획 문서 rope_balance 반영)

1. [x] W/S 앞뒤 이동 — 입력 에셋, 명령 구조체, 코스 방향 이동, 플레이어 프리팹, 테스트 씬
2. [x] 디버깅 표시 — 명령 HUD(P 토글, 기능 코드와 분리)
3. [x] A/D 자세 값 — Posture(-1~1) → HUD
4. [x] 균형 게이지 — Canvas UI 프리팹 + TMP, 기획 반영: 균형값 ±100, 초록 ±40·빨강, A/D 60/초, 캐릭터 키 1.73m
5. [x] 자연 흔들림·기울기 가속 + 균형 켜기/끄기(줄 위에서만, 005가 `SetBalanceActive`로 제어, 테스트는 B키) — 이동 중 15/초·정지 중 5/초, 방향 1~2초마다 랜덤, 기울기 가속(균형값 × 0.5/초)
6. [x] 추락 신호 — 빨강 2초 → 추락 신호(`Fell`), 게이지에 빨강 체류 시간, 초록 복귀 시 초기화. PlayerMover에 조작 잠금(`SetControlEnabled`)·출발 위치 복귀(`ResetToStart`). 테스트 씬은 디버그 HUD가 추락→잠금, R→재시작 연결 (실제 낙하·대기·재시작 연결은 005)
7. [x] 목마 입력 자리 — `SetStackSize`(배율 1.0/1.3/1.6/2.0, 자연 흔들림·충격에 곱함)·`SetUpperBalanceSum`(위층 균형 합 × 0.2/초) + 인스펙터 테스트 값 (실제 연결·연쇄 추락은 006·연결 작업)
8. [x] 제자리 점프 — `PlayerCommand.Jump`(Space), PlayerMover 실제 점프(높이 1.0m, 중력 9.81, `AirborneChanged`), PlayerBalance 자동 연결(공중 균형·빨강 타이머 정지, 착지 ±10 × 목마 배율, `ApplyShock`). 목마 중 점프 규칙은 006
9. [x] Q/E 홀드 방향 — 입력 층 `AuxDirection`(누르는 동안만 -1/0/+1), 외줄 규칙이 옆줄 방향으로 지정(`HeldDirection`), 단독으로는 동작 없음
10. [x] Q/E 홀드 + Space 옆줄 건너기 — 외줄 규칙이 `RequestLaneJump`, PlayerMover가 줄 간격(1.5m)만큼 옆으로 실제 점프(방향은 뛴 순간 고정, `LaneJumpFilter`로 005가 허용 판단), 착지 시 PlayerBalance 단독 충격 ±35 + 3초 흔들림 ×2.5(`SetLaneLandingReduction`으로 005가 동료 근처 50% 감소). 줄 유무·합체·뒤쪽 보정은 005
11. [x] F 상호작용/해제 — 입력 층이 짧게(뗀 프레임)/길게(기준 시간 도달 프레임, `Long Press Time` 0.5초) 판정, 외줄 규칙이 `PlayerInteraction.RequestInteract`/`RequestRelease` 호출 → `InteractRequested`/`ReleaseRequested` 신호와 디버그 표시까지 (실제 목마 연결·해제는 006)
12. [x] 모델 변경 — `PlayerModelSlot`(모델 프리팹 교체 `SetModel`, 키 1.73m 맞춤·발 정렬, 모델 콜라이더 끔), 테스트 모델 생성 메뉴(Tools/CurtainCall/Build Player Test Models → `Models/DefaultCapsule`, `Models/BlockDoll`), HUD M키로 교체

결정(2026-10-01 @MoHoDu): 균형 시스템(값·흔들림·기울기 가속·게이지·추락 신호·목마/충격 입력 함수)은 004에서 만든다. 규칙은 `Harness/Project/Decisions/tightrope-balance.md`, 수치는 `Docs/References/tightrope-balance-summary.md`. 혼자 하는 제자리 점프는 004(2026-10-01 변경). 옆줄 이동의 실제 동작과 낙하·대기·재시작은 005, 목마 중 점프는 006이 담당한다.
결정(2026-10-01 @MoHoDu): 게이지 같은 UI는 디자인을 교체할 수 있게 Canvas 프리팹으로 만든다. 위치 `Assets/Resources/Prefabs/UIs/Player/`(004에 추가 배정). 스크립트는 값만 반영한다.
결정(2026-10-02 @MoHoDu): 점프 중(제자리·옆줄) 수평 움직임은 뛴 순간 고정, 공중에서 W/S·Q/E로 바꾸지 않는다. 옆줄 점프의 실제 움직임은 004, 착지 판정은 005.
결정(2026-10-02 @MoHoDu): 조작은 3층 구조(입력 → 조작 규칙(전략) → 동작 부품). `Harness/Project/Decisions/player-control-architecture.md`. 9~11단계 판정은 입력 층(짧게/길게·홀드)과 외줄 규칙(`TightropeControlScheme`, Q/E+Space 조합 등)에 둔다.
결정(2026-10-01 @MoHoDu): 이동 기준은 카메라가 아니라 월드 코스 방향(목적지 쪽). 카메라는 3인칭·다른 플레이어도 잡을 수 있어 이동과 분리한다.

## Files

- 수정 예정:
  - `Assets/Resources/Input/PlayerControls.inputactions` — 맵 `Common`: Move(W/S), Posture(A/D), Action(Space), DirectionLeft(Q), DirectionRight(E), Interact(F)
  - `Assets/Scripts/Player/PlayerInputFrame.cs` — 프레임별 공통 역할 입력 값
  - `Assets/Scripts/Player/PlayerController.cs` — 입력과 동작 부품 연결, 조작 규칙 교체(`SetScheme`)
  - `Assets/Scripts/Player/PlayerControlScheme.cs` — 조작 규칙(전략) 기반 + 동작 부품 묶음
  - `Assets/Scripts/Player/TightropeControlScheme.cs` — 외줄 조작 규칙
  - `Assets/Scripts/Player/PlayerInteraction.cs` — 동작 부품: 상호작용·해제 요청 신호
  - `Assets/Scripts/Player/PlayerModelSlot.cs` — 겉모습 모델 슬롯(교체·키 맞춤)
  - `Assets/Scripts/Player/Editor/PlayerModelPrefabBuilder.cs` — 테스트 모델 프리팹 생성 메뉴
  - `Assets/Scripts/Player/PlayerInputReader.cs` — 키 → 공통 역할 입력 (`Current`)
  - `Assets/Scripts/Player/PlayerMover.cs` — 동작 부품: 코스 이동·제자리 점프 (CharacterController)
  - `Assets/Scripts/Player/PlayerCommandDebugHud.cs` — 테스트용 명령 HUD (기능 코드가 참조하지 않음, P 토글)
  - `Assets/Scripts/Player/PlayerBalance.cs` — 균형 값·흔들림·기울기 가속·추락 신호·충격/목마 입력 (공개 진입점)
  - `Assets/Scripts/Player/BalanceGaugeView.cs` — 균형 게이지 UI에 값 반영(바늘 위치·초록 폭만, 모양은 프리팹)
  - `Assets/Scripts/Player/Editor/BalanceGaugePrefabBuilder.cs` — 그레이박스 게이지 프리팹 생성 메뉴(Tools/CurtainCall)
  - `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab` — 균형 게이지 UI 프리팹(Canvas + TMP, 임시 폰트 Pretendard-Medium, 리소스 오면 디자인 교체)
  - `Assets/Resources/Prefabs/Characters/Players/Player.prefab` — 플레이어 프리팹
  - `Assets/Scenes/Tests/PlayerControl/PlayerControl.unity` — 테스트 씬 배치
- 읽기 전용 참고: `Docs/References/tightrope-keymap-summary.md`, `Harness/Project/Decisions/tightrope-balance.md`, `Docs/References/tightrope-balance-summary.md`

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
