# Task-20261013-031

- **Title:** 공연 결과 및 보상, 공동 자금 연결
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`)
- **Current Skill:** merge-integration
- **Updated:** 2026-10-16

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

- Open: 정식 환호·야유·생존/절단 보너스 수치(보상 시스템 기획 전)
- Decided: 2026-10-15 사용자: 이전 씬 테스트로 공동 테스트 완료 승인. #67 통합 병합 확인 후 integrated 처리, 별도 다중 접속 실행 증거는 미보고
- Decided: 2026-10-14 사용자 승인 검증용: 환호 +10, 야유 −5, 난이도 상한 100/150/200, 생존/절단 보너스 0. Inspector 및 런타임 override로 변경 가능
- Decided: 2026-10-15 구현 계획 승인: 명부 고정·이탈 지급/생존 제외·보너스 상한 포함·진행 중 상한 하향 clamp. 공유는 호스트 송신 enqueue 후 전환이며 클라이언트 도착 순서는 가정하지 않음
- Decided: 2026-10-14 PM: 생존 2명 미만 = 묘기 공통 실패(개발자 모드 034가 예외), 공동 자금은 팀 공용 잔액. 상점·구매 UI는 범위 밖. 선행: 029, 033, 028 세팅 틀. 통합 계획 2B
- Decided: 2026-10-14 PM: 033은 결과 명부를 옮기지 않음 → 031이 `Systems/Results/`(작업 구역)에 묘기 공통 공연 결과를 새로 만든다. 외줄 결과 코드는 고치지 않고 4단계 연결 때 교체
- Decided: 2026-10-14 PM: 묘기가 종료를 알리면 031이 결과(성공/실패·종료 이유·사망자·팔다리 변화·정산액)를 확정·공유하고 그다음 `CampaignRun.EndPerformance()`를 부른다(결과 먼저 → 시간대 전환). 생존 2명 미만 예외는 034 개발자 모드 API를 읽는다. 병합 순서 030 다음
- Decided: 2026-10-14 PM: 034 연결(#60 병합) — 묘기 실패 = `생존자 수 < DevMode.MinSurvivors(2)`(`CurtainCall.Dev`). 정식값 2는 031이 가진다. 개발자 모드면 1(전원 사망 때만 실패)
