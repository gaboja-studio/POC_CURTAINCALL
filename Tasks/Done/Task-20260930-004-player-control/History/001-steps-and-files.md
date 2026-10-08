# 004 단계·파일 기록 (2026-10-02, plan.md에서 원문 그대로 이동)

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
13. [x] 몸 기울기 — `BalanceTiltView`가 균형 값(±100)에 따라 모델 슬롯을 좌우로 기울임(최대 25°, 부드럽게 따라감, 균형 꺼지면 똑바로, 추락 시 유지). 충돌·이동 영향 없음 (2026-10-02 @MoHoDu 추가)
14. [x] 추락 래그돌 — `PlayerRagdoll`: 추락 신호(`Fell`) 때 모델을 슬롯에서 떼어 힘 빠진 물리 래그돌로 무너뜨림(기운 쪽으로 살짝 밀기), 균형 되돌림(`BalanceReset`) 때 같은 모델 다시 끼움. 테스트 모델 BlockDoll은 CharacterJoint 래그돌, 캡슐은 한 덩어리. 실제 낙하 후 대기·재시작은 005, 온라인 동기화는 007 (2026-10-02 @MoHoDu 추가)

## Files

- 수정 예정:
  - `Assets/Resources/Input/PlayerControls.inputactions` — 맵 `Common`: Move(W/S), Posture(A/D), Action(Space), DirectionLeft(Q), DirectionRight(E), Interact(F)
  - `Assets/Scripts/Player/PlayerInputFrame.cs` — 프레임별 공통 역할 입력 값
  - `Assets/Scripts/Player/PlayerController.cs` — 입력과 동작 부품 연결, 조작 규칙 교체(`SetScheme`)
  - `Assets/Scripts/Player/PlayerControlScheme.cs` — 조작 규칙(전략) 기반 + 동작 부품 묶음
  - `Assets/Scripts/Player/TightropeControlScheme.cs` — 외줄 조작 규칙
  - `Assets/Scripts/Player/PlayerInteraction.cs` — 동작 부품: 상호작용·해제 요청 신호
  - `Assets/Scripts/Player/PlayerModelSlot.cs` — 겉모습 모델 슬롯(교체·키 맞춤)
  - `Assets/Scripts/Player/BalanceTiltView.cs` — 균형에 따른 몸 기울기 연출
  - `Assets/Scripts/Player/PlayerRagdoll.cs` — 추락 시 래그돌 연출
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

