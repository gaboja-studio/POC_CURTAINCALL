---
name: close-integration
description: PM 전용. 공동 테스트가 끝난 통합을 마무리한다. Task를 Done으로 옮기고 이슈 번호를 모아 dev로 PR, 병합 후 브랜치를 정리한다.
---

# Close Integration

권한: PM만.

1. 모든 Task가 `integrated`인지, `result.md`의 남은 이슈가 후속 Task로 넘어갔는지 확인한다.
2. **테스트 씬 처리**: 각 Task `setup.md`의 "테스트 씬 처리"가 `미정`이면 PM에게 유지/삭제를 묻고 기록한다. 삭제는 PM이 Unity에서 직접 한다(스크립트는 씬을 지우지 않음).
3. 작업자가 handoff에 남긴 "교훈 제안"을 모아 `record-knowledge`로 기록한다.
4. 미리보기: `pwsh -File Scripts/close-integration.ps1 -Slug <slug> -WhatIf`
   - 확인하는 것: 테스트 씬 결정 기록, 모든 Task PR 병합 여부.
   - 하는 일: Task 폴더를 `Tasks/Done/`으로(Status `done`), 통합 기록을 `Integrations/Done/`으로 옮기고 커밋·push, 병합된 PR 본문에서 `closed #번호`를 모음.
5. 확인 후 실행하고, 출력된 이슈 번호로 dev PR을 만든다:
   `pwsh -File Scripts/new-pr.ps1 -Base dev -Title "<기능 묶음 이름>" -Summary "<요약>" -Issues <번호들> -DryRun` → 확인 → `-DryRun` 없이.
6. dev PR은 **Merge commit**으로 병합한다(GitHub에서 PM이 확인 후).
7. 병합 후 정리: `pwsh -File Scripts/close-integration.ps1 -Slug <slug> -Cleanup -WhatIf` → 확인 → 실행.
   - dev에 반영된 것을 확인한 뒤 통합 브랜치와 병합된 작업 브랜치를 원격에서 삭제한다.
