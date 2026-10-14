# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 제출 완료(2026-10-14, PR #59). 033 이전 결과를 합친 뒤 재검사 통과.
- 동작하는 것:
  - 세팅 5묶음: 공통 `BaseGameSettings`(+ 런 진행·기물 소지), 묘기 `Tricks/*`, 빈 틀 `WaitingRoom`·`Exploration`·`EscapeRoute` Settings(에셋 생성·`GameSettings` 연결 완료). 공통 값: 3일·하루 2공연·1일차 `Tightrope`→`HumanCannon`·타이머 B안(A/B)·쉬는 시간 300초·가이드 300초(임시)·경보 60초·기물 2/1/+2·최소 인원 2.
  - 런 진행 `Systems/Campaign/`: `RunState`(순수 판정) → `CampaignRun`(공개 진입점, 호스트 판정·named message 공유, 방 Playing 시 세대 1 시작, 엔딩 시 `EndGame`) → `CampaignFlowTestUI`(디버그 버튼, 오프라인 런 가능).
  - 신호(호스트): `EndIntro`·`EndPerformance`·`NotifyLeftWaitingRoom`·`NotifyAllAtStageEntrance`·`StartEscape`·`EndEscape(bool)`·`SetSetlist`, 누구나 `SetReady`. 이벤트: `StepChanged`·`RecallAlarm`·`RunEnded`·`Changed`.
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/59

## 다음 할 일

1. PR #59 리뷰·병합(PM) → 합치기 ①에서 2단계 Task와 시간대 신호 형식 확정. 리뷰 수정이 있으면 이 브랜치에서 고친다.

## PM이 확인할 것

- 확인 요청:
  - B안 타이머 = **누군가 처음** 대기실에서 나온 순간 시작하는 팀 공용 타이머로 해석.
  - B안에서 아무도 안 나오면 타이머가 안 흐름 → **4일차 마지막 아침에 대기실에만 있으면 런 실패가 오지 않음.** 기획 판단 필요.
  - 최소 인원 2 → 개발자 모드(CC-118) 전까지 접속 1인 시작 불가(오프라인 런은 가능).
  - 1일차 묘기 이름은 임시 문자열. 묘기 식별 방식은 031·공연장과 합의 필요.
- 대기 중인 결정: 시간대 전환 신호 형식(meta Open) — `CampaignRun` 메서드 + `StepChanged` 이벤트로 제안, 031·035·032 시작 전 확정.
- 공용 파일 요청: `Docs/Domains/common-game-systems.md` — 2026-10-14 PM 허용, setup.md 배정 공용 파일에 추가. 033도 같은 파일을 고치므로 합칠 때 확인

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: NO_PROJECT_TESTS (EditMode)
- 4 실행 로그: PASS (2026-10-14 작업자: 테스트 씬에서 공동 테스트 1~3 확인 "문제 없음", 2인 접속 확인은 보고 없음)
- 5 공동 테스트: 미요청
