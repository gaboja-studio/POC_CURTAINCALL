# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 제출 완료(submitted), PR 병합 대기
- 동작하는 것: 조정값이 `Assets/Resources/GameSettings/`(GameSettings → BaseGameSettings, Tricks/TightropeSettings)에서 읽힘. 플레이 중 수정 즉시 반영, 코스 모양은 대기 중 즉시·게임 중 수정은 잠금 풀릴 때. Player·코스·NetworkManager 프리팹 숫자 칸 삭제. 캡슐 높이·지름은 CharacterController 칸이 남지만 실행 시 세팅 값으로 덮어씀
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/19

## 다음 할 일

1. PM: PR #19 병합 → 공동 테스트 1~3 → 1단계 갈래(012·011) 시작(세팅을 먼저 써야 하므로 이 PR이 먼저)
2. PM: Domain Map 갱신 — `Docs/Domains/tightrope-course.md` 조정값 표 "지금 위치"를 세팅 파일로, `player-control.md`에 `Assets/Scripts/Settings/` 언급(이 Task 구역 밖)

## PM이 확인할 것

- 확인 요청: 플레이 중 세팅 인스펙터에서 바꾼 값은 플레이를 멈춰도 에셋에 남는다(ScriptableObject) — 기획자 안내 필요. `Assets/Scenes/Tests/*/Prefabs/` 빈 폴더 정리는 이 Task에서 되돌림, 별도 작업 + `code-folders.md` 규칙 수정 필요
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음

## Verification

- 구역 검사: PASS (10-03)
- 1 문서/규칙: PASS (10-03)
- 2 컴파일: PASS (10-03)
- 3 테스트: NO_PROJECT_TESTS (10-03) — 연결된 에디터 `list_tests` 결과 0개. check-work의 배치 실행은 에디터가 열려 있어 실행 불가(환경 제약)
- 4 실행 로그: Tightrope 씬 Play — 경고·오류 없음, 세팅 에셋 로드 확인(대체값 아님), 플레이 중 줄 수 4→3→4 변경 시 코스 재생성 확인 (10-03, AI)
- 5 공동 테스트: 작업자 직접 플레이로 1~3 확인 — "잘 되는 것 같아" (10-03 @MoHoDu). 병합 후 공동 테스트는 미실행
