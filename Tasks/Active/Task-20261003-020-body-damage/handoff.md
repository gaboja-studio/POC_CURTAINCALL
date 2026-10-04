# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 제출. 신체 상태·사망·공유·잘린 부위 연출 완료, MPPM 2인 테스트 통과(2026-10-04 PM). 2026-10-05 011 옆줄 규칙과 함께 2인 확인: 버그 없음(아래 Verification).
- 동작하는 것: 네 팔다리 모두 잃으면 사망·래그돌, 잘린 팔다리가 날아가 래그돌(몸은 유지), 두 다리 잃으면 몸통이 바닥에 닿고 기어가기(×0.5·캡슐 0.9m·점프/옆줄 가능), 외줄 불이익 정책(BodyDamageEffects + TightropeBodyPenalty, 본인만), 재시작 복구 설정, 디버그 키 1~5(`TightropeDamage` 씬, 화면에 불이익 수치 표시). Player 모델 BlockDoll.
- 막힌 것: 없음. 스킨 메시 휴머노이드 분리는 아트 모델이 없어 미시험.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/22

## 다음 할 일

1. PR 리뷰·병합 대기(PM). 병합 후 공동 테스트(plan.md 3개 항목).
2. 후속 Task 연결은 plan.md "다른 Task와의 연결" 참고: 012 절단 API, 006 거리 배율·쌓는 높이, 017 LostArms·LostLegs·기어가기 클립.
3. 006 참고: 동료 자리로 옆줄 착지 시 뒤쪽에 줄이 없으면(플랫폼 등) 착지 순간 추락하는 011 규칙 — 목마 합체가 이 경우를 대신하는지 006에서 확인(2026-10-05 사용자 메모, 이번 Task에서 변경 안 함).
4. 기획 공유 문서: https://claude.ai/code/artifact/b0552187-8c6f-4c6f-9551-96541176c2cd

## PM이 확인할 것

- 확인 요청: 없음
- 대기 중인 결정: 기어가기 세부, 캡슐 높이, 다리 없는 사람의 목마·수직 톱날 규칙, 톱날 좌우 선택, 임시 패널티 수치.
- 공용 파일 요청: 012 plan·Integration plan에 이전 사망 조건이 남아 있음(PM 정리).

## Verification

- 구역 검사: PASS (10-05)
- 1 문서/규칙: PASS (10-05)
- 2 컴파일: PASS (10-05)
- 3 테스트: NO_PROJECT_TESTS (10-05) — check-work는 FAIL로 표시: 에디터가 열려 있어 unity test 거부(Pitfall unity-test-editor-open). 열린 에디터의 Pipeline list_tests 0개.
- 4 실행 로그: 재질 소수점·TMP 폰트 아틀라스 자동 변경 되돌림(Pitfall material-color-float-churn, tmp-dynamic-font-churn)
- 011 합침: 충돌 없음, 함께 컴파일, 옆줄 착지 충격(다리 없음×손잡기) 계산 확인. 2026-10-05 사람 2인 플레이로 함께 확인, 버그 없음
- 5 공동 테스트: 사람 MPPM 2인 확인 통과(절단·사망·재시작·다리 없는 높이, 10-05 011과 함께). 재시작 때 살아 있는 사람의 손상 유지는 기본 설정(생존자 유지)대로의 정상 동작으로 확인. 공동 테스트(병합 후) 미요청
