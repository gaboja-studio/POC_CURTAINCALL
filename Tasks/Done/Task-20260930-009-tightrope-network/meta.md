# Task-20260930-009

- **Title:** 외줄 묘기 플로우 최종 씬
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-08

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-network (인계 때 생성)

## Goal

- 방 만들기부터 4명이 외줄 묘기 플로우(대기 → 묘기 시작 → 톱날·추격 불·불타는 구간 → 도착 클리어/실패 재시작 → 결과·보상)를 한 번에 해 볼 수 있는 **최종 데모 씬**이 있고, 모두에게 같게 보인다. 앞 작업에서 남은 연결·버그를 정리한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 마지막 단계. 1~3단계 작업(012·014·011·006·010·015·016, 에셋 작업 017·018·019는 준비된 만큼) 병합 후. 동기화는 각 기능 작업이 이미 했으므로 이 작업은 묘기 플로우 최종 씬과 남은 연결만.
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(013, `Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Decided: 2026-10-03 @MoHoDu — 최종 씬은 "묘기 플로우" 전체: 방 만들기 → 대기 → 묘기 시작 → 톱날·불 → 도착/실패 → 결과·보상
- Open: 세부 규칙은 Confluence 문서 반입 후 확인
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정. 바뀌면 PM이 다시 배정
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
- Decided: 2026-10-02 @MoHoDu — 기능과 온라인 동기화를 같이 만든다. 실제 플레이어 프리팹과 007 공개 기능(`Assets/Scripts/Network/PlayerSync/`)·004 공개 함수를 쓴다. 단계마다 여러 명 접속으로 확인 (`Harness/Project/Decisions/feature-with-network.md`)
