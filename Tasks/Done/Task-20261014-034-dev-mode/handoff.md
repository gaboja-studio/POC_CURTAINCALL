# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: done (PR #60 병합, 2026-10-14). 남은 작업 없음
- 동작하는 것: `Systems/DevMode/` 개발자 모드(에디터 메뉴 `Tools/CurtainCall/Dev Mode (1인 테스트)` / 빌드 `-devmode`), 최소 인원·최소 생존자 예외 API, DEV MODE 표시. 테스트 씬 `DevMode.unity` 유지
- 기록: [History/001-play-check.md](History/001-play-check.md), [History/002-ai-teammate-review.md](History/002-ai-teammate-review.md)
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/60

## 다음 할 일

1. 없음. 생존 2명 미만 실패 판정은 031이 `DevMode.MinSurvivors(2)`로 구현(031 meta에 연결 약속 기록). 테스트 AI 동료는 이후 별도 Task로 고려.

## PM이 확인할 것

- 확인 요청: 없음
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음 (`NetworkSessionManager.cs` 반납)

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: NO_PROJECT_TESTS (EditMode)
- 4 실행 로그: PASS (10-14) — History/001, Console 오류 0
- 5 공동 테스트: PASS (10-14, 생존 판정 실제 확인은 031) — 통합 `checklist.md`
