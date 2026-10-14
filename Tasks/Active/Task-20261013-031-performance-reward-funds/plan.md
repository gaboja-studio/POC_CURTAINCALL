# Plan

## Context

공연은 팀 단위로 진행하지만 정산은 개인 단위다. 성공하면 남은 팀 호응 게이지가 공동 자금으로 들어가고, 사망자는 그 공연 보상을 잃는다. 생존 2명 미만이면 묘기 공통으로 실패한다. 지금은 외줄 전용 결과 명부와 빈 보상 규칙 틀만 있고, 게이지·공동 자금은 없다.

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Facts/game-flow.md`(공연·팀 호응 게이지·공동 자금), `Harness/Project/Decisions/run-rules.md` #2, `Harness/Project/Decisions/injury-rules.md` #9

## Approach

1. 묘기 공통 공연 결과: 성공/실패, 종료 이유(도착·전원 사망·생존 2명 미만·제한 시간), 사망자 목록, 플레이어별 시작/종료 잃은 팔다리 수, 플레이어별 정산액, 전원 생존 여부. 033은 결과 명부를 옮기지 않았다(2026-10-14 PM, Damage만 이전). 외줄 `MainStage/Tightrope/Results/`의 공통부(참가자 명부·불변 결과 스냅샷·호스트 공유/검증·이력)를 참고해 `Systems/Results/`에 새로 만들고, 외줄 전용부(`TightropeEndReason`, `Arrived` 기준 직접 완주/동반 성공 판정, 남은 시간·묘기 경과 의미, 메시지 이름)는 넣지 않는다. 종료 이유·참가자 판정은 묘기가 넘기는 값으로 받는다(경계안: Task-033 handoff).
2. 생존 2명 미만 실패 판정은 공통으로 둔다. 개발자 모드(034)가 켜져 있으면 예외.
3. 팀 호응 게이지: 플레이어별 구간, 환호 +·야유 −(0 하한), 사망 시 구간 제거, 절단 보너스. 증감 이벤트는 묘기가 보내고 수치는 보상 규칙 파일(`RewardTable` 구현, 임시값).
4. 공동 자금: 팀 공용 잔액 하나, 호스트 확정·공유, 사용 내역(장부) 기록. 런 범위(일차가 바뀌어도 유지, 세대 교체 때 0).
5. 공연 결과가 확정되면 028(시간대 전환)과 030(사망자 부활)에 알린다.

## Files

- 수정 예정: `Assets/Scripts/Systems/Reward/`, `Assets/Scripts/Systems/Results/`, `Assets/Scenes/Tests/Systems/Reward/PerformanceReward.unity`, 배정된 보상 규칙 파일
- 읽기 전용 참고: `Assets/Scripts/Systems/Telemetry/`(판정에 쓰지 않음), 외줄 결과 코드 `Assets/Scripts/MainStage/Tightrope/Results/`(쓰는 곳: `Rope/TightropeRun.cs`·`Telemetry/TightropeTelemetry.cs`·`UI/TightropeDemoUI.cs`), 028·030 결과물

## Risks

- 보상·보너스 수치는 보상 시스템 문서 전까지 임시값. 절단 보너스는 신체 복구 비용보다 낮게 둔다.
- 공동 자금은 호스트만 바꾼다. 클라이언트는 받아서 보여 주기만 한다.
- 외줄 실제 결과 흐름은 4단계까지 그대로 둔다. 외줄 `TightropeResults`와 새 공통 결과가 잠시 함께 있다(외줄 결과 테스트 씬 `TightropeResults.unity`는 빈 씬, 외줄 확인은 `Assets/Scenes/Prototypes/proto_0.0.2.unity`).

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: PerformanceReward 테스트 씬에서 성공·실패·사망 정산과 자금 변화 확인, Console 오류 0 기록

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 성공하면 생존자 구간 합이 공동 자금에 더해지고, 사망자 구간은 빠진다. 실패하면 0이다.
2. 생존자가 1명이 되면 공연이 실패로 끝나고(개발자 모드에서는 예외), 결과에 사망자와 잃은 팔다리 변화가 담긴다.
3. 모든 플레이어가 같은 공동 자금을 보고, 다음 날에도 유지되며 세대 교체 때 0이 된다.
