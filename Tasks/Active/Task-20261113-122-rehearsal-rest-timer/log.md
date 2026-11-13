# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(선행 실패·생략)

## 2026-11-13 구현
- 멈춤 사유를 구역과 리허설로 나눔(RunState.zonePaused/rehearsalHold). 리허설이 멈춘 타이머는 구역 규칙이 다시 흘리지 못함.
- 인간대포는 종료 연출 뒤 원위치 복귀가 끝나 shared.active=false가 될 때 풀림. 외줄 재연습은 practice=false가 될 때 풀림.
- 새 문구 3개(reminder-title/body, pursuit)를 WaitingRoomTexts.txt와 WaitingRoomHud.asset에 추가함.
- 검사: check-work는 Task-115 log.md 길이(HARD, 이번 작업과 무관)로 문서 단계에서 실패해 컴파일·테스트를 생략함. Unity에서 직접 다시 컴파일함(PASS). EditMode BreakFlow 43/43, Rehearsal 226/226 PASS. 새 테스트 2개를 추가함(RestTimeRulesTests). 실제 플레이는 확인하지 않음.
- 저장 시 Task-115 log.md(154줄, HARD)가 막아 사용자 승인으로 오래된 기록을 History/004로 원문 그대로 옮김(삭제 없음).

- 2026-11-13 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/359 (fix/rehearsal-rest-timer → dev)
- 제출: [PR #359](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/359) → dev. 다음: 지정 팀원 검토, 2인 플레이 확인.

## 2026-11-13 후속: 거절 뒤 다음 시간대 재안내 안 뜸
- 사용자 보고: 팝업 거절 → 공연 → 다음 쉬는 시간 진입 때 호스트 팝업이 다시 안 뜸.
- 원인: 재안내 조건이 RestTime만 봄. 1일차 가이드 쉬는 시간에 해금·거절하면 다음 공연 뒤는 밤 정비(Night)라 대기열에 들어가지 않음.
- 수정: 밤 정비 시작도 포함(PracticePhase), 키에 Phase 추가. 재컴파일 PASS, CannonRehearsal EditMode 1/1 PASS. 실제 플레이 재확인 필요.
