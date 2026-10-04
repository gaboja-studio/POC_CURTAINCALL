# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 구현 중. 신체 상태·사망·공유·잘린 부위 연출 완료, MPPM 2인 테스트 통과(2026-10-04 PM).
- 동작하는 것: 네 팔다리 모두 잃으면 사망·래그돌, 잘린 팔다리가 날아가 래그돌(몸은 유지), 두 다리 잃으면 몸통이 바닥에 닿고 기어가기(×0.5·캡슐 0.9m·점프/옆줄 가능), 외줄 불이익 정책(BodyDamageEffects + TightropeBodyPenalty, 본인만), 재시작 복구 설정, 디버그 키 1~5(`TightropeDamage` 씬, 화면에 불이익 수치 표시). Player 모델 BlockDoll.
- 막힌 것: 없음. 스킨 메시 휴머노이드 분리는 아트 모델이 없어 미시험.
- PR: 없음

## 다음 할 일

1. 기어가기·외줄 불이익 2인 플레이 확인 결과 반영(AI 오프라인 수치 확인만 함).
2. 012 절단 API(`NetworkPlayer.ServerCutPart`·`ServerCutLegThenArm`) 전달, 006(`PlayerInteraction.ReachMultiplier`, 쌓는 높이 = 실제 캡슐)·017(LostArms·LostLegs, 기어가기 클립) 연결 항목 정리.
3. plan.md 공동 테스트 항목을 지금 규칙으로 갱신 → 검사 → PR 제출. 커밋·push는 사용자가 요청할 때만.
4. 기획 공유 문서: https://claude.ai/code/artifact/b0552187-8c6f-4c6f-9551-96541176c2cd

## PM이 확인할 것

- 확인 요청: 없음
- 대기 중인 결정: 기어가기 세부, 캡슐 높이, 다리 없는 사람의 목마·수직 톱날 규칙, 톱날 좌우 선택, 임시 패널티 수치.
- 공용 파일 요청: 012 plan·Integration plan에 이전 사망 조건이 남아 있음(PM 정리).

## Verification

- 구역 검사: PASS (10-04)
- 1 문서/규칙: PASS (10-04)
- 2 컴파일: PASS (10-04)
- 3 테스트: NO_PROJECT_TESTS (10-04) — check-work는 FAIL로 표시: 에디터가 열려 있어 unity test 거부(Pitfall unity-test-editor-open). Pipeline list_tests 0개.
- 4 실행 로그: 재질 소수점 자동 변경 되돌림(Pitfall material-color-float-churn)
- 5 공동 테스트: 사람 MPPM 2인 확인 통과(절단·사망·재시작·다리 없는 높이). 기어가기·외줄 불이익 2인 결과는 미보고. 공동 테스트 미요청
