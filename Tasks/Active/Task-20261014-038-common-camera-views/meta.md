# Task-20261014-038

- **Title:** 공용 1인칭·3인칭 카메라 시점과 전환
- **Type:** feat
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 외줄 코스·플레이어 동기화 참고
- **Current Skill:** merge-integration
- **Updated:** 2026-10-15

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/common-camera-views

## Goal

- 1인칭·3인칭 시점이 공용 카메라 기능으로 있고, 각 공간이 공용 코드를 고치지 않고 씬에 놓는 규칙(구역 진입·상황 등)으로 시점을 바꿀 수 있다. 외줄타기 화면 구도·관전·가림 흐리기는 전과 똑같다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 없음. 10-15 직접 editor_status·열린 씬 조회로 Task 경로 ready/dirty 0 확인. CLI beta.6 status 검색과 불일치, 지원 명령으로 진행
- Decided: 2026-10-15 PM: 구체 구현 계획 승인(단일 리그·시점 구역·로컬 모델 렌더 숨김·외줄 요청화·resolver 테스트). 초기값·관전·커서·테스트 씬 구성은 `plan.md`
- Decided: 2026-10-14 PM: 시점 요청은 높은 숫자 우선순위 → 같은 순위면 최근 요청을 적용. 해제하면 남은 요청 또는 기본 시점으로 복귀
- Decided: 2026-10-14 PM: 즉시 전환 기본, 시점 에셋의 전환 시간으로 부드러운 전환 선택 가능. 외줄은 기존 즉시 적용 유지
- Decided: 2026-10-14 PM: 카메라는 1인칭/3인칭 시점 로직을 모두 공용으로 만든다. 나중에 일정 규칙에 따라 각 공간의 특정 상황에서 시점을 쉽게 바꿀 수 있어야 한다. 034(개발자 모드)·037(조작 정리)과 병렬
- Decided: 2026-10-14 PM: 외줄 전용 카메라 구도(`TightropeCameraFraming`)는 공연장으로 옮기고, 공용 가림 흐리기는 외줄 코스·세팅을 직접 읽지 않게 한다(Confluence 19398663 6장)
- Decided: 2026-10-14 PM: 리타이어(`PlayerState.Retired`, 탈출로 사망·잡힘, Task-030 추가)한 사람도 사망자처럼 관전한다(`CameraSpectator`가 `Fallen`만 보는 것을 `Retired`까지). 관전 대상(살아 있는 사람)은 지금처럼 `Normal`만
