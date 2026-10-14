# Task-20261013-028

- **Title:** 런 진행 관리와 시간 흐름
- **Type:** feat
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`) — 런 진행 (코드 확정 후 Domain Map 추가)
- **Current Skill:** 없음 (integrated, 통합 마무리 때 Done 이동)
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/campaign-flow

## Goal

- 새 런을 시작하면 세대 1·1일차로 시작하고, 기획된 하루 흐름(1일차: 공연 → 쉬는 시간(가이드) → 공연 → 밤 정비 / 2·3일차: 아침 쉬는 시간 → 공연 → 쉬는 시간 → 공연 → 밤 정비)과 4일차 아침 쉬는 시간 종료(기한 초과) → 런 실패 → 세대 교체가 상태 전환으로 확인된다.
- 쉬는 시간 타이머가 세팅의 A/B 방식(기본 B)대로 시작하고, 밤 정비는 전원 준비 완료로 끝난다. 세팅이 공통·묘기별·대기실·탐색·탈출로 5묶음으로 나뉘고 정해진 공통 값이 들어 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 없음
- Decided: 2026-10-14 PM: 쉬는 시간 B안 기본·A/B 세팅 전환, 세팅 5묶음, 최소 인원 2명, 게임 시작 후 재접속 불가(로비 허용) 확인, 기물 소지 2/1/+2 공통 값(run-rules.md, game-settings.md). 공간 내부 기능·보상·기물 구매·장소 전환은 포함하지 않는다. 통합 계획 1A
- Decided: 2026-10-14 PM: 시간대 전환 신호 = `CampaignRun` 메서드 + `StepChanged` 확정, B안 타이머 팀 공용·첫 사람이 나올 때 시작(안 나오면 멈춤), 최소 인원 2 유지·034 먼저(run-rules.md #6~8)
