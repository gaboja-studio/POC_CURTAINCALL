---
name: scaffold-task
description: 목적·담당·작업 브랜치를 적은 Task 3문서를 만든다. PM 준비·인계 gate는 없다.
---

# scaffold-task

1. 요청 목적·담당·종류·짧은 slug를 확인한다. 현재 작업과 관련 없으면 기존 미저장 변경을 보존한다.
2. `pwsh -File Scripts/new-task.ps1 -Type <feat|fix|refactor> -Slug <slug> -Title "<목적>" -Assignee <계정> [-Base <공유 대상>] -WhatIf`로 번호·경로를 확인한다.
3. 요청 범위에서 실행한다. 현재 별도 작업 브랜치를 쓰려면 `-UseCurrentBranch`. 원격 ref 갱신이 필요하면 `-Fetch`.
4. plan에는 Context만 간단히 적는다. 상세 계획·todo는 작업자가 조사하며 갱신한다. 테스트 씬·폴더를 PM이 미리 만들 필요는 없다.
5. 착수는 `start-work`. Jira·구역 배정·인계·공동 테스트 계획을 기다리지 않는다.
6. commit/push는 저장·제출 요청이 있을 때만 한다.
