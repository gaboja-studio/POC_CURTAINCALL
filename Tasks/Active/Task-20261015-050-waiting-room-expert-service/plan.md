# Plan

## Context

CC-132 개발 순서의 이 단계만 구현한다. 2026-10-15 PM 결정으로 앞 단계 완료를 기다리지 않고 착수한다. 단계 전용 씬에서 독립 검증하고, 앞 단계 기능이 필요하면 통합 병합분을 받아 쓴다.

## Approach

1. setup 구역(단계 전용 스크립트 폴더·테스트 씬) 안에서 구현한다. 1단계 코드(`Assets/Scripts/WaitingRoom/Base/`)는 수정하지 않는다.
2. 아래 공동 테스트 항목을 완료 기준으로 삼고, 범위 밖 기능은 추가하지 않는다.
3. 공용 인터페이스가 부족하면 직접 고치지 않고 PM에게 별도 소유 Task를 요청한다.

## Files

- 수정 예정: `Assets/Scripts/WaitingRoom/ExpertService/`, `Assets/Scenes/Tests/WaitingRoom/ExpertService/`(WaitingRoomExpertService.unity 포함), 이 Task 문서.
- 읽기 전용: `Harness/Project/Decisions/Waiting-room/night-maintenance.md`, `Harness/Project/Facts/game-flow.md`, 공용 Systems.

## Risks

- 미정 항목을 AI가 임의로 확정하지 않는다(meta Open).
- 4인 동기화 결과가 화면마다 다를 위험.
- 단계 동시 진행으로 통합 병합 때 다른 단계·1단계 루프와 연결·충돌이 생길 수 있다. 병합 순서는 PM이 정한다.

## Auto Verification

- 구역 → 문서 → 컴파일 → 자동 테스트: `pwsh -File Scripts/check-work.ps1 -Mode PlayMode`.
- PlayMode 테스트가 없으면 NO_PROJECT_TESTS. 생략·환경 차단은 handoff에 기록한다.

## 공동 테스트 항목

CC-132 단계 기준. 실행 씬: `Assets/Scenes/Tests/WaitingRoom/ExpertService/WaitingRoomExpertService.unity`. 4인(호스트 1·참가자 3). handoff에 일시·참여자·씬·재현·예상·실제를 기록한다. 현재 전부 미실행이다.

1. 구매·입장: 서비스 구매 → ⑧ 문 열림. 예상: 구매자만 입장, 나머지는 통과 불가, 네 화면 일치.
2. 의료 복구: 1·3일차 밤 결손 플레이어 이용. 예상: 신체 복구 후 재등장, 상태 동기화.
3. 부재중: 2일차 밤. 예상: 의료 비활성·'담당자 부재중' 표시, 구매 불가.
