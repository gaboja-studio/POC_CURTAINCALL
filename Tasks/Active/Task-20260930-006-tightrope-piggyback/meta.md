# Task-20260930-006

- **Title:** 목마
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-piggyback (인계 때 생성)

## Goal

- F 짧게로 다른 캐릭터에게 목마로 올라타고 F 길게로 내리며, 안 되는 상황은 막힌다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 외줄 위 캐릭터 동작(011) 병합 후(갈래 B, 011 → 006 → 010). 실제 플레이어 프리팹과 줄 위에서 바로 만든다. 연결·해제 호스트 판정과 동기화는 이 작업에서 같이 한다(2026-10-02 변경). 남은 예외는 010.
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(013, `Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 세부 규칙은 Confluence 문서 반입 후 확인
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정. 바뀌면 PM이 다시 배정
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
- Decided: 2026-10-02 @MoHoDu — 기능과 온라인 동기화를 같이 만든다. 실제 플레이어 프리팹과 007 공개 기능(`Assets/Scripts/Network/PlayerSync/`)·004 공개 함수를 쓴다. 단계마다 여러 명 접속으로 확인 (`Harness/Project/Decisions/feature-with-network.md`)
