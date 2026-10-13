# Plan

## Context

기획의 하루 흐름은 공연과 자유시간을 반복한 뒤 밤에 다음 Day로 넘어간다. 현재 프로젝트에는 방 세션의 대기/진행/종료 상태와 일부 공연 결과 기록은 있지만, 캠페인의 세대·Day·하루 단계 진행은 없다. 이 Task는 캠페인 진행 상태를 분리해 제공하고, 공간이나 UI 구현 없이 규칙 전환을 검증 가능하게 한다.

근거: `Docs/References/game-flow.md` §2–5, §10–11; `Harness/Project/Facts/game-flow.md`; 세션 상태와 캠페인 상태의 경계는 `Docs/Domains/network-session.md` 참조.

## Approach

1. `setup.md` 작업 구역 안에서 캠페인 진행 상태와 전환만 구현하고 테스트한다.
2. 런 시작은 기획에 명시된 세대 1·Day 1·첫 공연 상태로 시작한다. 하루 공연 수와 마지막 Day는 기획상 조정값이므로 코드 상수로 고정하지 않는다.
3. 공연→자유시간 반복→밤→다음 Day 전환은 기획의 기본 흐름을 따른다. 전환 조건은 테스트로 확인한다.
4. 공간 씬 전환, 자유시간 타이머, 정산·구매·저장, 사망·추격자 이벤트 연결은 이 Task에서 구현하지 않는다. 필요하면 별도 연결 Task에서 다룬다.
5. 검사 후 `integration/*`로 제출한다.

## Files

- 수정 예정: `Assets/Scripts/Systems/Campaign/`, `Assets/Scenes/Tests/Systems/Campaign/CampaignFlow.unity`, `Assets/Scenes/Tests/Systems/Campaign/` 내 테스트 전용 구성
- 읽기 전용 참고: `Docs/References/game-flow.md`, `Harness/Project/Facts/game-flow.md`, `Docs/Domains/network-session.md`, `Assets/Scripts/Systems/Network/Session/GameSessionState.cs`
- 공용 파일: 없음

## Risks

- 하루 공연 수·마지막 Day는 튜닝값이다. 기본값만으로 이를 최종 규칙처럼 고정하지 말고, 테스트에서 명시적으로 지정한다.
- `network-session`의 방 진행 상태와 캠페인 단계 상태를 섞지 않는다.
- 캠페인 상태를 실제 장소 이동·정산·저장에 연결하는 것은 공간 담당자 소유 범위와 별도 결정이 필요하다.
- 현재 저장소에서 자동 테스트 폴더·Assembly Definition은 확인되지 않았다. 임의로 패키지나 프로젝트 설정을 바꾸지 않는다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 현재 자동 테스트 구성이 없으므로 `NO_PROJECT_TESTS` (별도 자동 테스트 체계가 승인되면 갱신)
- 4 실행 로그: Unity 테스트 씬에서 상태 전환을 확인하고 Console 오류 0을 기록. 실행하지 못하면 사유를 기록

## 공동 테스트 항목

아래는 구현 전 합의용 제안이다. 고정 공연 수·Day 수는 테스트 입력으로 지정하고, 실제 기획 수치를 확정하지 않는다.

1. 새 런이 세대 1·Day 1·첫 공연 상태로 시작하는지 확인한다.
2. 설정된 공연 수만큼 공연과 자유시간이 반복되고, 마지막 자유시간 다음에 밤 단계로 이동하는지 확인한다.
3. 밤 종료 시 Day가 증가하며, 마지막 Day 종료 시 다음 세대 Day 1로 넘어가는지 확인한다.
