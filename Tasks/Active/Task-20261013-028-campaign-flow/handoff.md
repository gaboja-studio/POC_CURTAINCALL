# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: integrated (#59 병합, 2026-10-14). 공동 테스트 1~3 PASS
- 동작하는 것:
  - 세팅 5묶음: 공통 `BaseGameSettings`(+ 런 진행·기물 소지), 묘기 `Tricks/*`, 빈 틀 `WaitingRoom`·`Exploration`·`EscapeRoute` Settings(에셋 생성·`GameSettings` 연결 완료). 공통 값: 3일·하루 2공연·1일차 `Tightrope`→`HumanCannon`·타이머 B안(A/B)·쉬는 시간 300초·가이드 300초(임시)·경보 60초·기물 2/1/+2·최소 인원 2.
  - 런 진행 `Systems/Campaign/`: `RunState`(순수 판정) → `CampaignRun`(공개 진입점, 호스트 판정·named message 공유, 방 Playing 시 세대 1 시작, 엔딩 시 `EndGame`) → `CampaignFlowTestUI`(디버그 버튼, 오프라인 런 가능).
  - 신호(호스트): `EndIntro`·`EndPerformance`·`NotifyLeftWaitingRoom`·`NotifyAllAtStageEntrance`·`StartEscape`·`EndEscape(bool)`·`SetSetlist`, 누구나 `SetReady`. 이벤트: `StepChanged`·`RecallAlarm`·`RunEnded`·`Changed`.
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/59

## 다음 할 일

1. 2단계(034 먼저, 030·031·035) Task에 시간대 신호 사용법(`CampaignRun`) 안내.
2. 공통 통합 마무리 때 Task 폴더 Done 이동, 테스트 씬 처리 결정 기록.

## PM이 확인할 것

- 확인 요청: 2026-10-14 PM 답변으로 정리됨(run-rules.md #6~8) — B안 타이머 해석 확정, 마지막 아침은 대기실에서 나와야 진행(플레이테스트 후 재논의 가능), 최소 인원 2 유지·034 먼저. 묘기 이름 임시 문자열은 031·공연장과 묘기 식별 방식 정할 때.
- 대기 중인 결정: 없음 (시간대 신호 형식 확정)
- 공용 파일 요청: `Docs/Domains/common-game-systems.md` — 2026-10-14 PM 허용, setup.md 배정 공용 파일에 추가. 033도 같은 파일을 고치므로 합칠 때 확인

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: NO_PROJECT_TESTS (EditMode)
- 4 실행 로그: PASS (2026-10-14 작업자: 테스트 씬에서 공동 테스트 1~3 확인 "문제 없음", 2인 접속 확인은 보고 없음)
- 5 공동 테스트: PASS (10-14, #59 병합 후 checklist 기록)
