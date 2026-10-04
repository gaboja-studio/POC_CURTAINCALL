# Task-20261005-021

- **Title:** 장애물 배치 편집기(씬 뷰 배치·쉬운 시간 칸·미리보기)
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-05

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/obstacle-layout-editor

## Goal

- 기획자가 씬 뷰에서 톱날을 보면서 끌어 배치하고, 쉬운 말로 된 칸(○초에 움직임·○초 뒤 멈춤·다음 등장까지 ○초)으로 순서를 정하며, 시간 슬라이더로 플레이 없이 위치를 미리 본다. 결과는 지금과 같은 장애물 동작으로 플레이된다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 012(톱날)·013(세팅) 병합 완료. 갈래 A에서 **014 불보다 먼저**(불타는 구간도 이 편집기로 배치하도록 확장 자리를 둔다).
- 계기: 2026-10-05 012 공동 테스트 8 — `steps`의 `trigger`·`repeat` 뜻이 불분명(가로 `repeat 0`인데 계속 왕복 = 정지 단계 전까지 왕복), 수직 톱날이 93m에서 생겨 110초 뒤에야 플레이어에게 닿아 "안 나온 것처럼" 보임.
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`) — 편집기는 세팅 파일을 보여 주고 고치기만 한다.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 기존 `steps`의 고급 조건(다른 단계 시작·끝 뒤, 선두 거리)을 쉬운 칸에 남길지
- Decided: 2026-10-05 @MoHoDu — 씬 뷰 배치·쉬운 시간 칸·시간 슬라이더 미리보기로 바꾼다. 저장은 세팅 폴더 한 곳(장애물 배치 파일), 공통 규격(크기·높이)은 외줄 세팅에 남긴다
- Decided: 2026-10-05 @MoHoDu — 수직 톱날은 너무 갑자기 생기지 않게 **선두 플레이어 앞 n m 이상**인 곳에 등장(n은 세팅 칸, 임시값). 등장 지점은 기획 생성 지점(93m)을 넘지 않고, 선두가 그보다 가까워 n m를 둘 수 없으면 등장을 미룬다(해석, 작업 중 PM 확인)
- Decided: 2026-10-05 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정, JIRA CC-66, 공동 테스트 3개 확정
