# Task-20261013-031

- **Title:** 공연 결과 및 보상, 공동 자금 연결
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`)
- **Current Skill:** start-work
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/performance-reward-funds

## Goal

- 모든 묘기 공통 공연 결과(성공/실패·사망자·잃은 팔다리 변화·플레이어별 정산액)가 나오고, 생존 2명 미만이면 실패한다. 팀 호응 게이지(플레이어별 구간, 사망 시 구간 제거)가 성공 시 공동 자금으로 들어가고 실패 시 0이 된다.
- 공동 자금은 팀 공용 잔액 하나(호스트 확정·모두 같은 값)이고 사용 내역(장부)이 남는다. 일차가 바뀌어도 유지되고 세대 교체 때 초기화된다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 환호·야유 행동별 증감·생존 보너스·절단 보너스 수치(보상 시스템 문서 전까지 임시값)
- Decided: 2026-10-14 PM: 생존 2명 미만 = 묘기 공통 실패(개발자 모드 034가 예외), 공동 자금은 팀 공용 잔액. 상점·구매 UI는 범위 밖. 선행: 029, 033, 028 세팅 틀. 통합 계획 2B
- Decided: 2026-10-14 PM: 033은 결과 명부를 옮기지 않음 → 031이 `Systems/Results/`(작업 구역)에 묘기 공통 공연 결과를 새로 만든다. 외줄 결과 코드는 고치지 않고 4단계 연결 때 교체
- Decided: 2026-10-14 PM: 묘기가 종료를 알리면 031이 결과(성공/실패·종료 이유·사망자·팔다리 변화·정산액)를 확정·공유하고 그다음 `CampaignRun.EndPerformance()`를 부른다(결과 먼저 → 시간대 전환). 생존 2명 미만 예외는 034 개발자 모드 API를 읽는다. 병합 순서 030 다음
