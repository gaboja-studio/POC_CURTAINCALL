# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-04 검사: 안전성=FAIL(새 스크립트 .meta 없음 → 추가) ; 문서/규칙=SKIPPED; 컴파일=SKIPPED; 테스트=SKIPPED
- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 에디터 미실행, Pipeline 인스턴스 없음); 테스트=SKIPPED(선행 실패·생략)
- 2026-11-04 origin/dev(#286·#287) 반영: 에디터 종료 → stash → fast-forward → stash pop, 충돌 없음. dev에서 `WaitingRoomTutorial.TodoVisible`이 제거되어 `PursuitQuest`의 최소 높이 조정도 제거(dev 첫날 퀘스트와 같은 방식).
- 2026-11-04 최소 검증(에디터 Pipeline): 컴파일=PASS. EditMode `PursuitQuestTextTests` 2/2 PASS.
  - `WaitingRoomTextTests` 3/5: 실패 2개는 이번 작업과 무관한 기존 상태(리허설·첫날 키 미사용 판정, `explore.locked-before-tutorial` 파일 누락). `pursuit.*` 키는 걸리지 않음.
- 2026-11-04 추격 설계 창에서 문구 표 직접 편집 추가(`PursuitTextTable`). 컴파일=PASS. 에디터 eval로 읽기·쓰기·원복·단계 다시 쓰기를 실행해 파일이 바이트 단위로 원래대로 돌아오는 것 확인(git diff 동일).
- 다음: 사람 플레이 확인(todo 마지막 항목).
