# Plan

## Context

부상 기획(2026-10-13)으로 절단 순서가 고정되고(왼다리 → 오른다리 → 왼팔 → 오른팔), 사망 후에는 묘기를 다시 시작하지 않고 다음 시간대에 외팔 상태로 부활하며, 신체 복구는 밤 의료 전문가에게 받는다. 지금 코드에는 잃은 팔다리 상태·동기화·사망 확정은 있지만 위치 기준 절단, 묘기 재시작 시 복구가 남아 있다.

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Decisions/injury-rules.md`, `Harness/Project/Decisions/run-rules.md`, `Docs/Domains/player-sync.md`

## Approach

1. 공통 절단 함수를 고정 순서로 만든다(`PlayerCondition.NextLegThenArmCut(leftFirst: true)`가 이미 같은 순서). 한 번에 N곳 절단(난이도 3축·추격자)과 외팔 상태로 설정, 역순 1개 복구를 더한다. 외줄이 쓰는 기존 함수는 남겨 깨지지 않게 한다.
2. 사망: 4곳 손실·추락·외팔로 잡힘. 사망한 사람은 관전(`CameraSpectator`)하고, 028의 다음 시간대 시작 알림 때 대기실에서 외팔 상태로 부활한다. 생존자는 부상 그대로.
3. 장소별 상태 규칙: 공연(사망) / 백스테이지(잡히면 외팔, 외팔이면 사망) / 탈출로(사망·잡힘 = 리타이어). `ServerCanChangeState` 교체 방식을 제안한다. 리타이어 상태는 `PlayerState` 끝에 추가.
4. 패널티: 점프 높이 배율 추가(다리 단계별), 탐색 중 부상 패널티 정책 1개(더미 값). 기존 묘기 재시작 복구 설정(`BodyRestartRecovery`)은 폐기 표시.
5. 기물 소지 칸 수: 공통 세팅(팔 2개 2·팔 1개 1·배낭 +2)과 남은 팔 개수로 계산하는 함수만 둔다(기물 자체는 범위 밖).
6. 세대 교체 시 잃은 팔다리 0.

## Files

- 수정 예정: `Assets/Scripts/Systems/Survival/`, `Assets/Scenes/Tests/Systems/Survival/InjurySurvival.unity`, 배정된 플레이어 상태·몸 상태·이동·Damage 파일
- 읽기 전용 참고: `Docs/Domains/player-sync.md`, 028·031 결과물, 외줄 톱날 코드(`Assets/Scripts/MainStage/Tightrope/Saws/`)

## Risks

- `PlayerState`·`BodyPart`는 숫자로 전송한다. 기존 값을 바꾸지 말고 끝에만 추가한다.
- 외줄 실제 코드(톱날 절단 위치·실패 후 재시작)는 4단계까지 그대로 둔다. 공통 함수를 바꿀 때 외줄 테스트 씬이 깨지지 않는지 확인한다.
- 패널티·복구 비용 수치는 미확정(보상 시스템 문서). 세팅 임시값.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: InjurySurvival 테스트 씬에서 절단·사망·부활·복구 확인, Console 오류 0 기록

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 어디를 맞든 왼다리 → 오른다리 → 왼팔 → 오른팔 순서로 잃고, 네 번째 절단·추락이면 사망한다.
2. 공연 중 사망한 사람은 관전하다가 다음 시간대 시작 때 외팔 상태(잃은 팔다리 3)로 부활하고, 생존자는 부상 그대로다.
3. 신체 복구로 역순(왼팔 → 오른다리 → 왼다리) 1개씩 되찾고, 세대 교체 때 모두 온전한 몸이 된다.
