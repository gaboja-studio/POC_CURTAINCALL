---
name: close-integration
description: 완료로 확인한 Task와 선택 통합 기록만 Done으로 옮긴다. 자동 삭제·커밋은 없다.
---

# close-integration

1. 사용자와 실제 완료를 확인한 Task만 고른다. 상태·공동 테스트·인계 표는 요구하지 않는다.
2. `pwsh -File Scripts/close-integration.ps1 -Slug <slug> [-CompletedTasks <Task-ID들>] -WhatIf`로 이동 목록을 본다.
3. 원문·씬·에셋을 보존하며 Done으로 옮긴다. 완료하지 않은 Task를 자동으로 닫지 않는다.
4. commit/push는 별도 저장 요청 때 한다. 브랜치·worktree 자동 삭제는 하지 않는다.
