---
summary: 브랜치 6종, 한 방향 PR 흐름, 병합 방식(Squash/Merge commit), 연결 Task, PM 전용 영역
status: active
updated: 2026-09-29
source: human (2026-09-29 팀 협업 방식 결정)
---

# 브랜치 전략

## 언제 읽나

브랜치를 만들거나, PR 대상을 정하거나, 병합 방식을 고를 때. 규칙 전문은 `Harness/Core/Policies/git-workflow.md`.

## 결정

- 2026-09-29 / PM(@MoHoDu):
  - 기본 브랜치 이름은 `dev`로 유지한다.
  - 브랜치 종류: `feat/*`(기능 추가), `fix/*`(완성된 기능 수정), `refactor/*`(리팩터링), `integration/*`(기능 묶음 병합), `builds/*`(빌드 버전), `tests/*`(임시·테스트, 합치지 않음).
  - 통합 단위는 **기능 묶음**이다. 담당자가 모두 마무리하면 PR을 순차 병합한다.
  - 흐름: 작업 브랜치 → `integration/*` → `dev` → `builds/*`. **예외 없이** 작업 브랜치는 `integration/*`로만 제출한다.
  - `dev`, `integration/*`, `builds/*`는 PR로만 병합한다(GitHub 브랜치 보호는 PM이 직접 설정).
  - 병합 방식: 작업 → 통합은 Squash, 통합 → dev는 Merge commit.
  - 합치기 단계의 기능 연결도 별도 Task(연결 Task)로 만든다.
  - PM 전용 수정: `Harness/`, `AGENTS.md`, `CLAUDE.md`, 루트 `Scripts/`, `.github/`, `Docs/Guides/`, Skill, 프로젝트 MCP. 권한 표는 `Harness/Core/Policies/team-roles.md`.
  - 데일리 미팅 기록은 하네스에 포함하지 않는다.

## 이유

비개발 작업자는 "내 브랜치에서 작업 → 제출"만 알면 되고, 충돌 해결과 병합은 PM 한 사람이 일관되게 처리한다.

## 바꾸려면

`git-workflow.md`, `Scripts/new-pr.ps1`(허용 흐름), `.github/CODEOWNERS`, `Docs/Guides/team-workflow.md`를 함께 고친다.
