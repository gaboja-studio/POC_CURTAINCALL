# Plan

## Context

CC-132 개발 순서의 이 단계만 구현한다. 앞 단계 루프 위에 쌓으며, 착수 전 PM이 실제 카드·구역을 확정한다. 현재는 예약 계획이다.

## Approach

1. 앞 단계 통합·소유 이관·이 단계 JIRA 카드 확인 후 setup 구역 안에서 구현한다.
2. 아래 공동 테스트 항목을 완료 기준으로 삼고, 범위 밖 기능은 추가하지 않는다.
3. 공용 인터페이스가 부족하면 직접 고치지 않고 PM에게 별도 소유 Task를 요청한다.

## Files

- 수정 예정: 착수 시 setup 작업 구역(예약: WaitingRoom/Base 스크립트·테스트)과 이 Task 문서.
- 읽기 전용: `Harness/Project/Decisions/Waiting-room/shop-storage.md`, `Harness/Project/Facts/game-flow.md`, 공용 Systems.

## Risks

- 미정 항목을 AI가 임의로 확정하지 않는다(meta Open).
- 4인 동기화 결과가 화면마다 다를 위험.

## Auto Verification

- 구역 → 문서 → 컴파일 → 자동 테스트: `pwsh -File Scripts/check-work.ps1 -Mode PlayMode`.
- PlayMode 테스트가 없으면 NO_PROJECT_TESTS. 생략·환경 차단은 handoff에 기록한다.

## 공동 테스트 항목

CC-132 단계 기준에서 옮긴 예정 항목이다. 4인(호스트 1·참가자 3), 착수 시 PM과 재합의한다. 현재 전부 미실행이다.

1. 구매·수령: 수화기로 구매 → 배송구 수령. 예상: 자금 감소·장부가 네 화면에 같고 수령은 1회.
2. 운반 제한: 1인 3개, 배낭 착용 시 +3. 예상: 한도 초과 집기 불가, 네 화면 소지 상태 일치.
3. 보관 유지: 소품실·선반에 보관 → 다음 공연 왕복. 예상: 기물이 그대로 남고 이전 상태 꼬임 없음.
