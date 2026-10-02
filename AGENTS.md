# POC_CURTAINCALL Agent Guide

Unity 6 POC 프로젝트. 게임 개요는 아직 미정(TBD)이며 확정되면 `Harness/Project/Facts/overview.md`에 기록한다.
이 파일은 "무엇을 어떤 순서로 읽을지"만 안내한다. 링크를 재귀적으로 전부 읽지 않는다.

## 읽는 순서 (기본 컨텍스트)

1. 이 파일.
2. `pwsh -File Scripts/doctor.ps1` — 작업 시작에 한 번 환경 점검(1분 이내). 같은 작업을 이어갈 때 중복 실행하지 않는다. FAIL이면 먼저 해결하거나 사람에게 보고하며, 에디터 경고를 컴파일 통과로 해석하지 않는다.
3. 현재 Task: `Tasks/Active/Task-*/`의 `handoff.md` → `meta.md` → `setup.md`(작업 구역) → `todo.md` → `plan.md`.
   `log.md`, `History/`는 기본으로 읽지 않는다.
4. 관련 Domain Map 1개: `Docs/Domains/index.md`에서 고른다.
5. 현재 단계 Skill 1개: `Harness/Core/Skills/` 또는 `Harness/Engine/Unity/Skills/`.
6. 관련 지식만: 해당 폴더의 `index.md`를 보고 필요한 파일 1개만 연다.
   - 함정: `Harness/Engine/Unity/Pitfalls/`, `Harness/Project/Pitfalls/`
   - 결정: `Harness/Engine/Unity/Decisions/`, `Harness/Project/Decisions/`
   - 사실: `Harness/Engine/Unity/Facts/`, `Harness/Project/Facts/`

사용자의 말은 `Harness/Core/Policies/skill-routing.md`로 Skill에 대응시킨다(예: "저장해줘" → `save-work`).
현재 Task가 없으면 작업자에게는 PM에게 Task를 받으라고 안내하고, PM이면 `scaffold-task`를 따른다. 범위 없는 요청으로 게임/Unity 작업을 시작하지 않는다.

## 하네스 3층 구조

- `Harness/Core/` — 공통 층. 어느 프로젝트든 그대로 쓰는 Task 흐름·결정·Git·문서 크기 규칙.
- `Harness/Engine/Unity/` — 엔진 층. 에디터 조작, 씬·프리팹 소유권, Unity 함정.
- `Harness/Project/` — 프로젝트 층. 이 POC만의 사실·결정·함정.

충돌 시 우선순위: 사람의 현재 지시 > Project > Engine > Core.

## 경계 (항상 적용)

- 역할·수정 권한: `Harness/Core/Policies/team-roles.md`. 권한 밖 수정 요청은 하지 않고 "PM 요청 필요"라고 알린다.
- 브랜치·PR: `Harness/Core/Policies/git-workflow.md`. `feat/*`·`refactor/*`는 `integration/*`로만, `fix/*`·`resource/*`는 `dev` 또는 `integration/*`로 제출한다(`builds/*` 직행 금지).
- 플레이어가 체감하는 선택(게임 규칙·밸런스·UX·씬·아트·사운드)은 `Harness/Core/Policies/human-decision.md`.
- Unity 변경 전 `Harness/Engine/Unity/Policies/code-folders.md`, `asset-ownership.md`. `setup.md` 작업 구역 밖은 수정하지 않는다.
- commit/push는 사용자가 요청할 때만("저장해줘", "제출해줘" 포함). reset/discard/force는 PM의 명시적 요청 때만.
- 커밋 작성자는 gh 로그인 계정만. AI 이름의 작성자·`Co-Authored-By`·`Generated with` 줄을 커밋·PR에 넣지 않는다.
- 비밀값, OAuth 상태, Pipeline 토큰을 추적하거나 출력하지 않는다.
- 보조 AI 사용은 `Harness/Core/Policies/subagent-usage.md` (기본 1명, 동시 최대 2명).

## 이름 규칙

폴더는 첫 글자만 대문자(`Pitfalls/`, `Task-20260929-001-slug/`), 파일은 모두 소문자(`version.md`, `doctor.ps1`).
예외: 외부 도구가 강제하는 이름(`AGENTS.md`, `CLAUDE.md`, `SKILL.md`, Unity `Assets/`).

## 검증 (계단식, 자세한 기준은 `Harness/Core/Policies/verification-ladder.md`)

- 작업자 검사 한 번에: `pwsh -File Scripts/check-work.ps1` (구역 → 문서 → 컴파일 → 테스트, handoff에 기록)
- 개별: `Scripts/verify-scope.ps1`, `verify-fast.ps1`, `verify-unity.ps1 -ProjectPath . [-Compile] [-RunTests]`
- 테스트가 없으면 PASS가 아니라 `NO_PROJECT_TESTS`로 보고한다.
- 실패·생략·환경 제약은 그대로 handoff에 적는다.

## 배운 것 기록

함정·결정·사실을 알게 되면 로그에 묻지 말고 `Harness/Core/Skills/record-knowledge.md`로 해당 폴더에 파일 1개로 남긴다.
