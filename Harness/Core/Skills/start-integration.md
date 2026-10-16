---
name: start-integration
description: PM 전용. 최신 원격 Base(기본 dev 또는 선언된 부모 integration)에서 통합 브랜치·기록 폴더를 만든다.
---

# Start Integration

권한: PM만 (`team-roles.md`). 작업자는 PM 요청이 필요하다.

1. 기능 slug·이름·부모 Base를 확인한다. 사람이 이미 확정한 값은 다시 묻지 않는다.
2. 미리보기: `pwsh -File Scripts/new-integration.ps1 -Slug <slug> -Feature "<기능>" [-Base integration/<부모>] -WhatIf`
3. 승인 범위에서 `-WhatIf` 없이 실행한다.
   - Base 기본 dev, 부모 통합이면 원격 존재·자기 메타 Branch/Base 계보·누락/순환을 검사한다.
   - 최신 원격 Base에서 브랜치 생성·이동, `Integrations/Active/Integration-<slug>/` meta/checklist/result 생성·준비 커밋/push.
4. 목표·담당·시각을 실제 결정대로 적는다. 일정 미정이면 미정으로 둔다.
5. 순차 통합은 선택 `Active Task`를 명시하고 `scaffold-task`로 단계별 준비한다. 비어 있음/없음은 기존 동작이다.
6. 자식은 메타 Base로만 Merge commit 제출한다. 기존 `close-integration`은 Base=dev 전용이므로 자식에 실행하지 않는다.
