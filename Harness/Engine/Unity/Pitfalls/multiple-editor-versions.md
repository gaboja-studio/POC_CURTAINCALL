---
summary: 여러 Unity 에디터 버전이 설치된 머신에서 다른 버전으로 프로젝트를 열어 업그레이드/재import가 발생함
status: active
updated: 2026-09-29
source: 2026-09-29 환경 점검 (6000.0 / 6000.3 / 6000.6 / 6000.7b 설치 확인)
---

# 에디터 버전 혼동

## 언제 읽나

프로젝트나 새 worktree를 에디터로 열 때, CLI로 batch 실행(`unity run`, `unity test`, `unity build`)할 때.

## 증상

프로젝트 업그레이드 경고가 뜨거나, `ProjectSettings/ProjectVersion.txt`와 `Packages/`가 의도치 않게 바뀐다.

## 원인

Hub 기본 에디터나 임의 경로의 에디터로 열었다.

## 대처

- `unity open <project>`처럼 `ProjectVersion.txt`에 맞는 버전을 고르는 명령을 쓴다.
- 버전을 올리는 것은 사람의 결정이다(`Decisions/version.md`).
- 실수로 열었다면 `git status -- ProjectSettings Packages`로 변경을 확인하고 사람에게 알린다.

## 확인 방법

`doctor.ps1`이 `ProjectVersion.txt`와 `Decisions/version.md`의 버전 일치, 해당 에디터 설치 여부를 검사한다.
