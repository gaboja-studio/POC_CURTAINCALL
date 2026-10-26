---
name: start-integration
description: 필요할 때만 선택 통합 브랜치를 만든다. PM이나 부모 자식 계층은 요구하지 않는다.
---

# start-integration

1. 통합 브랜치가 실제로 필요한지 요청 목적에서 판단한다. 작업의 필수 중간 단계로 만들지 않는다.
2. `pwsh -File Scripts/new-integration.ps1 -Slug <slug> -Feature "<목적>" [-Base <기준>] -WhatIf`.
3. 요청 범위에서 생성한다. 이 도구는 commit/push하지 않는다. 저장 요청은 save-work로 따로 처리한다.
