# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 구현·검사 완료, 저장(커밋)·제출 전
- 동작하는 것:
  - `Systems/DevMode/DevMode.cs` 공개 API: `Enabled`, `MinPlayers(정식값)`, `MinSurvivors(정식값)`(켜지면 1, 꺼지면 정식값).
  - 켜기: 에디터 메뉴 `Tools/CurtainCall/Dev Mode (1인 테스트)`(이 PC에만 저장, Play 중 잠김) / 빌드는 실행 인자 `-devmode`. 기본 꺼짐. 켜지면 `DevModeBadge`가 화면 오른쪽 위에 DEV MODE 표시.
  - `NetworkSessionManager.MinPlayers` 한 줄 변경 → 시작·재시작 판정과 접속 UI가 모두 예외를 따른다.
  - 테스트 씬 `DevMode.unity`: 접속·런 진행·개발자 모드(`DevModeTestUI`) 화면.
  - 플레이 확인: [History/001-play-check.md](History/001-play-check.md) (꺼짐 1명 거절, 켜짐 1명 시작·2일차까지 진행, 오류 0)
  - 테스트 AI 동료 검토: [History/002-ai-teammate-review.md](History/002-ai-teammate-review.md) (기반은 공용 5~8파일 중간 규모 + 묘기별 행동, 별도 Task 권장)
- 막힌 것: 없음. 생존 2명 미만 실패 판정은 031 담당이라 여기는 API와 테스트 UI 흉내까지.
- PR: 없음

## 다음 할 일

1. 사람이 Unity에서 메뉴 토글·DEV MODE 표시를 직접 한 번 확인 → "저장해줘" → "제출해줘"(2단계 맨 먼저 병합).
2. 031에 연결 약속 전달: 묘기 실패 = `생존자 수 < DevMode.MinSurvivors(2)`(정식값 2는 031 소유).

## PM이 확인할 것

- 확인 요청: 031 연결 약속 전달, 테스트 AI 동료 별도 Task 여부
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음 (배정된 `NetworkSessionManager.cs` 한 줄만 수정)

## Verification

- 구역 검사: PASS (10-14)
- 1 문서/규칙: PASS (10-14)
- 2 컴파일: PASS (10-14)
- 3 테스트: NO_PROJECT_TESTS (EditMode)
- 4 실행 로그: PASS (10-14) — History/001, Console 오류 0
- 5 공동 테스트: 미요청
