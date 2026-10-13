# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 최신 기획(Confluence 게임 흐름·부상·장소·조작, 2026-10-13) 반영 재조사 완료, 검사 전
- 동작하는 것: `Docs/Domains/common-game-systems.md` 재작성(재사용 목록 4묶음, 1차 Open → 기획 답 매칭 8건, 남은 판단 7건). 하네스 갱신: Facts game-flow·run-state(신규)·overview·unity-project·team, Decisions injury-rules(신규)·control-keys(신규), body-damage·tightrope-course-rules에 대체 표시, space-workspaces 하위 구역
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. PM 검토 → "검사해줘" → "제출해줘"(`integration/common-game-systems`)
2. 병합 뒤 통합 브랜치에서 신규 Task 3개 생성(공통 폴더 이전·더미 공간·개발자 모드, JIRA 발급 필요), 028·030·031·032 plan을 통합 계획 기준으로 갱신

## PM이 확인할 것

- 확인 요청: 작업 구역을 Harness/Project 12개 파일로 넓힘(2026-10-13 PM 지시, setup.md 기록)
- 대기 중인 결정: 7건은 2026-10-14 PM 결정으로 기록(run-rules.md 신규, game-settings·injury-rules 갱신, 도메인 문서 "PM 결정" 표). 공통 폴더 이전(#4)은 보류. 1인 테스트(개발자 모드)·로비 재입장 허용도 기록. 작업 순서는 `Integrations/Active/Integration-common-game-systems/plan.md`
- 공용 파일 요청: 없음

## Verification

- 구역 검사: OK (20 files)
- 1 문서/규칙: Fast OK (크기 WARN 3: body-damage.md 42줄 + 기존 2건)
- 2 컴파일: 해당 없음 (코드 변경 없음)
- 3 테스트: 해당 없음 (조사 Task)
- 4 실행 로그: 해당 없음
- 5 공동 테스트: 미요청
