# Task-20261014-037

- **Title:** 공용 조작 규칙 정리와 기본 이동 규칙 전환
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 외줄 코스 참고
- **Current Skill:** submit-work
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/common-control-rules

## Goal

- 공용 조작 코드(`Systems/`)가 외줄타기를 직접 알지 않는다. 조작 규칙을 끼우지 않은 공간은 **기본 이동 규칙**(현재 플랫폼 이동)으로 움직이고, 기획자가 에디터에서 기본 이동 규칙을 바꿀 수 있다. 어느 공간 UI가 열려도 키 입력이 막힌다. 외줄타기는 전과 똑같이 동작한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: `Assets/Scenes/Tests/Systems/ControlRules/ControlRules.unity` 배치 (2026-10-14 PM 예외 허용)

## Decisions

- Open: 기본 이동 규칙 선택을 두는 곳 — 제안대로 새 조작 설정 에셋 `Assets/Resources/Settings/Control/ControlSettings.asset`으로 구현(2026-10-14), PM 확인 대기
- Decided: 2026-10-14 PM: 정리 작업을 034(개발자 모드)와 병렬로 진행한다. 공용 문서: Confluence 19398663 6장
- Decided: 2026-10-14 PM: 외줄 플랫폼 이동을 공용 **기본 이동 규칙**으로 삼고, 기획자가 에디터에서 언제든 바꿀 수 있게 한다. 각 공간의 이동 규칙은 공용 코드를 고치지 않고 자기 폴더에서 만들어 연결할 수 있어야 한다
- Decided: 2026-10-14 PM: 균형(게이지·흔들림)은 일단 외줄타기 전용으로 둔다. 공용 기능으로 넓히지 않고, 이 Task에서 위치도 옮기지 않는다(참조가 030 소유 `NetworkPlayer`·`PlayerMover`에 걸려 있음)
- Decided: 2026-10-14 PM: 걷기 속도·점프 높이를 외줄 세팅에서 공통 세팅으로 옮기는 일은 `PlayerMover`·`BaseGameSettings` 소유인 030에 넣는다(이 Task 범위 밖)
