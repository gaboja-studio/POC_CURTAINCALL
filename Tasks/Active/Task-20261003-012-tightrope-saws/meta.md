# Task-20261003-012

- **Title:** 톱날 장애물(가로·수직)
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** [tightrope-course](../../../Docs/Domains/tightrope-course.md)
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-saws

## Goal

- 외줄 코스 위에 가로 톱날 22개가 좌우로 왕복하고 수직 톱날이 줄을 따라 내려오며, 톱날에 닿은 플레이어는 닿은 위치에 맞는 팔·다리가 잘린다(020이 손상 처리). 가로는 점프로, 수직은 옆줄 이동(또는 목마 2층이 넘어가 버튼)으로 피하는 모습이 모두에게 같게 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 005(완료). 013(세팅)과 병행 가능(2026-10-03 PM) — 013 병합 후 수치를 세팅 파일로 옮긴다. 갈래 A 첫 작업, 다음은 014 불(같은 사망 경로). 수직 톱날 버튼은 목마 앞으로 점프(006)가 있어야 시험할 수 있다 — 버튼 자체는 이 작업, 목마 연결 확인은 006 이후.
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(013, `Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md` · 규칙: `Harness/Project/Decisions/tightrope-course-rules.md`

## Decisions

- Decided: 2026-10-03 @MoHoDu — 발판: 진행 중인 플레이어 누구든 밟는 즉시 눌림(`plateHoldTime` 0), 톱날 뒤 1.5m(`plateOffset`). 세팅 칸으로 조정
- Decided: 2026-10-03 @MoHoDu — 부위 판정: 허리(0.85m) 아래 다리·위 팔, 좌우는 몸 중심 기준 코스 오른쪽 +(정가운데는 오른쪽), 이미 잃은 쪽이면 같은 종류 반대쪽, 둘 다 잃었으면 없음
- Decided: 2026-10-05 @MoHoDu — 020 병합 후 연결: 가로 톱날은 020 순서 절단(`ServerCutLegThenArm`), **첫 다리는 톱날이 닿은 쪽**(규칙 #9 "최초 좌우 미정"을 채움), 수직 톱날은 닿은 위치 부위(`ServerCutPart`). `SawSettings`는 `TightropeSettings` 톱날 칸으로 이동(세팅 파일 012 배정). 012는 020·두 작업이 함께 동작하는 것을 확인한 뒤 병합
- Decided: 2026-10-03 @MoHoDu — 묘기 시작 = 진행 중인 플레이어 중 **첫 사람이 줄에 오른 순간**(규칙 #3 변경, 원래 전원 — 한 명이 플랫폼에 남아 장애물을 늦추는 치팅 방지). 005 `TightropeRun.cs`를 012에 공용 파일로 배정해 수정
- Decided: 2026-10-03 @MoHoDu — 수직 톱날 줄은 생성 지점보다 앞(시작 쪽)에 진행자가 있는 줄 중 랜덤, 없으면 출현 대기(선두 뒤에 생기는 문제 해결)
- Decided: 2026-10-03 @MoHoDu — 장애물 순서는 **단계 목록**(`SawSettings.steps`, 레벨 디자인): 단계마다 시작 조건(게임 시작·묘기 시작·선두 거리·다른 단계 시작/끝 후)·지연·할 일(가로 톱날 범위 작동/정지, 수직 톱날 출현·줄 지정 또는 랜덤)·반복. 가로 정지 = 가까운 끝(맵 밖)까지 가서 멈춤. 기본값은 원문 규칙 재현(가로 전체 게임 시작, 수직 묘기 60초·제거 20초 뒤 반복). 012에서 구현. 가로 톱날 목록은 위치·출발 쪽만(출발 지연은 단계로 대체) 양산·밸런스 데이터는 나중에 구글 시트 등으로 뺄 수 있다(지금은 하지 않음)
- Decided: 2026-10-03 @MoHoDu — 톱날은 즉시 사망이 아니라 부위 절단(규칙 #9). 012는 닿은 부위 판정·요청까지, 손상 상태·패널티·공유는 020. **012는 020보다 뒤에 병합**
- Decided: 2026-10-03 @MoHoDu — 발판을 밟으면 톱날이 0.1~0.3초(세팅, 기본 0.2초) 안에 내려간다. JIRA CC-64, 공동 테스트 3개 확정
- Decided: 2026-10-03 @MoHoDu — 수직 톱날 버튼은 우선 **발판(밟으면 눌림)**으로 임시 구현, 형태를 나중에 바꿀 수 있게 눌림 판정과 겉모습을 분리
- Decided: 2026-10-03 @MoHoDu — 013(세팅)과 병행. 톱날 수치는 우선 `Tightrope/Saws/`의 `SawSettings` 묶음에 두고, 013 병합 후 `TightropeSettings` 톱날 칸으로 옮긴다
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정. 바뀌면 PM이 다시 배정
- Decided: 2026-10-03 @MoHoDu — 톱날은 이 새 Task가 맡는다(권유서 §5). 규칙·수치는 `tightrope-course-rules.md` #6~#8
