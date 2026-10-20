# Task-20261021-068

- **Title:** 모든 묘기 씬 공용 공연장 배경 자동 로드 연결
- **Type:** fix
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** performance-background
- **Current Skill:** implement-code
- **Updated:** 2026-10-21

## Workspace

- **Integration:** dev
- **Branch:** fix/performance-background-load
- **Previous Task:** Task-20261020-067

## Goal

- #196의 배경을 dev로 가져오고 모든 실제 묘기 진입 시 additive 배경 하나를 자동 표시한다.
- 기존 코스·판정·카메라를 보존하고 묘기 종료 시 배경을 정리한다.

## Scope

- setup.md의 전용 로더·테스트와 빌드 씬 목록, 승인된 private 포인터만 수정한다.
- AI Setup Allowed: None

## Decisions

- Open: 사람 플레이 시점 배치·성능, 실제 2인/늦은 참가 검증.
- Decided: 2026-10-21 PM — 별도 worktree 없이 현재 프로젝트 사용. 같은 폴더의 dev 기반 작업 브랜치에서 구현 후 PR.
- Decided: PM 이미지와 이전 배치 근거를 따라 배경만 1/3 축소해 코스 중앙에 맞춘다. 코스 크기·게임 규칙은 변경하지 않는다.
- Decided: #197 통합 PR 및 #198 동기화 PR 충돌. dev 직접 push 없이 본 Task의 dev 대상 PR로 배경 포인터와 연결을 함께 제출한다. 두 PR은 임의로 닫지 않는다.
