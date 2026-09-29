# Context Budget (문서 크기 정책)

문서 크기 숫자는 **이 파일 한 곳에만** 적는다. `Scripts/verify-context.ps1`이 아래 표를 직접 읽어 검사한다(표 형식을 바꾸면 스크립트도 확인).
문서가 길수록 AI가 읽는 비용(토큰)과 시간이 늘고 중요한 내용을 놓친다. 저장된 정보는 늘어나도 되지만, **기본으로 읽는 정보는 작게** 유지한다.

## 기본 읽기(Default-read) 범위

1. `AGENTS.md`
2. 현재 Task의 `handoff.md`, `meta.md`, `setup.md`, `todo.md`, `plan.md`
3. Domain Map 1개, 현재 단계 Skill 1개
4. 필요한 지식 폴더의 `index.md` + 파일 1개

기본으로 읽지 않는 것: `log.md`, 모든 `History/`, 전체 정책·Skill·Domain Map, `Docs/References/`.

## 크기 기준 (단위: 줄)

- `warn`: 경고. 다음 저장 전에 줄인다.
- `hard`: 차단. `verify-context.ps1`이 실패하며(커밋 전 `verify-fast`, PR의 GitHub 검사 포함) 줄이기 전에는 진행하지 않는다.

| Target | Warn | Hard |
|---|---|---|
| `AGENTS.md` | 60 | 80 |
| `task/meta.md` | 40 | 50 |
| `task/setup.md` | 70 | 90 |
| `task/plan.md` | 80 | 100 |
| `task/todo.md` | 60 | 80 |
| `task/handoff.md` | 30 | 40 |
| `task/log.md` | 100 | 130 |
| `integration/meta.md` | 60 | 80 |
| `integration/checklist.md` | 80 | 100 |
| `integration/result.md` | 60 | 80 |
| `guide` | 100 | 130 |
| `domain` | 150 | 180 |
| `skill` | 60 | 80 |
| `policy` | 100 | 130 |
| `knowledge` | 40 | 60 |
| `index` | 60 | 80 |
| `default` | 100 | 130 |

- `guide` = `Docs/Guides/`, `domain` = `Docs/Domains/`, `knowledge` = `Pitfalls/`·`Decisions/`·`Facts/`, `index` = 모든 `index.md`.
- 표에 없는 `.md`는 `default`를 적용한다.
- **템플릿**(`Harness/Core/Templates/`)은 채워질 문서의 `warn`보다 짧아야 한다(넘으면 차단). 템플릿이 길면 복사된 모든 문서가 처음부터 길어진다.
- 제외: 모든 `History/` 폴더, `Docs/References/`(외부 원본 자료).

## 넘었을 때: 문서 종류별 처리

| 종류 | 대상 | 처리 |
|---|---|---|
| **기록형** (시간이 지나며 쌓임) | `task/*`, `integration/*` | 오래된 내용을 같은 폴더의 `History/`로 옮긴다 |
| **안내형** (규칙·설명) | 그 외 전부 | ① 압축 → ② 그래도 길면 주제별 하위 문서로 쪼개고 링크. 옛 버전은 git 기록이 보관하므로 `History/`에 복사하지 않는다 |

자세한 절차는 `Harness/Core/Skills/compact-docs.md`.

### History 규칙 (기록형 전용)

- 위치: 원래 문서와 같은 폴더의 `History/` (예: `Tasks/Active/Task-…/History/001-first-build.md`)
- 파일명: `NNN-주제.md` (소문자). 한 번 만든 파일은 수정하지 않는다.
- 현재 문서에는 "지금 유효한 내용"과 옮긴 파일 링크만 남긴다.

### 줄일 때 지우면 안 되는 것

해결 안 된 문제, 현재 유효한 결정, 남은 확인 항목, 이후 디버깅에 필요한 증거, 확정된 값. 배운 교훈은 지우지 말고 지식 폴더로 옮긴다(`knowledge-routing.md`).

## 검사 시점

1. AI가 문서를 고친 뒤 저장·제출·인계 전(`save-work`, `submit-work`, `write-handoff`)
2. 커밋 전: `pwsh -File Scripts/verify-fast.ps1`
3. PR: GitHub Actions `verify-docs`가 자동으로 검사한다(`.github/workflows/verify-docs.yml`)

## log.md 작성 규칙

한 항목은 30~50단어의 결론 1줄로 쓴다. 파일 열람, 검색 기록, 반복된 같은 검증, 사고 과정은 적지 않는다.

## 검색 순서

현재 Task → Domain Map 1개 → 이름이 정확한 파일/심볼 → 인접 도메인 → 저장소 전체 검색.
