---
summary: (해결됨 2026-10-14) batch unity test는 같은 프로젝트를 연 에디터가 있으면 거부돼 check-work 테스트 단계가 항상 FAIL이었음 → verify-unity가 열린 에디터의 Pipeline run_tests(비동기)+test_status로 실행
status: active
updated: 2026-10-28
source: Task-20260930-003, Task-20261014-036, Task-20261027-076
---

# 에디터가 열려 있으면 unity test가 거부됨

## 언제 읽나

`check-work.ps1` 또는 `verify-unity.ps1 -RunTests`의 테스트 단계가 실패하거나, 테스트 실행 방식을 바꿀 때.

## 증상 (수정 전)

- check-work: `3 테스트 FAIL — 테스트 결과 없음`
- 세부 출력: `It looks like another Unity instance is running with this project open.` / `TEST_RUN_FAILED: 결과 파일이 생성되지 않음 (exit 6)`
- 에디터를 닫으면 그 앞의 에디터 연결 확인에서 `ENV_BLOCKED`로 멈춰, 어느 상태에서도 통과할 수 없었다.

## 원인

`unity test`는 열린 에디터를 쓰지 않고 batch 에디터를 새로 띄운다. 한 프로젝트는 에디터 하나만 열 수 있어 거부된다. 열린 에디터를 쓰게 하는 옵션이 없다.

## 대처 (2026-10-14 적용)

- `verify-unity.ps1`이 열린 에디터의 Pipeline `run_tests --mode <M> --async_tests true`로 시작하고 `test_status`가 `completed`가 될 때까지 기다린다. 시작 전에 `Temp/pipeline_test_status.json`을 지워 이전 결과를 읽지 않게 한다.
- PlayMode는 동기 실행이 거부된다(도메인 리로드로 요청이 끊김). 그래서 두 모드 모두 비동기로 실행한다. 리로드 중 연결이 끊기면 상태 확인을 계속한다.
- `run_tests` 응답은 바깥 `success: true`여도 안쪽 결과의 `success: false`·`error`가 있을 수 있다. 안쪽 값을 본다.
- 출력 형식은 그대로: `Tests(<M>): NO_PROJECT_TESTS` / `Tests(<M>): total, passed, failed, skipped`. 결과는 `.harness/local/test-results-<mode>.json`.

## 확인 방법

에디터를 연 상태에서 `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -RunTests -Mode EditMode`(·`PlayMode`). 테스트 0개면 `NO_PROJECT_TESTS`, 임시 실패 테스트를 넣으면 `failed 1`과 exit 1(2026-10-14 확인 후 삭제).

## 필터 주의

Pipeline 0.8.0-exp.1의 `-Filter 'FixtureA|FixtureB'`는 OR 정규식으로 처리되지 않았다(Task-076). 실제 존재하는 fixture를 합쳐도 `NO_PROJECT_TESTS`가 나왔고 개별 이름으로 실행하면 테스트가 선택됐다. 여러 묶음은 개별 필터로 **순차 실행**한다. 종료0이어도 total0은 통과가 아니다.
