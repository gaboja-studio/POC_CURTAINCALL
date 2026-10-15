---
name: handoff-to-member
description: PM 전용. 기술 배정 준비와 실제 전달을 분리한다. 승인된 준비 브랜치는 배정 문서 동기화 PR으로 내려보낸다.
---

# Handoff To Member

권한: PM만. 기술 배정 준비 ≠ 실제 전달 ≠ 구현 시작.

1. 소속 integration인지 확인, `pwsh -File Scripts/assign-task.ps1 -Id <Task-ID> [-UsePreparedBranch] -WhatIf`.
2. setup 인계 전 기술 체크·실제 경로/.meta·Jira·plan 공동 테스트를 확인한다. 문제가 있으면 해결하고 진행한다.
3. 순차 통합이면 최신 Active Task 일치·실 Jira URL을 검사한다. Previous Task는 integrated와 handoff **실제** 공동 검증을 요구한다:
   - Verification: `- 5 공동 테스트: PASS — 근거`
   - `## 공동 테스트 결과`: `- 일시:`, `- 참여자:`, `- 재현:`, `- 예상:`, `- 실제:`에 실행 기록. 계획·미실행·짧은 PASS는 근거가 아니다.
   - 앞 Task Allowed/공용 소유를 반환하고 이력 절로 보존, 다음 한 Task만 배정한다.
4. 미리 준비한 브랜치는 `-UsePreparedBranch`로 동일 Task/Type/Integration·준비 기준/승인 SHA·부모 이력을 검사한다. 독립 구현 커밋이면 멈춘다.
5. 실실행하면 assigned·기술 점검만 체크, 준비 커밋/push·Tasks 표 갱신. 신규 작업 브랜치는 생성/push, 기존 준비 브랜치는 **소속 통합→그 브랜치 동기화 PR**으로 배정 문서 전달 대기한다. 자동 merge/force 재생성 없음.
6. 정상 Merge commit·배정 문서 유입을 확인한 뒤 보낼 안내를 만든다: “Task-ID(제목), 브랜치·구역·검증·시작 조건을 확인해 주세요.”
7. PM이 **실제 전달했다고 확인한 해당 Task만** `담당자에게 실제 전달 — PM 확인` todo를 체크한다. URL 연결/기술 준비만으로 체크하지 않고, 옛 합쳐진 인계 항목도 자동 완료 처리하지 않는다.
8. 작업자는 assigned/working·최신 배정/Active Task·실전달이 확인된 뒤 `start-work`. 후속 scaffolded는 시작하지 않는다.
