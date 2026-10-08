---
summary: unity-cli beta.11~12의 unity test는 별도 배치 에디터를 띄우므로, 같은 프로젝트가 에디터에 열려 있으면 실행을 거부해 check-work 테스트 단계가 FAIL로 찍힘
status: active
updated: 2026-10-02
source: Task-20260930-003
---

# 에디터가 열려 있으면 unity test가 거부됨

## 언제 읽나

`check-work.ps1` 또는 `verify-unity.ps1 -RunTests`의 테스트 단계가 "테스트 결과 없음"·`TEST_RUN_FAILED`로 실패할 때.

## 증상

- check-work: `3 테스트 FAIL — 테스트 결과 없음`
- 세부 출력: `The project at "<경로>" is already open in a running Editor (PID ...). Close it and run the command again.`
- 컴파일 단계는 PASS인데 테스트만 실패한다.

## 원인

unity-cli `1.0.0-beta.11`~`beta.12`의 `unity test`는 열린 에디터를 쓰지 않고 테스트용 배치 에디터를 새로 띄운다. 한 프로젝트는 에디터 하나만 열 수 있어서, 작업 중인 에디터가 있으면 거부한다. 열린 에디터를 쓰게 하는 옵션도 없다(`unity test --help` 확인).

## 대처

1. 코드 문제가 아니므로 고치려 하지 않는다.
2. 테스트 수만 확인하면 될 때: 열린 에디터의 Pipeline `list_tests`로 개수를 본다. 0개면 `NO_PROJECT_TESTS`로 기록하고, check-work가 FAIL로 표시한 사유를 handoff에 함께 적는다.
3. 실제 테스트를 돌려야 할 때: 사람이 에디터를 닫은 뒤 다시 검사한다. AI가 `unity close`로 닫지 않는다(저장 없이 종료).
4. 스크립트를 고치려면(열린 에디터면 Pipeline 테스트 명령으로 대체 등) PM이 하네스 작업으로 진행한다.

## 확인 방법

`unity status --json --project-path <p>`의 `instances`가 비어 있는지 본 뒤 `pwsh -File Scripts/check-work.ps1`을 다시 실행한다.
