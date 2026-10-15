---
name: scaffold-task
description: PM 전용. 통합에서 번호·Task 폴더를 발급하고 작업 구역/검증을 준비한다. 준비 브랜치와 실제 배정·인계는 구분한다.
---

# Scaffold Task

권한: PM만. Task 하나 = 담당자 한 명 = 공동 테스트 한 묶음.

1. 소속 integration에서 최신화한다. 다른 세션 폴더를 직접 전환/편집하지 않는다.
2. 종류·slug·제목·담당을 확인한다. 연결은 별도 Task, 확정된 단계 구성은 재승인받지 않는다.
3. `pwsh -File Scripts/new-task.ps1 -Type <종류> -Slug <slug> -Title "<제목>" -Assignee <계정> -WhatIf`
4. 승인 범위에서 실실행한다. 전체 ref 번호 검사·잠금 발급으로 Task 6문서와 Tasks 표를 만들고 **scaffolded** 유지. 임의 번호 금지.
5. Unity 구역/빈 테스트 씬은 Editor에서 만들고 `.meta`·빈 폴더 `.gitkeep`를 준비한다. 기능은 미리 구현하지 않는다.
6. `setup.md`에 실제 Jira URL·작업 구역·공용 소유, plan에 공동 테스트 최대 3항목을 기록한다. 테스트 씬/인원/호스트·참가 역할/재현/예상과 실제 기록 방법 명시.
7. 순차 준비라면 후속 Task는 **예약 구역만** 작성하고 Allowed/공용 파일은 비운다. meta `Previous Task`, 통합 `Active Task`로 한 단계만 활성화한다.
8. PM 승인 준비 커밋에서 여러 작업 브랜치를 미리 만들 수 있다. 통합 `## 준비 브랜치`에 `Task | 브랜치 | 준비 기준 SHA | 승인 SHA` 4열(전체 40자리), 기존 Tasks 6열 유지. 브랜치 존재는 배정·실제 인계가 아니다.
9. 다음은 `handoff-to-member`. 다음 단계에는 앞 integrated·실공동 검증·소유 반환·실 Jira URL이 필요하다.
