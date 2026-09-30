---
name: scaffold-task
description: PM 전용. 통합 브랜치 위에 Task 폴더를 만들고 번호를 발급한다. 테스트 씬·스크립트 폴더는 PM이 Unity에서 만들고 setup.md에 기록한다.
---

# Scaffold Task

권한: PM만. Task 하나 = 담당자 한 명 = 공동 테스트 한 묶음. 크면 여러 Task로 나눠 제안한다.

1. 현재 브랜치가 해당 `integration/<slug>`인지 확인한다. 아니면 `git switch integration/<slug>` 후 최신화한다.
2. 종류(feat/fix/refactor), slug, 제목, 담당자(GitHub 계정)를 확인한다. 기능 연결 작업이면 "연결 Task"로 만든다.
3. 미리보기: `pwsh -File Scripts/new-task.ps1 -Type <종류> -Slug <slug> -Title "<제목>" -Assignee <@계정> -WhatIf`
4. 확인 후 `-WhatIf` 없이 실행한다. 번호는 스크립트가 원격 브랜치까지 확인해 발급한다(손으로 고르지 않는다).
   - Task 폴더 생성, Integration `meta.md`의 Tasks 표에 한 줄 추가. 브랜치는 인계 때 만든다.
5. PM에게 Unity에서 할 일을 안내한다:
   - 테스트 씬: `Assets/Scenes/Tests/<Feature>/` / 스크립트 폴더: `Assets/Scripts/<Feature>/` (빈 폴더엔 `.gitkeep`)
   - Unity에서 저장해 `.meta`가 생기게 한다.
6. PM이 알려 준 경로와 JIRA 번호로 `setup.md`를 채운다(경로는 백틱, 폴더는 `/`로 끝냄). 공용 파일이 필요하면 Integration 소유 표에 배정하고 `setup.md`에도 적는다.
7. `plan-task`로 공동 테스트 항목을 합의한다.
8. 다음 단계: `handoff-to-member`. (여러 Task를 준비한 뒤 한 번에 인계해도 된다)
