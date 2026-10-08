# Task-20260930-010

- **Title:** 연결: 목마 + 온라인
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-08

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/piggyback-network (인계 때 생성)

## Goal

- 온라인 목마의 예외(동시에 타려 할 때, 목마 중 추락·연쇄 추락, 목마 중 호스트 이탈 등)가 올바르게 처리된다. 기본 연결·해제 동기화는 006이 한다(2026-10-02 축소).

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 목마(006) 병합 후(갈래 B 마지막). 합치기 ① 전에 끝낸다.
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(013, `Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 세부 규칙은 Confluence 문서 반입 후 확인
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정. 바뀌면 PM이 다시 배정
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
- Decided: 2026-10-02 @MoHoDu — 기능과 온라인 동기화를 같이 만든다. 실제 플레이어 프리팹과 007 공개 기능(`Assets/Scripts/Network/PlayerSync/`)·004 공개 함수를 쓴다. 단계마다 여러 명 접속으로 확인 (`Harness/Project/Decisions/feature-with-network.md`)
