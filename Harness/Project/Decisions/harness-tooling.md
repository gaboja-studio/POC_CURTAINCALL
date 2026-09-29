---
summary: 하네스 도구 선택 — PowerShell 스크립트, 일반 md Skill + .claude/skills 링크, 리포 옆 worktree
status: active
updated: 2026-09-29
source: human (2026-09-29 하네스 세팅 시 추천안으로 진행 승인)
---

# 하네스 도구 선택

## 언제 읽나

스크립트를 추가하거나, Skill 형식·위치를 바꾸거나, worktree 경로를 바꿀 때.

## 결정

- 2026-09-29 / 프로젝트 소유자:
  - 스크립트: PowerShell 7(`.ps1`). macOS·Windows에서 같은 스크립트를 쓴다.
  - Skill: `Harness/Core/Skills/`, `Harness/Engine/Unity/Skills/`에 소문자 `.md`(frontmatter 포함)로 둔다. Claude Code용 `.claude/skills/<name>/SKILL.md`는 `Scripts/setup-links.ps1`이 심볼릭 링크로 만들며 git에서 무시한다.
  - worktree: 리포 상위 폴더의 `github-worktrees/POC_CURTAINCALL/` (`.env`의 `WORKTREE_ROOT`로 변경 가능).
  - 하네스 구조: Octoplug 회고 보고서의 3층 구조를 따른다. 폴더명은 첫 글자만 대문자, 파일명은 모두 소문자이며 함정/결정/사실은 상황별 파일로 나눈다.

## 이유

- 기존 뼈대가 `.ps1`이었고, Windows 팀원이 합류해도 그대로 쓸 수 있다.
- 파일명 소문자 규칙과 모델 중립(AGENTS.md)을 지키면서 Claude의 Skill 자동 인식도 쓸 수 있다.

## 바꾸려면

사람의 결정이 필요하다. 스크립트 언어를 바꾸면 `Scripts/` 전체와 모든 문서의 실행 명령을 함께 바꾼다.
