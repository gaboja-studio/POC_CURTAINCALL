# Verification Ladder

확인은 싼 것부터 계단식으로 한다. 아래 단계를 통과하지 못하면 위 단계로 올라가지 않는다.

| 단계 | 내용 | 수단 | 목표 시간 |
|---|---|---|---|
| 0 | 구역 검사 | `Scripts/verify-scope.ps1` (setup.md 작업 구역·PM 전용 경로·`.meta`) | 수 초 |
| 1 | 문서/규칙 점검 | `Scripts/verify-fast.ps1` | 수 초 |
| 2 | 컴파일 확인 | `Scripts/verify-unity.ps1 -Compile` | 1분 이내 |
| 3 | 자동 테스트 | `Scripts/verify-unity.ps1 -RunTests` (EditMode → PlayMode) | 수 분 |
| 4 | 실행 로그 확인 | Play Mode Console 오류 0, 핵심 로그 확인 | 수 분 |
| 5 | 공동 테스트 | 합치기 후 PM·작업자가 함께 플레이. 손맛·화면·재미 등 AI가 할 수 없는 것 | — |

## 규칙

- **커밋 전에는 최소 0~2단계(구역·문서·컴파일)까지** 확인한다. 연속된 `fix:` 커밋과 구역 밖 수정을 막기 위함이다.
- 제출(PR) 전에 0~4단계 결과를 `handoff.md`에 적는다. PR 본문 "검사 결과"에 자동으로 들어간다.
- 5단계 확인 항목은 구현 전에 `plan.md`의 공동 테스트 항목으로 **최대 3개** 합의한다(무엇을 하면 통과인지).
- 테스트가 아예 없으면 PASS가 아니라 `NO_PROJECT_TESTS`.
- 리플렉션 직접 호출, 상태 강제 주입 같은 합성 테스트는 3단계(로직)로만 기록한다. 실제 입력 검증으로 보고하지 않는다(`Harness/Engine/Unity/Policies/runtime-interaction.md`).
- 1회용 검증 코드를 짰다면 가능한 한 EditMode/PlayMode 테스트로 남긴다. 다음엔 테스트 한 번이면 끝나게 한다.

## 결과 표기

handoff의 Verification 칸에는 단계별로 `PASS` / `FAIL` / `SKIPPED(이유)` / `NO_PROJECT_TESTS` / `ENV_BLOCKED(이유)` 중 하나를 적는다.
