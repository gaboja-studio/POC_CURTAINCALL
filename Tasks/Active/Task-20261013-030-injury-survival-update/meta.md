# Task-20261013-030

- **Title:** 부상 및 생존 상태 최신 기획 반영
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 플레이어 동기화 참고
- **Current Skill:** implement-code
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/injury-survival-update

## Goal

- 잃은 팔다리가 고정 절단 순서(왼다리 → 오른다리 → 왼팔 → 오른팔)로 늘고, 사망(4곳·추락·외팔로 잡힘)하면 공연 끝까지 관전한 뒤 다음 시간대 시작 때 대기실에서 외팔 상태로 부활한다.
- 밤 신체 복구(역순·개당)와 세대 교체 시 초기화, 점프 높이 감소·탐색 중 부상 패널티 교체, 팔 개수별 기물 소지 칸 수를 테스트 씬에서 확인할 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: `Assets/Scenes/Tests/Systems/Survival/InjurySurvival.unity` 테스트용 배치(2026-10-14 PM 위임)

## Decisions

- Decided: 2026-10-14 PM: 외줄이 쓰는 `ServerCutPart`·`ServerCutLegThenArm`은 이름·동작 그대로 두고 "외줄 전용, 4단계 교체" 표시만 한다. 공통 고정 순서 절단은 새 함수 `NetworkPlayer.ServerCut(개수)`
- Decided: 2026-10-14 PM: 장소별 규칙은 호스트의 현재 장소 값(`SurvivalRules.ServerPlace`: 공연/탐색/탈출로)으로 사망·잡힘 결과를 나누고, 기존 `ServerCanChangeState`는 "묘기 안에서 지금 다칠 수 있나" 판단으로 그대로 함께 본다(외줄 코드 수정 없음)
- Decided: 2026-10-14 PM: 부상 기획(injury-rules.md)이 기준, 기물 소지 기본 2/1/+2는 세팅 값. 추격자 등 부상 원인 이벤트와 공간 연결은 032·035. 선행: 029, 033(공통 폴더 이전), 028 세팅 틀. 통합 계획 2A
- Decided: 2026-10-14 PM: 부활은 030이 `CampaignRun.StepChanged`를 직접 받아 처리(쉬는 시간·밤 정비 시작 = 사망자 외팔 부활, 세대 2+ 도입 = 전원 복구). 외팔 설정 함수는 공개(035·032 호출), 대기실 이동은 032. 탐색 중 부상 패널티 값은 공통 세팅 신체 손상 묶음. 병합 순서 034 다음
- Decided: 2026-10-14 PM: 걷기 속도·점프 높이를 외줄 세팅(`Rope.MoveSpeed`·`Rope.JumpHeight`)에서 공통 세팅으로 옮기는 일을 이 Task에 추가한다(`PlayerMover`·`BaseGameSettings` 소유). 값은 그대로, 옆줄 간격 등 줄 위 전용 값은 외줄 세팅에 남긴다. 균형은 외줄 전용 유지(037·038과 병렬, Confluence 19398663 6장)
