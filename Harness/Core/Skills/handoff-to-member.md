---
name: handoff-to-member
description: PM 전용. 인계 전 점검(setup.md 체크리스트·경로·.meta)을 하고, 통합 브랜치에 준비 내용을 올린 뒤 담당자의 작업 브랜치를 만든다.
---

# Handoff To Member

권한: PM만.

1. 현재 브랜치가 해당 `integration/<slug>`인지 확인한다.
2. 점검 미리보기: `pwsh -File Scripts/assign-task.ps1 -Id <Task-ID> -WhatIf`
   - 스크립트가 확인하는 것: `setup.md` 인계 체크리스트가 모두 체크됨, 작업 구역 경로가 실제로 있음, `.meta`가 있음, JIRA 칸이 채워짐, `plan.md` 공동 테스트 항목이 있음.
3. `[문제]`가 있으면 PM에게 무엇을 해야 하는지 쉬운 말로 알린다(예: "스크립트 폴더에 .meta가 없어요. Unity에서 저장해 주세요").
4. 문제가 없으면 `-WhatIf` 없이 실행한다. 스크립트가 하는 일:
   - Status를 `assigned`로 바꾸고 준비 내용을 커밋해 통합 브랜치에 올림
   - 그 시점에서 작업 브랜치(`<종류>/YYYYMMDD-NNN-slug`)를 만들어 원격에 올림
   - Integration Tasks 표의 브랜치·상태 칸 갱신
5. 담당자에게 보낼 메시지를 만들어 준다:
   > "○○님, Task-YYYYMMDD-NNN(제목) 맡아 주세요. AI에게 'Task-YYYYMMDD-NNN 시작해줘'라고 하시면 됩니다."
