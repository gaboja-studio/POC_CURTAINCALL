# Plan

## Context

실제 공간(공연장·대기실·백스테이지·탈출로)이 만들어지기 전에 공통 게임 시스템(런 진행·부상·공연 결과·공동 자금·장소 전환)을 검증하려면 상황을 만들어 줄 공간이 필요하다. 버튼과 단순 구역 수준의 더미 공간을 만들고, 나중에 실제 공간으로 교체한다(`run-rules.md` #4).

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Facts/game-flow.md`, `Harness/Project/Facts/run-state.md`, `Harness/Project/Decisions/injury-rules.md`

## Approach

2026-10-15 PM 사용자 결정: 더미 묘기·대기실·탐색·탈출로는 4개 독립 씬. 035는 씬 안 상황만 검증하며 씬 전환·세션 유지는 032에서 연결한다. 빈 씬 4개와 .meta는 통합 브랜치에서 PM 준비 완료(로직 미구현).

1. 더미 묘기: 도착·사망·절단·제한 시간 버튼으로 공연 결과를 만든다(031 공연 결과 형식을 쓴다).
2. 더미 대기실: 로비(공연 후 시작점), 기물 구매, 신체 복구(의료 전문가 일정), 밤 정비 준비 완료.
3. 더미 탐색(백스테이지): 쉬는 시간 타이머(A/B 세팅), 대기실·무대 입구 통로(안전지대) 구역, 추격 시뮬레이션(잡힘 → 외팔·사망).
4. 더미 탈출로: 하역장 도착, 리타이어.
5. 대기실·탐색·탈출로 세팅 파일(028이 만든 틀)에 더미가 쓰는 값을 넣고, 값을 바꾸면 더미 동작이 바뀌게 한다.
6. 공간 사이 이동·데이터 전달은 032가 한다. 이 Task는 각 더미 씬 안에서 상황을 만들고 공통 시스템 함수를 부르기까지만 한다.

## Files

- 수정 예정: `Assets/Scripts/Systems/DummySpaces/`, `Assets/Scenes/Tests/Systems/DummySpaces/`, 배정 시 대기실·탐색·탈출로 세팅
- 읽기 전용 참고: 028·030·031 결과물, `Assets/Scenes/Levels/`(실제 공간, 참고만)

## Risks

- 028·030·031·037·038 통합 결과를 읽고 공개 API를 호출한다. 없는 함수·규칙을 더미에서 임시로 중복 구현하지 않고 PM에게 연결/소유 배정을 요청한다.
- 더미에 실제 게임 규칙을 넣지 않는다. 규칙은 공통 시스템에, 더미는 상황만 만든다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 더미 씬별로 상황 버튼 실행, Console 오류 0 기록

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 더미 묘기에서 도착·사망·절단 버튼으로 성공/실패 공연 결과를 만들 수 있다.
2. 더미 탐색에서 쉬는 시간 타이머가 세팅의 A/B 방식대로 시작하고, 안전지대 밖에서 끝나면 추격 결과(외팔·사망)가 난다.
3. 대기실·탐색·탈출로 세팅 값을 바꾸면 더미 동작(시간·개수 등)이 바뀐다.
