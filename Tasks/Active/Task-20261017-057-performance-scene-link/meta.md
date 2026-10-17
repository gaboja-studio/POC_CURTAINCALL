# Task-20261017-057

- **Title:** 게임 흐름의 실제 줄타기·인간 대포 씬 연결
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** start-lobby
- **Current Skill:** start-work
- **Updated:** 2026-10-17

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/performance-scene-link
- **Branch:** feat/performance-scene-link
- **Previous Task:**

## Goal

- 시작·로비에서 지정 줄타기와 인간 대포 공연을 기존 순서로 진행하고, 씬 직접 실행도 유지한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: None
- Decided: 2026-10-17 — PM 계획 승인 및 권한 차단 후 재허용. 준비 커밋/push만 승인, 기능 저장·PR·병합·enhancement 전달 제외.
- Decided: 2026-10-17 — PM 본인 단일 연결 Task. 기존 원본 씬/대포/로비 코드는 읽기 전용. 편집 전 읽기·변경 설명 후 Read/Edit/Write, 씬·설정은 Editor 사용.
