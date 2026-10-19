# 플레이어 조작 (입력·이동·균형·겉모습)

## 목적

플레이어 캐릭터 한 명의 조작과 동작을 담당한다: 키 입력, 코스 이동·점프·옆줄 점프, 외줄 균형(흔들림·추락 신호), 상호작용 신호, 모델 교체·몸 기울기·추락 래그돌.
구조 결정: [조작 3층 구조](../../Harness/Project/Decisions/player-control-architecture.md) · 균형 규칙: [외줄 균형](../../Harness/Project/Decisions/tightrope-balance.md) · 온라인: [기능+동기화](../../Harness/Project/Decisions/feature-with-network.md)

## 구조 (입력 → 조작 규칙 → 동작 부품)

```
키 ─(입력 에셋)→ PlayerInputReader ─ PlayerInputFrame(공통 역할) ─→ PlayerController ─→ 조작 규칙(전략) ─→ 동작 부품
```

- 키 변경은 입력 에셋에서, 콘텐츠별 키 의미는 조작 규칙에서, 실제 동작은 동작 부품에서 한다. 동작 부품은 키를 읽지 않는다.

## 담당 파일 (2026-10-03 확인, Task-004 병합 + 005 추가분)

입력·규칙 (`Assets/Scripts/Systems/Player/`)
- `PlayerInputReader.cs` — 키 → 공통 역할 값(`Current`). F 짧게/길게 판정(`Long Press Time`).
- `PlayerInputFrame.cs` — 이동·자세 제어·기본 액션·보조 방향·상호작용 값, 달리기·앉기 홀드(`SprintHeld`·`CrouchHeld`).
- `PlayerController.cs` — 매 프레임 조작 규칙 실행. `SetScheme`으로 교체.
- `PlayerControlScheme.cs` — 조작 규칙 기반(ScriptableObject) + `PlayerControlContext`(동작 부품 묶음, 플레이어별 달리기·앉기 누름 판정 `TrackLocomotionPresses`·미정 로그 `LogUndefinedLocomotion`).
- `TightropeControlScheme.cs` — 외줄 규칙. 에셋: `Assets/Resources/Input/TightropeControlScheme.asset`.

동작 부품
- `PlayerMover.cs` — 코스 방향 이동(줄 위 0.5m/s), 일반 이동(`FreeMovement`, 플랫폼 8방향 3m/s·이동 방향 바라봄), 제자리 점프(0.8m), 옆줄 점프(거리 = `LaneSpacing`, 외줄 코스가 3m로 맞춤, 방향 고정), 공중 움직임 고정, 달리기·앉기(Task-059: 속도 = 기본 × 부상 × 자세 배율, 앉으면 점프 금지·캡슐 높이 = min(정상 × 앉기 비율, 부상 높이)·머리 위가 막히면 떼도 앉은 채 유지 `IsCrouched`, 몸이 커질 때 공간 검사), 조작 잠금, 출발 위치(`SetStartPose`, 순간이동 없이 바꾸기 가능). 산 플레이어끼리 줄 위 앞뒤·플랫폼 모두 막힘, 사망 플레이어 통과(`PassThrough`), 몸통으로 래그돌 밀기(`pushSpeed`), 옆줄 도착점 겹침 검사(`FindPlayerAtLaneLanding`, 임시로 점프 막음 `BlockLaneJumpOntoPlayer`).
- `PlayerBalance.cs` — 균형 ±100, 자연 흔들림·기울기 가속·목마 배율·위층 전달·착지/옆줄 충격(옆줄 흔들림 배율 프리팹 1.75, #17), 빨강 2초 추락 신호.
- `PlayerCondition.cs`·`BodyPart.cs` — 신체 부위 손실 상태(준비만, 프리팹 미부착 → 020에서 부착·공유). 디메리트는 콘텐츠 조작 규칙이 `PlayerControlContext.Condition`으로 읽어 처리. 게임 전체 상태(완전히 죽기 전까지 유지, 이후 부품 수리): `Harness/Project/Decisions/body-damage.md`.
- `PlayerInteraction.cs` — 상호작용·해제 요청 신호.
- `PlayerModelSlot.cs` — 모델 교체, 키 1.73m 맞춤(충돌체는 2026-10-03부터 캡슐 높이 1.5m·지름 0.6m). 앉으면 슬롯을 발 기준으로 위아래만 줄인다(`PostureHeightScale`, 모델 측정·분리 때는 원래 크기).

겉모습·테스트
- `BalanceTiltView.cs`(몸 기울기), `PlayerRagdoll.cs`(추락 래그돌), `BalanceGaugeView.cs`(게이지 UI 값 반영)
- `PlayerCommandDebugHud.cs` — 테스트용 HUD(P 표시, B 균형, R 재시작, M 모델). 테스트 씬 한정 연결 포함.
- `Editor/BalanceGaugePrefabBuilder.cs`, `Editor/PlayerModelPrefabBuilder.cs` — 그레이박스 프리팹 생성 메뉴(Tools/CurtainCall).

에셋
- `Assets/Resources/Input/PlayerControls.inputactions` — 맵 `Common`: Move, Posture, Action, DirectionLeft, DirectionRight, Interact, Sprint(LShift), Crouch(LCtrl).
- `Assets/Resources/Prefabs/Characters/Players/Player.prefab` — 루트에 CharacterController·위 컴포넌트. 모델은 `ModelSlot` 아래. 수치 칸 없음(013): 속도·중력·몸통 크기·균형·연출 값은 `GameSettings.Base`, 줄 위 동작·목마는 `GameSettings.Tightrope`(`Assets/Scripts/Systems/Settings/`)에서 매 프레임 읽는다.
- `Assets/Resources/Prefabs/Characters/Players/Models/` — `DefaultCapsule`, `BlockDoll`(관절 래그돌), `Materials/`.
- `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab` — Canvas + TMP(임시 폰트 `Assets/Resources/Fonts/Pretendard-Medium/`).
- 테스트 씬: `Assets/Scenes/Tests/Systems/PlayerControl/PlayerControl.unity`.

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

- 입력 값(`SetMoveInput`, `SetLocomotionInput`, `RequestJump`, `RequestLaneJump`, `SetCorrectionInput`)은 조작 규칙만 넣는다. 한 프레임만 유효하다.

## 달리기·앉기 (Task-20261019-059, 2026-10-19 PM)

- LShift 달리기·LCtrl 앉기는 다른 키 조합 없이 **누르는 동안만** 켜진다(이전 문서의 조합 토글은 적용하지 않음). 동시에 누르면 앉기 우선.
- 배율·비율은 `GameSettings.Base` 캐릭터: 달리기 1.5, 앉기 이동 0.5, 앉기 높이 0.6(플레이 중 반영).
- 플랫폼 규칙(`PlatformControlScheme.ApplyLocomotion`)과 배치 미리보기·인간 대포 플랫폼이 같은 함수를 쓴다. 줄 위·대포 탑승/비행은 동작 없이 누른 순간 로그만 남긴다.
- 실제 앉은 상태는 `NetworkPlayer`가 owner-write로 공유하고, 남의 캐릭터는 `SetRemoteCrouched`로 겉모습·몸통 높이만 맞춘다.
- 1인칭: 몸통 높이 기준 눈높이는 자동으로 낮아지고, 고정 눈높이(프리셋 `EyeHeight`, 배치 미리보기)는 `PostureHeightScale`을 곱한다.

## 의존 도메인

- [Unity Assets 구조](assets-structure.md) — 폴더·Resources 배치.
- 사용하는 쪽: [플레이어 동기화](player-sync.md)(007), [외줄 코스·진행](tightrope-course.md)(005 — 플랫폼 조작 규칙·전환), 외줄 위 캐릭터(011), 목마(006), 도구(008).

## 수정 주의점

- 이 도메인은 **혼자 기준 동작 부품**이다. 온라인 처리(남의 캐릭터 끄기·값 공유·호스트 판정)는 [플레이어 동기화](player-sync.md)가 붙인다. 온라인에서 부품을 직접 켜고 끄지 말고 그쪽 진입점을 쓴다.
- 온라인용 부품 함수(007): `PlayerMover.Simulated`·`Teleported`·`All`·`Body`, `PlayerBalance.SetDisplayedState`, 게이지·HUD `Target`·`AutoFindTarget`, HUD `RestartOverride`.
- 테스트 HUD의 추락→조작 잠금, R 재시작 연결은 테스트용이다. 외줄 재시작은 005 `TightropeRun`(호스트만)이 한다.
- 산 플레이어끼리는 밀지 않고 막기만 한다(줄 위 앞뒤·플랫폼). 사망하면 몸통은 통과, 래그돌은 산 플레이어 몸통에 밀린다(2026-10-03 결정). `Block Lane Jump Onto Player`는 목마(006) 전 임시.
- 그레이박스 생성 메뉴는 같은 경로를 덮어쓴다. 디자인을 바꾼 프리팹에는 다시 실행하지 않는다.
- 플레이어 프리팹·입력·UI 폴더는 공용 파일이다. 수정 Task는 통합 소유 표를 따른다.
