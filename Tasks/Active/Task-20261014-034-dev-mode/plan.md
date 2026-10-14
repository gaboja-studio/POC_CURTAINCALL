# Plan

## Context

기획상 최소 인원이 2명이 되고, 공연은 생존 2명 미만이면 실패한다(`run-rules.md` #2·#5). 혼자서도 개발·테스트할 수 있도록 인원 규칙을 예외 처리하는 개발자 모드를 만든다. 협력을 보는 테스트 AI 동료는 가능성만 검토한다.

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Decisions/run-rules.md`, `Docs/Domains/network-session.md`

## Approach

1. 켜는 방법과 예외 규칙 목록을 먼저 제안하고 PM 확인을 받는다. 빌드 기본은 꺼짐.
2. 다른 Task(028 최소 인원, 031 생존 2명 미만 실패)가 읽을 수 있는 개발자 모드 상태 하나를 `Systems/DevMode/`에 둔다. 각 규칙은 이 상태를 읽어 예외를 적용한다.
3. 세션 시작 판정에 최소 인원 예외가 필요하면 배정받은 `NetworkSessionManager.cs`만 고친다.
4. 테스트 AI 동료 검토: 호스트가 가진 캐릭터가 모두 `NetworkPlayer.Local`로 잡히는 구조(`NetworkPlayer.cs:215`) 등 필요한 변경과 규모를 handoff에 정리한다. 구현하지 않는다.

## Files

- 수정 예정: `Assets/Scripts/Systems/DevMode/`, `Assets/Scenes/Tests/Systems/DevMode/DevMode.unity`, 배정 시 `NetworkSessionManager.cs`
- 읽기 전용 참고: `Assets/Scripts/Systems/Network/`, 028·031 결과물

## Risks

- 개발자 모드가 실제 빌드에서 켜져 있으면 정식 규칙이 깨진다. 기본 꺼짐과 화면 표시로 켜져 있음을 알 수 있게 한다.
- 028·031이 같은 시기에 진행되므로, 예외를 읽는 지점(함수 이름)을 2단계 시작 때 먼저 약속한다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: DevMode 테스트 씬에서 1인 시작·공연 넘기기 확인, Console 오류 0 기록

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 개발자 모드를 켜면 1명으로 게임을 시작할 수 있고, 끄면 2명 미만에서 시작이 막힌다.
2. 개발자 모드에서는 1명이 공연을 끝까지 진행할 수 있고, 끄면 생존 2명 미만으로 실패한다.
3. 테스트 AI 동료 검토 결과(필요한 변경·규모)가 handoff에 있다.
