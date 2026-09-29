---
name: record-knowledge
description: 알게 된 함정·결정·사실을 상황별 파일 1개로 알맞은 폴더에 기록하고 index.md를 갱신한다. Harness 수정이므로 PM만 직접 기록한다.
---

# Record Knowledge

`Harness/Core/Policies/knowledge-routing.md`를 따른다.

## 작업자일 때

`Harness/`는 PM만 수정한다. 작업자는 직접 기록하지 않고, Task `handoff.md`의 "PM이 확인할 것"에 한 줄로 남긴다.
예: `- 교훈 제안: 재컴파일 직후 Inspector 값이 옛 값으로 보임 (함정)`. PM이 통합 마무리 때 모아서 기록한다.

## PM일 때

1. 종류를 고른다: 함정(`Pitfalls/`) / 결정(`Decisions/`, 사람이 확정한 것만) / 사실(`Facts/`).
2. 층을 고른다: 어느 Unity 프로젝트든 해당 → `Harness/Engine/Unity/`, 이 POC만 → `Harness/Project/`.
3. 대상 폴더의 `index.md`에서 같은 주제 파일이 있는지 찾는다.
   - 있으면 그 파일에 추가·수정한다.
   - 없으면 `Harness/Core/Templates/Knowledge/<종류>.md`를 복사해 "언제 읽는지" 드러나는 소문자 kebab-case 이름으로 만든다.
4. frontmatter의 `summary`, `updated`, `source`(Task ID 또는 `human`)를 채운다.
5. `index.md`에 1줄을 추가한다: `- [file.md](file.md) — 요약. 언제 읽는지`
6. 더 이상 맞지 않는 항목은 삭제하지 말고 `status: superseded`와 대체 경로를 적는다.
7. `pwsh -File Scripts/verify-knowledge.ps1`로 확인한다.
