---
name: sync-integration
description: 필요한 출발·대상 브랜치끼리 직접 변경을 받거나 선택 동기화 PR을 만든다.
---

# sync-integration

1. 사용자가 지정한 출발·받는 브랜치와 기존 변경을 확인한다. 부모 자식 계층·PM 전용 제한은 없다.
2. 현재 작업 브랜치로 직접 fetch/merge할 수 있다. 미저장 변경은 보존하고 dev는 PR로만 바꾼다.
3. 선택 PR은 `pwsh -File Scripts/sync-integration.ps1 -Parent <출발> -Child <대상> [-DryRun]`.
4. 실제 생성 시 방향·상태·커밋을 재확인한다. 승인된 공유 요청을 단계마다 다시 묻지 않는다.
5. 자동 병합은 하지 않는다. PR 생성과 병합 권한은 사용자의 요청 범위를 따른다.
