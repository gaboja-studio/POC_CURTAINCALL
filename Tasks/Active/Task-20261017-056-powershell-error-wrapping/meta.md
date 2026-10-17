# Task-20261017-056

- **Title:** PowerShell 오류 출력 줄바꿈 회귀 수정
- **Type:** fix
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** assets-structure
- **Current Skill:** submit-work
- **Updated:** 2026-10-17

## Workspace

- **Integration:** dev
- **Branch:** fix/powershell-error-wrapping
- **Previous Task:**

## Goal

- 줄바꿈·색상 코드가 포함된 예상 오류를 올바르게 판정하고, 다른 오류나 정상 종료는 계속 거부한다.

## Scope

- 작업 구역은 setup.md 기준.
- AI Setup Allowed: None

## Decisions

- Open: None
- Decided: 2026-10-17 — 기존 PR #148이 이미 병합되어 새 Task·브랜치로 수정하라는 사용자 요청. 055 및 다른 Task는 수정하지 않음.
- Decided: 2026-10-17 — 전체 ref 번호 검사·잠금으로 발급. PM 본인 직접 dev 기준 fix 준비·배정·착수, 실팀원 전달 N/A. 일반 gate 변경 없음.
- Decided: 2026-10-17 — 기존 승인 계획의 최소 수정·3가지 회귀·로컬 검증을 수행. Read/Edit/Write로 편집 전 읽기·변경안 설명, 전용 편집 dryRun 없음.
- Decided: 2026-10-17 — 게임 기능·운영 코드·workflow·에셋·private·실제 동기화는 제외. 초기 구현은 새 PR 등록·병합 제외.
- Decided: 2026-10-17 — 사용자 "저장하고 PR 제출해서 온라인 검사까지 확인해줘"로 커밋·push·새 PR 제출·실제 CI 확인 승인. PR 미리보기 확인 후 등록하며 병합은 제외.
