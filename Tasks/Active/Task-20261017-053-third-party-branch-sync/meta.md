# Task-20261017-053

- **Title:** 외부 에셋 안전 브랜치 전환
- **Type:** fix
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** assets-structure
- **Current Skill:** write-handoff
- **Updated:** 2026-10-17

## Workspace

- **Integration:** dev
- **Branch:** fix/third-party-branch-sync
- **Previous Task:**

## Goal

- 에셋 수신 실패를 브랜치 전환 전에 차단하고 파일·사용자 변경을 보존한다.

## Scope

- 작업 구역은 `setup.md` 기준.
- AI Setup Allowed: None

## Decisions

- Open: None
- Decided: 2026-10-17 — PM 본인 A만 준비 커밋 없이 로컬 구현하는 단회 예외. 일반 배정·착수 gate는 유지한다.
- Decided: 2026-10-17 — 사용자 저장 요청·경로 판단 위임에 따라 단일 fix의 제출 대상을 dev로 결정했다. fix 브랜치 commit/push만 진행하며 dev 직접 push·PR·병합은 하지 않는다. 별도 원격 통합은 만들지 않는다.
