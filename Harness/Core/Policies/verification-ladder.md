# Verification Ladder

확인은 싼 것부터 계단식으로 한다. 아래 단계를 통과하지 못하면 위 단계로 올라가지 않는다.

| 단계 | 내용 | 수단 | 목표 시간 |
|---|---|---|---|
| 0 | 구역 검사 | `Scripts/verify-scope.ps1` (setup.md 작업 구역·PM 전용 경로·`.meta`) | 수 초 |
| 1 | 문서/규칙 점검 | `Scripts/verify-fast.ps1` | 수 초 |
| 2 | 컴파일 확인 | `Scripts/verify-unity.ps1 -Compile` | 1분 이내 |
| 3 | 자동 테스트 | `Scripts/check-work.ps1 -Mode EditMode` 또는 `-Mode PlayMode` | 수 분 |
| 4 | 실행 로그 확인 | Play Mode Console 오류 0, 핵심 로그 확인 | 수 분 |
| 5 | 공동 테스트 | 합치기 후 PM·작업자가 함께 플레이. 손맛·화면·재미 등 AI가 할 수 없는 것 | — |

## 규칙

- **커밋 전에는 최소 0~2단계(구역·문서·컴파일)까지** 확인한다. 연속된 `fix:` 커밋과 구역 밖 수정을 막기 위함이다.
- 제출(PR) 전에 0~4단계 결과를 `handoff.md`에 적는다. PR 본문 "검사 결과"에 자동으로 들어간다.
- 5단계 확인 항목은 구현 전에 `plan.md`의 공동 테스트 항목으로 **최대 3개** 합의한다(무엇을 하면 통과인지).
- 플레이와 무관한 변경은 EditMode로 충분하다. 플레이 내 동작을 검증하는 Task는 PlayMode 테스트를 필수로 선택하며, handoff에 실행한 모드를 기록한다. PlayMode 자동 테스트가 없으면 `NO_PROJECT_TESTS`이지 플레이 검증 PASS가 아니다.
- 복잡한 플레이 검증은 작업자에게 씬·입력 순서·예상 결과·결과 기록 방법을 전달하고 실제 실행 결과를 handoff의 4·5단계에 기록한다. 자동화 테스트만으로 수동 체감 검증을 대체하지 않는다.
- 에디터가 필수인데 연결되지 않으면 해당 단계를 `ENV_BLOCKED(이유)`로, 후속 단계는 `SKIPPED(선행 단계 차단)`으로 남기고 비성공 종료한다. 선행 실패 뒤에는 후속 단계를 실행하지 않는다.
- 시작 때 doctor, 변경 후 check-work, 저장 직전에는 현재 변경의 검증 상태를 확인한다. 오래된 검사 결과만으로 저장을 허용하지 않는다.
- 테스트가 아예 없으면 PASS가 아니라 `NO_PROJECT_TESTS`.
- 리플렉션 직접 호출, 상태 강제 주입 같은 합성 테스트는 3단계(로직)로만 기록한다. 실제 입력 검증으로 보고하지 않는다(`Harness/Engine/Unity/Policies/runtime-interaction.md`).
- 1회용 검증 코드를 짰다면 가능한 한 EditMode/PlayMode 테스트로 남긴다. 다음엔 테스트 한 번이면 끝나게 한다.

## 결과 표기

handoff의 Verification 칸에는 단계별로 `PASS` / `FAIL` / `SKIPPED(이유)` / `NO_PROJECT_TESTS` / `ENV_BLOCKED(이유)` 중 하나를 적는다.
