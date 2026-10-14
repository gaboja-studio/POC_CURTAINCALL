# Task-20261014-034

- **Title:** 1인 테스트용 개발자 모드
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 네트워크 세션 참고
- **Current Skill:** implement-code
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/dev-mode

## Goal

- 개발자 모드를 켜면 1명으로 게임을 시작하고 공연을 넘길 수 있으며(최소 인원 2명·생존 2명 미만 실패 예외), 끄면 정식 규칙대로 막힌다. 테스트 AI 동료 가능성 검토 결과가 handoff에 남는다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 없음
- Decided: 2026-10-14 PM: 개발자 모드를 먼저 만들고 테스트 AI는 검토만 한 뒤 이후에 고려한다. 빌드 기본은 꺼짐. 통합 계획 2D(선행: 028 세팅 틀)
- Decided: 2026-10-14 PM: 개발자 모드 스위치는 세팅 파일이 아니라 `Systems/DevMode/`에 둔다(디버그는 세팅에 넣지 않는다, game-settings.md #5). 031이 생존 2명 미만 예외에 읽을 공개 API를 둔다. 2단계 맨 먼저 병합
- Decided: 2026-10-14 PM: 켜는 방법 = 에디터 메뉴 토글(EditorPrefs, 개인 PC에만 저장) + 빌드는 실행 인자 `-devmode`일 때만. 게임 진행 중 변경 불가, 켜져 있으면 화면에 DEV MODE 표시. 예외 규칙은 인원 규칙 2개만: ① 시작 최소 인원 → 1명(`NetworkSessionManager.MinPlayers`) ② 묘기 실패 기준 생존 2명 미만 → 생존 0명(031이 `DevMode.MinSurvivors(정식값)`을 읽음). 예외는 호스트 판정에만 적용
