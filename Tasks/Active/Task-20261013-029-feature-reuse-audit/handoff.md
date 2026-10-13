# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 최신 기획(Confluence 게임 흐름·부상·장소·조작, 2026-10-13) 반영 재조사 완료, 검사 전
- 동작하는 것: `Docs/Domains/common-game-systems.md` 재작성(재사용 목록 4묶음, 1차 Open → 기획 답 매칭 8건, 남은 판단 7건). 하네스 갱신: Facts game-flow·run-state(신규)·overview·unity-project·team, Decisions injury-rules(신규)·control-keys(신규), body-damage·tightrope-course-rules에 대체 표시, space-workspaces 하위 구역
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. PM 검토 → "검사해줘" → "제출해줘"(`integration/common-game-systems`)
2. (완료 10-14) 신규 Task 033·034·035 생성(CC-117·118·119), 028·030·031·032 문서 최신화, 028·030·031·033·034·035 인계. 032는 3단계 인계 대기

## PM이 확인할 것

- 확인 요청: 작업 구역을 Harness/Project 12개 파일로 넓힘(2026-10-13 PM 지시, setup.md 기록)
- 대기 중인 결정: 7건은 2026-10-14 PM 결정으로 기록(run-rules.md 신규, game-settings·injury-rules 갱신, 도메인 문서 "PM 결정" 표). 공통 폴더 이전은 Task-033으로 분리. 1인 테스트(개발자 모드)·로비 재입장 허용도 기록. 작업 순서는 `Integrations/Active/Integration-common-game-systems/plan.md`
- 공용 파일 요청: 없음

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: NO_PROJECT_TESTS (EditMode·PlayMode 0개, 10-14 에디터 `list_tests`·`run_tests`로 확인). check-work의 batch 테스트 실행은 같은 프로젝트를 에디터가 열고 있어 시작하지 못함(ENV: "another Unity instance is running")
- 4 실행 로그: 해당 없음
- 5 공동 테스트: 미요청
