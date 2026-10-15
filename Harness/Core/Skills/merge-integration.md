---
name: merge-integration
description: PM 전용. 작업 PR만 순차 Squash 병합하고 실제 공동 테스트를 기록한다. 통합·동기화 PR은 메타 Base의 Merge commit 절차로 구분한다.
---

# Merge Integration

권한: PM만. `git-workflow.md`·필수 리뷰/보호 규칙을 따른다.

1. `pwsh -File Scripts/merge-integration.ps1 -Slug <slug>`로 PR·검사·미제출 Task를 확인한다.
2. 승인된 병합 묶음/순서를 checklist에 기록한다(공용 소유→의존→나머지). 순차 통합의 후속 scaffolded 예약 Task는 이번 활성 단계 묶음에 포함하지 않는다.
3. **작업 head만** `-Merge <PR번호>`로 Squash. 실제 head가 dev/integration이면 스크립트가 차단하므로 정식 Merge commit 절차를 사용한다.
   - 통합→자기 메타 Base, 선언된 부모→자식, 소속 통합→등록된 준비 Task branch만 허용. 임의 방향·자식 dev 직행 금지.
   - 필수 리뷰/권한 차단은 다른 도구·admin·peer로 우회하지 않는다.
4. 뒤처진 작업 PR은 `-Update <번호>`로 최신화. 충돌은 담당/PM과 해결, 씬·프리팹 YAML은 AI가 해결하지 않는다.
5. 통합을 받아 컴파일·자동 테스트, 실제 공동 테스트를 수행한다. 결과대로 PR 병합/공동 테스트 todo 체크·Status integrated를 기록한다. 미실행을 PASS로 바꾸지 않는다.
6. 순차 다음 단계 근거: handoff Verification `- 5 공동 테스트: PASS — 근거`와 `## 공동 테스트 결과`의 일시/참여자/재현/예상/실제 실행 기록. 앞 구역 반환·실 Jira URL 확보 후 다음 한 Task만 배정한다.
7. 실패는 report-issue/후속 Task, 공용 연결이 필요하면 별도 연결 Task/소유 배정.
8. Base=dev 통합만 `close-integration`로 마감한다. 자식 통합은 이번에 기존 dev 마감 도구로 닫지 않는다.
