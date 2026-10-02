# 플레이어 조작 (입력·이동·균형·겉모습)

## 목적

플레이어 캐릭터 한 명의 조작과 동작을 담당한다: 키 입력, 코스 이동·점프·옆줄 점프, 외줄 균형(흔들림·추락 신호), 상호작용 신호, 모델 교체·몸 기울기·추락 래그돌.
구조 결정: [조작 3층 구조](../../Harness/Project/Decisions/player-control-architecture.md) · 균형 규칙: [외줄 균형](../../Harness/Project/Decisions/tightrope-balance.md) · 온라인: [기능+동기화](../../Harness/Project/Decisions/feature-with-network.md)

## 구조 (입력 → 조작 규칙 → 동작 부품)

```
키 ─(입력 에셋)→ PlayerInputReader ─ PlayerInputFrame(공통 역할) ─→ PlayerController ─→ 조작 규칙(전략) ─→ 동작 부품
```

- 키 변경은 입력 에셋에서, 콘텐츠별 키 의미는 조작 규칙에서, 실제 동작은 동작 부품에서 한다. 동작 부품은 키를 읽지 않는다.

## 담당 파일 (2026-10-02 확인, Task-20260930-004 병합)

입력·규칙 (`Assets/Scripts/Player/`)
- `PlayerInputReader.cs` — 키 → 공통 역할 값(`Current`). F 짧게/길게 판정(`Long Press Time`).
- `PlayerInputFrame.cs` — 이동·자세 제어·기본 액션·보조 방향·상호작용 값.
- `PlayerController.cs` — 매 프레임 조작 규칙 실행. `SetScheme`으로 교체.
- `PlayerControlScheme.cs` — 조작 규칙 기반(ScriptableObject) + `PlayerControlContext`(동작 부품 묶음).
- `TightropeControlScheme.cs` — 외줄 규칙. 에셋: `Assets/Resources/Input/TightropeControlScheme.asset`.

동작 부품
- `PlayerMover.cs` — 코스 방향 이동, 제자리 점프(1.0m), 옆줄 점프(줄 간격 2.0m, 방향 고정), 공중 움직임 고정, 조작 잠금, 출발 위치 복귀(`SetStartPose`). 플레이어끼리 밀지 않음·같은 줄 앞뒤 막힘, 옆줄 도착점 겹침 검사(`FindPlayerAtLaneLanding`, 임시로 점프 막음 `BlockLaneJumpOntoPlayer`).
- `PlayerBalance.cs` — 균형 ±100, 자연 흔들림·기울기 가속·목마 배율·위층 전달·착지/옆줄 충격, 빨강 2초 추락 신호.
- `PlayerInteraction.cs` — 상호작용·해제 요청 신호.
- `PlayerModelSlot.cs` — 모델 교체, 키 1.73m 맞춤.

겉모습·테스트
- `BalanceTiltView.cs`(몸 기울기), `PlayerRagdoll.cs`(추락 래그돌), `BalanceGaugeView.cs`(게이지 UI 값 반영)
- `PlayerCommandDebugHud.cs` — 테스트용 HUD(P 표시, B 균형, R 재시작, M 모델). 테스트 씬 한정 연결 포함.
- `Editor/BalanceGaugePrefabBuilder.cs`, `Editor/PlayerModelPrefabBuilder.cs` — 그레이박스 프리팹 생성 메뉴(Tools/CurtainCall).

에셋
- `Assets/Resources/Input/PlayerControls.inputactions` — 맵 `Common`: Move, Posture, Action, DirectionLeft, DirectionRight, Interact.
- `Assets/Resources/Prefabs/Characters/Players/Player.prefab` — 루트에 CharacterController·위 컴포넌트. 모델은 `ModelSlot` 아래.
- `Assets/Resources/Prefabs/Characters/Players/Models/` — `DefaultCapsule`, `BlockDoll`(관절 래그돌), `Materials/`.
- `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab` — Canvas + TMP(임시 폰트 `Assets/Resources/Fonts/Pretendard-Medium/`).
- 테스트 씬: `Assets/Scenes/Tests/PlayerControl/PlayerControl.unity`.

## 진입점 (다른 기능이 쓰는 공개 함수)

```csharp
controller.SetScheme(scheme);                     // 콘텐츠별 조작 규칙 교체
mover.SetControlEnabled(false); mover.ResetToStart();
mover.LaneJumpFilter = dir => 착지 가능한가;      // 외줄(011)
mover.AirborneChanged += airborne => ...;  mover.CurrentJump  // InPlace / Lane / Fall
balance.SetBalanceActive(true);                   // 줄에 오를 때 켬
balance.Fell += () => ...;  balance.BalanceReset += () => ...;
balance.ApplyShock(10); balance.SetLaneLandingReduction(0.5f);
balance.ForceFall();                              // 균형과 상관없이 즉시 추락(줄 밖 착지 등)
balance.SetStackSize(n); balance.SetUpperBalanceSum(sum);   // 목마(006)
interaction.InteractRequested += ...; interaction.ReleaseRequested += ...;
modelSlot.SetModel(prefab);
```

- 입력 값(`SetMoveInput`, `RequestJump`, `RequestLaneJump`, `SetCorrectionInput`)은 조작 규칙만 넣는다. 한 프레임만 유효하다.

## 의존 도메인

- [Unity Assets 구조](assets-structure.md) — 폴더·Resources 배치.
- 사용하는 쪽: 플레이어 동기화(007), 외줄 코스·진행(005), 외줄 위 캐릭터(011), 목마(006), 도구(008).

## 수정 주의점

- 아직 **혼자 기준**이다(2026-10-02). 모든 플레이어가 키보드 입력을 받고, 게이지·HUD는 "씬에서 처음 찾은 플레이어"를 보이며, 흔들림 방향·추락 판정·래그돌 물리가 각자 계산된다. 소유자 처리·호스트 판정은 007이 붙인다.
- 테스트 HUD의 추락→조작 잠금, R 재시작 연결은 테스트용이다. 실제 연결은 005·011이 한다.
- 그레이박스 생성 메뉴는 같은 경로를 덮어쓴다. 디자인을 바꾼 프리팹에는 다시 실행하지 않는다.
- 플레이어 프리팹·입력·UI 폴더는 공용 파일이다. 수정 Task는 통합 소유 표를 따른다.
