# Task-20261003-014

- **Title:** 불(추격 불·불타는 구간)
- **Type:** feat
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`
- **Current Skill:** implement-code
- **Updated:** 2026-10-05

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-fire

## Goal

- 묘기가 시작되면 출발점 뒤에서 불이 쫓아와 닿은 사람은 사망하고, 줄 일부 구간이 시간에 맞춰 불타 쓸 수 없게 되어 후반으로 갈수록 쓸 수 있는 줄이 줄어드는 모습이 모두에게 같게 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 005(완료)·013(세팅) 병합 후. 갈래 A에서 012 톱날 다음(사망 처리 경로를 012와 같게).
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Decided: 2026-10-05 @MoHoDu — 발화 당시 구간 위 플레이어도 사망한다. 높이와 무관한 수평 접촉 판정이며 같은 호스트 판정 시점에는 도착을 우선한다.
- Decided: 2026-10-05 @MoHoDu — 초기 실험값: 묘기 10초 후 -6m에서 0.35m/s 추격·95m 정지, 120초에 1·4번 줄 55~95m, 180초에 3번 줄 70~95m(끝 제외), 10초 예고. 설정 스냅샷은 다음 시도부터 변경 적용.
- Open: 초기 실험값의 밸런스 적합성 — 실제 4인 결과로 확인한다.
- Decided: 2026-10-03 @MoHoDu — 화재는 새 Task. 수치는 나중에 에디터에서 변경 가능하게
