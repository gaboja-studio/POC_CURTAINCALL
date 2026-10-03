# Task-20261003-016

- **Title:** UI 고도화
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`, `Docs/Domains/network-session.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-ui (인계 때 생성)

## Goal

- 접속(방 만들기·참가·시작), 진행(남은 시간·묘기 시작·진행/도착/사망 인원·내 상태), 결과 화면이 테스트용 화면 대신 정식 UI로 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 015(결과)·013 병합 후. 아트 없이 임시 스타일로 시작하고, 아트가 오면 교체.
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 화면 배치·글자·색 — 플레이어 체감 UX라 `request-human-decision`으로 PM 확인
- Open: 테스트용 IMGUI(`NetworkSessionTestUI`, `TightropeRunDebug`)를 개발용으로 남길지
