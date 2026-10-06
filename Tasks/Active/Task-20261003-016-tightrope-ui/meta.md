# Task-20261003-016

- **Title:** UI 고도화
- **Type:** feat
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`, `Docs/Domains/network-session.md`
- **Current Skill:** implement-code
- **Updated:** 2026-10-06

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-ui (인계 때 생성)

## Goal

- 접속(방 만들기·참가·시작), 진행(남은 시간·묘기 시작·진행/도착/사망 인원·내 상태), 결과 화면이 테스트용 화면 대신 정식 UI로 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 015 결과·023 기록 계약 병합 후 순차 시작. 최종 입력/재도전 연결은 022가 담당.
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Decided: 2026-10-06 후속 승인 — 남색·아이보리·골드 데모 팝업, Pretendard/uGUI/TMP/DOTween 재사용. UI는 015 고정 결과·023 기록 계약을 읽는다.
- Decided: 최종 씬에 IMGUI 방 UI 제외, P/O 도구는 기본 숨김으로 유지(022가 gate 연결). 보상·새 패키지·폰트 수정 제외.
