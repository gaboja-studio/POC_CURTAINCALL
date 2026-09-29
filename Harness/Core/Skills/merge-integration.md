---
name: merge-integration
description: PM 전용. 통합 브랜치로 온 PR을 정해진 순서로 하나씩 Squash 병합하고, 뒤처진 PR을 최신화한 뒤 공동 테스트를 진행한다.
---

# Merge Integration

권한: PM만. 규칙: `Harness/Core/Policies/git-workflow.md`

1. 현황: `pwsh -File Scripts/merge-integration.ps1 -Slug <slug>`
   - PR 목록, 검사 통과 여부, 병합 가능 상태, 제출하지 않은 Task를 보여 준다.
2. 모든 Task가 제출되었는지 확인한다. 안 된 Task가 있으면 PM에게 기다릴지, 이번 묶음에서 뺄지 묻는다.
3. 병합 순서를 `checklist.md`에 적고 PM에게 확인받는다(공용 파일 소유 Task → 의존 대상 → 나머지).
4. 하나씩: `pwsh -File Scripts/merge-integration.ps1 -Slug <slug> -Merge <PR번호>` (Squash)
   - 스크립트가 뒤처진 다른 PR을 알려 준다 → `-Update <PR번호>`로 최신화한다.
   - 충돌이 나면 멈추고 해당 담당자와 PM이 함께 해결한다. 씬·프리팹 충돌은 AI가 해결하지 않는다.
5. 모두 병합하면 통합 브랜치를 받아(`git pull`) 컴파일·테스트(`check-work` 2·3단계)를 한다.
6. 공동 테스트: `checklist.md` 표의 항목(각 Task의 공동 테스트 항목)을 함께 확인하고 결과를 적는다. 통과한 Task의 meta Status를 `integrated`로 바꾼다.
7. 실패 항목은 `report-issue`로 버그 이슈를 만들고 후속 Task로 넘긴다.
8. 기능 연결 작업이 필요하면 연결 Task를 만든다(`scaffold-task`).
9. 다음 단계: `close-integration`.
