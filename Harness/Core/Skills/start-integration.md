---
name: start-integration
description: PM 전용. 기능 묶음 통합 브랜치(integration/*)와 통합 기록 폴더를 dev에서 만든다.
---

# Start Integration

권한: PM만 (`team-roles.md`). 작업자가 요청하면 PM 권한이 필요하다고 알린다.

1. 기능 묶음 이름을 소문자 kebab-case slug로 정한다(예: 커튼콜 연출 → `curtain-call`). 사용자에게 확인받는다.
2. 미리보기: `pwsh -File Scripts/new-integration.ps1 -Slug <slug> -Feature "<기능 묶음 이름>" -WhatIf`
3. 확인 후 `-WhatIf` 없이 실행한다. 스크립트가 하는 일:
   - 원격 최신 `dev`에서 `integration/<slug>` 브랜치 생성 후 이동
   - `Integrations/Active/Integration-<slug>/`에 meta·checklist·result 생성
   - 커밋 후 원격에 올림(PM은 브랜치 보호 우회 목록에 있어야 함)
4. `meta.md`의 목표와 병합 예정 시각을 PM에게 물어 채운다.
5. 다음 단계 안내: "이 기능 묶음에 들어갈 Task를 만들까요?" → `scaffold-task`
