# Task-20261017-055

- **Title:** Ubuntu CI Git 실행 경로 단일 선택
- **Type:** fix
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** assets-structure
- **Current Skill:** submit-work
- **Updated:** 2026-10-17

## Workspace

- **Integration:** dev
- **Branch:** fix/integration-sync-git-path
- **Previous Task:**

## Goal

- Ubuntu에서 복수 Git 실행 경로가 반환되어도 격리 동기화 회귀가 하나의 실행 파일만 호출한다.

## Scope

- 작업 구역은 setup.md 기준.
- AI Setup Allowed: None

## Decisions

- Open: None
- Decided: 2026-10-17 — PR #147 병합 후 CI 결함만 별도 fix로 수정. 기존 B/A 문서는 섞지 않음.
- Decided: 2026-10-17 — 승인 계획에 따라 최신 dev 기준 PM 본인 직접 준비·배정·착수 단회 예외. 번호는 전체 ref 검사·잠금으로 발급. 별도 팀원 전달 N/A, 일반 gate 변경 없음.
- Decided: 2026-10-17 — 이 Task에 Read/Edit/Write 사용 승인. 편집 전 재독·변경안 설명, 전용 편집 dryRun 없음. 초기 구현 승인은 저장·제출 제외.
- Decided: 2026-10-17 — 로컬 검증 보고 뒤 사용자 "진행해"로 저장·제출 진행 승인. PR 미리보기 확인 후 등록하며 병합·A/B 마감·실제 동기화·C/private는 제외.
