# Task-20261020-063

- **Title:** 음성 채팅(생존자 거리 감쇠·사망자 분리)
- **Type:** feat
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** voice-chat (`Docs/Domains/voice-chat.md`, 신규) · network-session · player-sync
- **Current Skill:** start-work
- **Updated:** 2026-10-20

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/voice-chat
- **Branch:** feat/voice-chat
- **Previous Task:**

## Goal

- Start에서 2명이 Services 방으로 접속해 로비·대기실·줄타기·인간 대포에서 생존자 거리 감쇠 음성, 사망자끼리 거리 무관 음성, 생존·사망 분리, 음소거 키를 확인할 수 있다.
- 자동 생성 관리자라 씬·프리팹 수정 없이 Start 흐름의 확정 씬(세팅 `사용 씬`)에서만 동작한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 음소거 키 기본값(임시 `M`, control-keys 목록 밖 — 디스코드 `소집` 합의)
- Decided: `Harness/Project/Decisions/voice-chat.md` (2026-10-20 PM)
