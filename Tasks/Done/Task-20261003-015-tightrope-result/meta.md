# Task-20261003-015

- **Title:** 묘기 확정 결과·참가자 신체 상태
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`
- **Current Skill:** implement-code
- **Updated:** 2026-10-08

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-result (인계 때 생성)

## Goal

- 호스트가 묘기 종료 직전 결과·고정 시간·직접 도착/동반 성공/사망/이탈·참가자별 사지 손실 상태를 확정하고 모든 화면에 같은 스냅샷을 공유한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`가 기준이다.
- AI Setup Allowed: 승인된 Unity 에디터 기반 전용 테스트 씬 배치.
- Dependencies: 005·013·014 병합본. 결과 화면은 016, 개인 JSON 저장은 023, 최종 재도전·입력·포즈 연결은 022.
- 보상·보상 설정·외부 업로드·최종 프로토타입 씬 편집은 제외한다.

## Decisions

- Decided: 2026-10-06 @MoHoDu 후속 계획 승인 — 2026-10-03 보상 포함·기록 제외 계획을 대체한다. 이번 보상은 제외하고 기록은 별도 023에서 구현한다.
- Decided: 성공 시 생존자를 동반 Arrived로 바꾸기 전, 실패 시 종료 상태 변경과 자동 재시도 전에 호스트가 불변 결과를 고정한다.
- Decided: 기존 실패 3초 자동 재시도 유지. 지난 결과는 다음 시도에도 보존하고 회차별 중복 종료·이전 회차 혼입을 방지한다.
- Open: 실제 4인 결과 일치 검증은 사람 플레이 결과 대기.
