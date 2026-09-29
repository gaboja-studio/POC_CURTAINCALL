---
name: integrate-editor
description: 승인된 씬/프리팹/에셋 변경을 격리된 worktree의 Unity 에디터 조작으로 반영한다.
---

# Integrate Unity Editor

1. `Harness/Engine/Unity/Policies/workspace-isolation.md`, `asset-ownership.md`, `editor-targeting.md`를 확인한다.
2. 격리된 Task worktree와 명시적 Exclusive Assets(또는 AI Setup Allowed)가 있어야 한다.
3. `Harness/Engine/Unity/Pitfalls/index.md`를 훑고 해당 함정 파일만 읽는다.
4. `unity status --project-path <p>`로 대상 에디터가 준비됐고 컴파일 중이 아닌지 확인한다.
5. `unity command --project-path <p> --query <키워드>`로 사용 가능한 명령을 찾는다. 이름을 가정하지 않는다.
6. 수정 전 하이어라키/컴포넌트를 조회한다.
7. 승인된 변경만 적용하고 소유한 에셋만 저장한다. 여러 설정을 한 번에 적용하는 명령이 실패하면 하나씩 나눠 실행한다.
8. 다시 조회해 결과를 확인하고, `git status`로 실제 저장된 파일을 확인한다.
9. 컴파일하고 해당 테스트를 돌린다(`verify-unity`).
10. 변경된 직렬화 파일과 저장/clean 상태를 handoff에 적는다.
