# Plan

## Context

부상 효과 적용기·부상 패널티 틀·절단 연출·공연 결과 명부는 모든 공간(대기실·백스테이지·탈출로)이 써야 하는데 공연장 폴더(`Assets/Scripts/MainStage/Tightrope/`)에 있다. 공통 폴더(`Assets/Scripts/Systems/`)로 옮겨 2단계 Task(030 부상·생존, 031 공연 결과)가 공통 위치에서 시작하게 한다. **동작은 바꾸지 않는다.**

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Decisions/space-workspaces.md`(공용 코드 위치)

## Approach

1. 옮길 파일: `Damage/BodyDamageEffects.cs`, `BodyPenaltyPolicy.cs`, `LimbDismemberment.cs`, `ModelLimbs.cs` → `Systems/Damage/`. 외줄 전용 `TightropeBodyPenalty.cs`, `BodyDamageDebug.cs`는 남긴다.
2. 결과 명부: `Results/TightropeResult(s).cs`에서 묘기 공통 부분(참가자 명부·확정 결과 자료)과 외줄 전용 부분(종료 이유 등)의 경계를 먼저 제안하고, PM 확인 뒤 공통 부분을 `Systems/Results/`로 옮긴다.
3. Unity 안에서(또는 `.meta`째) 옮겨 GUID를 유지한다. 프리팹·씬 참조가 그대로인지 확인한다.
4. 공통 코드가 공연장 코드를 부르지 않게 한다. 예: `BodyDamageEffects`가 정책이 비면 외줄 정책을 만들어 쓰는 부분은 외줄 쪽에서 정책을 끼우도록 바꾸되, 외줄에서의 결과는 같게 한다.
5. 지도 3개(`common-game-systems`·`tightrope-course`·`player-sync`)의 경로를 갱신하고 `integration/common-game-systems`로 제출한다.

## Files

- 수정 예정: `Assets/Scripts/Systems/Damage/`, `Assets/Scripts/Systems/Results/`, `Assets/Scripts/MainStage/Tightrope/Damage/`, `Assets/Scripts/MainStage/Tightrope/Results/`, 지도 3개
- 읽기 전용 참고: 외줄 테스트 씬(부상·결과), `Assets/Resources/Prefabs/Characters/Players/Player.prefab`

## Risks

- `.meta` 없이 옮기면 GUID가 바뀌어 프리팹의 컴포넌트가 Missing이 된다.
- 공간 통합 브랜치(`integration/main-stage` 등)는 dev를 받을 때 새 경로를 쓰게 된다. dev 반영 후 공간 담당에게 알린다.
- 결과 명부 분리가 커지면 031로 넘기고 이 Task는 Damage 이전만 끝낸다(PM 확인).

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 외줄 부상·결과 테스트 씬에서 Missing 컴포넌트 0, Console 오류 0 기록

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 외줄 부상 테스트 씬에서 팔다리 절단·기어가기·패널티가 옮기기 전과 똑같이 동작한다.
2. 외줄 결과 테스트 씬에서 성공/실패 결과와 참가자 결과가 옮기기 전과 같게 나온다.
3. Player 프리팹과 외줄 씬에 Missing 컴포넌트가 없다.
