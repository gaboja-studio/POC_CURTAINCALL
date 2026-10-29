---
summary: 작업 브랜치 유지, integration 선택, 필요한 브랜치끼리 직접 공유, dev PR 검토 유지
status: active
updated: 2026-10-27
source: human (하네스 간소화 승인)
---

# 브랜치 전략

- 작업별 feat/fix/refactor/resource 이름과 선택 integration을 유지한다.
- feat→integration→dev 및 dev→integration→feat 순서는 강제하지 않는다. 필요한 브랜치끼리 직접 변경을 받는다.
- 작업·통합 공유는 PR 필수가 아니다. dev 반영은 PR 1회와 지정 팀원 검토를 유지한다.
- builds는 dev에서만 받는다. 커밋·push·병합은 사용자가 요청한 범위에서 진행한다.
- 예전 공급 브랜치·부모 자식 예외는 더 이상 필요 없다. 진행 중인 변경·참조는 보존한다.
- 상세 기준: `Harness/Core/Policies/git-workflow.md`와 `harness-simplification.md`.
