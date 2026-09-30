# Plan

## Context

현재 생성된 Unity Assets 폴더부터 역할과 규칙을 문서로 정리하고, 폴더가 추가·변경될 때 언제든 재점검하고 갱신할 절차를 마련한다.

## Approach

1. setup.md 문서 작업 공간 확정 후 Assets의 실제 폴더·파일 구조를 읽기 전용으로 조사한다.
2. 현존 폴더별 경로·역할·배치 기준·이름 규칙·예외를 기록한다.
3. Resources 특성, Unity .meta/GUID 보존 및 씬·프리팹 참조 주의사항을 기존 엔진 정책과 대조한다.
4. 점검 계기·조사 방법·규칙 대조·예외 승인·문서 갱신·검증·변경 기록 절차를 체크리스트로 작성한다.
5. 공동 리뷰 후 integration/document-project로 제출한다. 물리적 정리가 필요하면 변경 목록을 제안하고 별도 승인을 받는다.

## Files

- 수정 예정: setup.md에 PM이 지정한 규칙 문서 및 이 Task 문서.
- 읽기 전용 참고: Assets/, Harness/Engine/Unity/Policies/code-folders.md 및 asset-ownership.md.
- 프로젝트 overview는 Task-20260930-001의 소유이며 중복 작성하지 않는다.

## Acceptance Criteria

- 현재 폴더의 경로·역할·규칙·예외가 실제 구조와 일치한다.
- 마지막 점검 기준과 규칙 변경 기록 방식이 명시된다.
- 담당자가 재점검 체크리스트를 따라 차이를 발견하고 문서를 갱신할 수 있다.
- .meta/GUID·참조 보존과 예외 승인 기준이 포함된다.
- setup.md 범위 밖 변경 및 승인 없는 Assets 이동·삭제가 없다.

## Dependencies

- 선행 Task 없음. Task-20260930-001과 독립 구현 가능.
- 공용 링크 파일은 PM 배정 후 수정하며 두 Task가 동시에 소유하지 않는다.

## Risks

- 실제 구조와 문서의 불일치, Resources 동작을 일반 폴더와 혼동, 물리적 이동 시 참조 손상.

## Auto Verification

- 구역 검사: 작업 공간 확정 후 Scripts/verify-scope.ps1.
- 1 문서/규칙: Scripts/verify-fast.ps1 및 실제 Assets 트리와 문서 대조.
- 2 컴파일: 문서만 변경하면 생략하고 사유 기록. 승인된 Unity 변경 발생 시 별도 검증.
- 3 테스트: 문서 작업의 Unity 테스트는 해당 없음. 미실행을 PASS로 표기하지 않는다.
- 4 실행 로그: 재점검 수행 결과와 발견한 차이를 handoff.md에 기록.

## 공동 테스트 항목

1. 현재 Assets 폴더를 문서와 대조하여 누락·잘못된 역할·예외가 없는지 확인한다.
2. 재점검 체크리스트를 읽기 전용으로 수행해 갱신 필요 여부를 판단할 수 있는지 확인한다.
3. Unity 참조 보존 지침과 overview 문서의 책임 분리가 일관적인지 확인한다.
