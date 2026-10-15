# Plan

## Context

CC-132 개발 순서의 이 단계만 구현한다. 앞 단계 루프 위에 쌓으며, 착수 전 PM이 실제 카드·구역을 확정한다. 현재는 예약 계획이다.

## Approach

1. 앞 단계 통합·소유 이관·이 단계 JIRA 카드 확인 후 setup 구역 안에서 구현한다.
2. 아래 공동 테스트 항목을 완료 기준으로 삼고, 범위 밖 기능은 추가하지 않는다.
3. 공용 인터페이스가 부족하면 직접 고치지 않고 PM에게 별도 소유 Task를 요청한다.

## Files

- 수정 예정: 착수 시 setup 작업 구역(예약: WaitingRoom/Base 스크립트·테스트)과 이 Task 문서.
- 읽기 전용: `Harness/Project/Decisions/Waiting-room/night-maintenance.md`, `Harness/Project/Facts/game-flow.md`, 공용 Systems.

## Risks

- 미정 항목을 AI가 임의로 확정하지 않는다(meta Open).
- 4인 동기화 결과가 화면마다 다를 위험.

## Auto Verification

- 구역 → 문서 → 컴파일 → 자동 테스트: `pwsh -File Scripts/check-work.ps1 -Mode PlayMode`.
- PlayMode 테스트가 없으면 NO_PROJECT_TESTS. 생략·환경 차단은 handoff에 기록한다.

## 공동 테스트 항목

CC-132 단계 기준에서 옮긴 예정 항목이다. 4인(호스트 1·참가자 3), 착수 시 PM과 재합의한다. 현재 전부 미실행이다.

1. 구매·입장: 서비스 구매 → ⑧ 문 열림. 예상: 구매자만 입장, 나머지는 통과 불가, 네 화면 일치.
2. 의료 복구: 1·3일차 밤 결손 플레이어 이용. 예상: 신체 복구 후 재등장, 상태 동기화.
3. 부재중: 2일차 밤. 예상: 의료 비활성·'담당자 부재중' 표시, 구매 불가.
