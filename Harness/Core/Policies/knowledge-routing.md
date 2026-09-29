# Knowledge Routing

배운 내용을 Task 로그 속에 묻어 두지 않는다. 함정·결정·사실은 **상황 하나당 파일 하나**로 나눠서, AI가 지금 상황에 맞는 파일만 열도록 한다.

## 종류

| 종류 | 폴더 | 의미 | 예 |
|---|---|---|---|
| 함정 | `Pitfalls/` | 한 번 겪은 착각·환경 문제와 대처법 | 재컴파일 후 옛 값이 남음 |
| 결정 | `Decisions/` | 사람이 확정한 선택(기술 선택 포함) | Unity 버전, 입력 방식 |
| 사실 | `Facts/` | 확인 가능한 현재 상태 | 기본 브랜치, 대표 씬 경로 |

## 층 선택

- 어느 Unity 프로젝트에서도 통하는 내용 → `Harness/Engine/Unity/{Pitfalls,Decisions,Facts}/`
- 이 POC에만 해당하는 내용 → `Harness/Project/{Pitfalls,Decisions,Facts}/`
- 엔진과 무관한 작업 방식 자체 → 지식 파일이 아니라 `Harness/Core/Policies/`의 정책 개정(사람 승인 필요)

판단이 애매하면 Project에 먼저 적고, 다른 프로젝트에서도 반복되면 Engine으로 옮긴다.

## 파일 규칙

- 파일명은 "언제 읽어야 하는지"가 드러나는 소문자 kebab-case 주제명이다.
  - 예: `Harness/Engine/Unity/Decisions/version.md`, `Harness/Project/Pitfalls/shell-rosetta-brew.md`
- 같은 주제는 같은 파일에 모은다(새 파일을 만들기 전에 `index.md`에서 기존 주제를 찾는다).
- 양식은 `Harness/Core/Templates/Knowledge/`의 `pitfall.md`, `decision.md`, `fact.md`를 쓴다.
- 파일 1개는 `context-budget.md`의 `knowledge` 기준을 넘지 않는다. 넘으면 주제를 쪼갠다.
- 한 폴더에 파일이 15개를 넘으면 상황별 하위 폴더를 만든다(첫 글자만 대문자, 예: `Pitfalls/Editor/`). 하위 폴더에도 `index.md`를 둔다.

## index.md

모든 지식 폴더에는 `index.md`가 있고, 파일마다 1줄씩 적는다.

```
- [version.md](version.md) — Unity·URP·Pipeline 버전 고정. 패키지 업그레이드 전에 읽기
```

- 설명은 "무엇"보다 "언제 읽는지"를 적는다.
- `Scripts/verify-knowledge.ps1`이 index와 실제 파일이 일치하는지, frontmatter가 있는지 검사한다.

## 상태 변경

- 더 이상 맞지 않는 결정·사실은 삭제하지 않고 `status: superseded`와 대체 파일 경로를 적는다.
- 결정(`Decisions/`)은 사람이 확정한 것만 적는다. AI 추천은 Task의 Open Decisions에 둔다.
