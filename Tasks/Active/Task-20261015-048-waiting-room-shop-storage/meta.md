# Task-20261015-048

- **Title:** 대기실 3단계 — 상점과 소품실 보관
- **Type:** feat
- **Status:** assigned
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** start-work
- **Updated:** 2026-10-15

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-shop-storage
- **Previous Task:**

## Goal

- ③ 수화기 구매 → 배송구 수령 → 운반(1인 3개, 배낭 +3) → ④ 소품실 보관(런 동안 유지)과 물려줄 선반 4칸을 4인 동기화한다.
- 공동 자금 잔액·장부를 모든 참가자에게 같게 보인다. 품목·가격은 임시값이다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 구역: `Assets/Scripts/WaitingRoom/ShopStorage/`, `Assets/Scenes/Tests/WaitingRoom/ShopStorage/`. 1단계 Base 구역은 읽기만 한다.

## Decisions

- 기준: CC-132 전체 개발 순서, `Harness/Project/Decisions/Waiting-room/shop-storage.md`. 게임 흐름 기획과 충돌하면 게임 흐름 우선.
- Open: 품목·가격·화폐 상세는 M1 범위 밖 — 임시값만 사용.
- Open: CC-132의 '선반 4칸'과 shop-storage.md의 '4단 선반'·물려주기 규칙 대조 필요.
- Decided: 2026-10-15 PM — 실제 전달·인계 완료, 단계 순서 gate 해제. 앞 단계 완료·PM 보고 없이 바로 착수. JIRA는 CC-132.
- Decided: 의존 — 공연 결과·보상·공동 자금(CC-110, Task-031), 2단계(047). 공용 수정은 별도 소유 Task.
