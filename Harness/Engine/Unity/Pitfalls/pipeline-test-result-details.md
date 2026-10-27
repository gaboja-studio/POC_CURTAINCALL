---
summary: Pipeline 테스트 합계와 results 상세 개수가 다를 때 Test Runner 창에서 실패 leaf를 확인한다
status: active
updated: 2026-10-28
source: Task-20261027-076 전체 EditMode 실행 및 Pipeline 0.8.0-exp.1 조사
---

# Pipeline 테스트 상세 누락

## 언제 읽나

`run_tests`·`test_status`의 합계에는 실패가 있지만 `results`에는 실패 이름이 없거나 전체 건수보다 적게 반환될 때.

## 확인된 현상

- Task-076 전체 EditMode는 1,049건·실패15~16건인데 `results`는118건만 반환했고 모두 통과였다. 상세 배열만 보고 전체 통과로 판단하면 안 된다.
- Pipeline `TestResultCollector.RunFinished`는 누적 `Results`가 비어 있을 때만 전체 `RootResult`의 leaf를 다시 수집한다. 실행 후에는 collector가 해제되어 static 필드에서 결과를 다시 읽을 수 없다.
- 누적 상세가 일부만 남는 정확한 리로드 시점·원인은 확정하지 않았다. 패키지 자체 테스트가 원인이라고 단정하거나 패키지 코드를 임의 수정하지 않는다.

## 대처

1. 합계·상세 건수를 함께 확인하고, 상세 미확보는 그대로 기록한다.
2. Unity Test Runner 창을 연 상태로 필요한 범위의 테스트를 다시 실행한다.
3. 창의 실패 목록을 확인한다. 자동 수집이 필요하면 `TestRunnerWindow.m_TestListGUIs` → 각 GUI의 `newResultList`에서 `resultStatus == Failed && !isSuite`인 `TestRunnerResult`의 `fullName`·`messages`·`stacktrace`를 읽을 수 있다.
4. 위 내부 필드·프로퍼티는 Test Framework 버전 의존이다. 없으면 실패를 숨기지 말고 현재 버전의 UI/API를 확인한다.

## 검증 범위

Task-076에서 Test Runner 창을 연 뒤 재실행해 실제 실패16건의 이름·메시지·소스 위치를 확보했다. 임시 `ResultsSavingCallbacks` 등록은 XML 파일을 만들지 못했으므로 검증된 대안으로 취급하지 않는다.
