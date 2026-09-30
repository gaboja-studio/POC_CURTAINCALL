# Git Workflow

비개발 작업자도 몇 가지 말만으로 작업하고, 합칠 때 문제가 없게 하는 브랜치 규칙이다.
결정 기록: `Harness/Project/Decisions/branch-strategy.md`. 권한: `team-roles.md`.

## 브랜치 종류

| 종류 | 용도 | 만드는 사람 | 합치는 곳 | 이름 |
|---|---|---|---|---|
| `dev` | 팀 공용 원본 | — | — | `dev` |
| `integration/*` | 기능 묶음 합치기 | PM | `dev` | `integration/<기능-slug>` |
| `feat/*` | 기능 추가 | PM(준비) | `integration/*` | `feat/<slug>` |
| `fix/*` | 완성된 기능 수정 | PM(준비) | `integration/*` | `fix/<slug>` |
| `refactor/*` | 동작 유지, 코드 정리 | PM(준비) | `integration/*` | `refactor/<slug>` |
| `builds/*` | 빌드 버전 보관 | PM | 없음 (dev에서 받기만) | `builds/<버전>` |
| `tests/*` | 임시·실험 | 누구나 | **없음** (끝나면 삭제) | `tests/<자유>` |

작업 브랜치의 slug는 목적을 나타내는 소문자 kebab-case로 쓴다(예: `feat/project-overview`). 날짜·Task 번호·관리 코드는 이름에 넣지 않는다. 같은 종류/slug는 중복 배정하지 않으며, Task ID·폴더명은 유지하고 `meta.md`의 Branch로 연결한다.

## 흐름 (한 방향, 예외 없음)

```
feat/* · fix/* · refactor/*  ──PR(Squash)──▶ integration/* ──PR(Merge commit)──▶ dev ──PR──▶ builds/*
```

- 작업 브랜치는 **항상** `integration/*`로만 제출한다. 작은 수정도 PM이 작은 integration을 만들어 받는다.
- `dev`, `integration/*`, `builds/*`는 **PR로만** 바뀐다. 직접 push하지 않는다(브랜치 보호는 PM이 GitHub에서 설정).
  - 예외: PM은 `integration/*`에 준비 작업(통합 기록, Task 폴더, 테스트 씬·폴더)과 마무리 작업(Done 이동)을 직접 올린다. 브랜치 보호의 **우회(bypass) 목록에 PM 계정만** 넣는다. `dev`는 PM도 PR로만 반영한다.
- `integration/*`는 `dev`에서, 작업 브랜치는 소속 `integration/*`에서 만든다. `builds/*`는 빌드 시점의 `dev`에서 만든다.
- 합치기 단계에서 기능끼리 연결하는 작업도 **별도 Task(연결 Task)**로 만들어 같은 흐름을 탄다.
- `Scripts/new-pr.ps1`이 이 흐름에 어긋나는 PR을 거부한다.

## 병합 방식

- 작업 → `integration/*`: **Squash**. Task 하나가 기록 한 줄이 되어 문제 Task를 찾고 빼기 쉽다.
- `integration/*` → `dev`: **Merge commit**. 기능 묶음 단위로 기록이 남고 한 번에 되돌릴 수 있다.
- 병합한 작업 브랜치에서 계속 작업하지 않는다(Squash 후 기록이 어긋난다). 추가 작업은 새 Task로 한다.

## 작성자

- 커밋은 **gh에 로그인된 현재 GitHub 계정으로만** 한다. 누가 작업했는지가 GitHub 기록에 보여야 한다.
- AI(Claude, Claude Code 등)의 이름을 작성자·공동 작성자로 쓰지 않는다. 커밋 메시지와 PR 본문에 `Co-Authored-By: <AI>`, `Generated with <AI>` 같은 줄을 넣지 않는다.
- 설정: `pwsh -File Scripts/setup-gh.ps1`이 이 저장소의 작성자를 gh 계정(`<id>+<login>@users.noreply.github.com`)으로 맞추고 커밋 검사 훅(`.githooks/commit-msg`)을 켠다.
- 강제: 커밋하는 스크립트는 작성자가 gh 계정이 아니면 멈추고, 훅은 AI 이름이 있으면 커밋을 거부하며, PR에서는 `verify-commits` 검사가 한 번 더 확인한다.
- GitHub에 연결되지 않은 PC에서는 커밋하지 않는다(`setup-gh` Skill로 연결).

## 저장(커밋)·올리기(push)

- AI는 사용자가 요청할 때만 커밋·push한다. "저장해줘", "제출해줘", "오늘 여기까지"가 요청에 해당한다.
- 커밋 전: `verify-fast`(문서) → 컴파일 확인(`verification-ladder.md` 2단계).
- 커밋 메시지: `<종류>: <요약>` (예: `feat: 커튼 열기 3초 연출`, `fix: …`, `docs: …`, `chore: …`)
- 작업자는 자기 작업 브랜치에만 push한다.
- hook·서명 우회, force push, reset, 이력 재작성은 PM의 명시적 요청 없이는 하지 않는다.

## PR과 순차 병합

- PR·이슈 형식: `Harness/Project/Decisions/pr-issue-format.md`. 등록은 `submit-work` / `report-issue` Skill.
- PM은 정해진 시간에 PR을 **하나씩** 병합한다(순서: 공용 파일 소유 Task → 의존 대상 Task → 나머지).
- 앞 PR이 병합되어 뒤 PR이 뒤처지면 GitHub **Update branch**로 최신 내용을 받는다.
- 충돌은 해당 Task 담당자와 PM이 함께 해결한다. **씬·프리팹 YAML 충돌은 AI가 해결하지 않는다.**
- 통합을 마무리할 때 각 Task PR의 `closed #번호`를 `dev` PR 본문에 모은다(GitHub는 dev 병합 때만 이슈를 닫음).

## 안전

- 수정 전 `git status`로 기존 변경을 보존한다. 관련 없는 변경을 섞지 않는다.
- 비밀값, `.env`, 개인 설정, 생성된 Unity 폴더(`Library/` 등)를 커밋하지 않는다.
- 새 에셋의 `.meta`를 함께 커밋한다.
- 선행 Task가 소속 integration에 아직 병합되지 않았다면 체리픽으로 우회하지 말고 PM에게 알린다.
