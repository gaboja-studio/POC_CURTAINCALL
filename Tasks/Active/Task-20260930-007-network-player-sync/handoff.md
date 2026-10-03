# Handoff

## 지금 상태

- 단계: 구현 ①~⑦ 완료·작업자 확인(2026-10-02). 이슈 #9(충돌)·#10(옆줄 착지)·줄 밖 착지 추락·모두 재시작 포함. 구조·진입점은 `Docs/Domains/player-sync.md`
- 기획과 다른 값(인스펙터): 줄 간격 1.5→2.0m, 옆줄 도착점 겹치면 점프 막음(목마 전 임시), 줄 밖 판정 0.25m·전원 추락 3초 후 재시작(테스트 부품), 카메라 뒤 4.5·위 2.6·FOV 60
- 막힌 것: 없음 / PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/11 (병합됨 `3a721da`, #9·#10 닫음, QA #13 통과 → integrated)

## 다음 할 일

1. PM: PR #11 병합(#9·#10 자동 닫힘) → 병합된 통합 브랜치에서 공동 테스트 3개(qa-feature).
2. 저장·검사 전 MPPM 가상 플레이어를 끈다(아래 확인 요청).
3. 공용 파일(플레이어 프리팹·UI·NetworkManager 프리팹) 수정은 `setup.md` 배정 범위 안에서만.

## PM이 확인할 것

- 확인 요청: `verify-unity.ps1`(저장 검사)이 MPPM 가상 플레이어(`Library/VP/…`)도 같은 경로 에디터로 세어 "에디터 2개, 모호"로 멈춤(beta.12 `unity status`가 VP를 표시). 끄고 저장함. 스크립트 수정은 PM 하네스 작업(교훈 제안: 함정)
- 확인 요청: unity-cli beta.12 변경점을 `Harness/Engine/Unity/Facts/unity-cli.md`에 기록하고 `Pitfalls/pipeline-command-drift.md`·`unity-test-editor-open.md`를 손봄(사용자 지시로 이 Task 브랜치에서 수정)
- 대기 중인 결정: 래그돌 물리 결과까지 모두에게 같게 맞출지(기본: 상태만 공유, 연출은 각자)
- 공용 파일 요청: 없음

## Verification

- 구역 검사: PASS (10-02)
- 1 문서/규칙: PASS (10-02)
- 2 컴파일: PASS (10-02)
- 3 테스트: NO_PROJECT_TESTS (10-02) — 열린 에디터 list_tests 0개. check-work는 FAIL 표시: `unity test`가 열린 에디터가 있으면 거부(`Pitfalls/unity-test-editor-open.md`)
- 4 실행 로그: SKIPPED(미실행)
- 5 공동 테스트: 병합 전 사전 확인 — 단계별 MPPM 2명 확인(①~⑤·#9·#10, 10-02). 공동 테스트 항목 1·2·3 모두 통과(작업자 확인 10-02, MPPM). 병합 후 공동 테스트 통과(2026-10-02, QA #13, 여러 PC·팀원 함께)
