---
name: check-work
description: 작업자의 검사 요청. 구역·문서·컴파일·테스트를 차례로 검사하고 결과를 handoff에 기록한 뒤 쉬운 말로 보고한다.
---

# Check Work

기준: `Harness/Core/Policies/verification-ladder.md`

1. Unity에서 저장했는지 확인한다.
2. 플레이와 무관한 Task는 `pwsh -File Scripts/check-work.ps1 -Mode EditMode`, 플레이 내 검증이 필요한 Task는 `-Mode PlayMode`를 사용한다.
   - 0 구역 → 1 문서 → 2 컴파일 → 3 선택한 모드의 테스트를 순서대로 실행해 `handoff.md`에 기록한다. 실패 뒤 후속 단계는 사유와 함께 `SKIPPED`한다.
   - Unity 에디터가 필수인데 연결되지 않으면 컴파일은 `ENV_BLOCKED`, 테스트는 `SKIPPED`이며 비성공 종료한다. 에디터에서 해당 프로젝트를 열고 연결을 확인한 뒤 재검사한다.
3. 결과를 쉬운 말로 보고한다. 예:
   - "✅ 내 구역만 고쳤어요 / ✅ 오류 없음 / ⚠️ 자동 테스트가 없어요(테스트 없음)"
   - 실패가 있으면 무엇이 문제인지와 다음 행동 하나를 제안한다(예: "Console 오류를 같이 볼까요?" → `investigate-bug`).
4. 플레이 검증이 단순 확인을 넘으면 작업자에게 **씬, 입력 순서, 예상 결과, 결과 기록 방법**을 구체적으로 전달한다. 실제 플레이 로그와 공동 테스트 결과를 handoff의 4·5단계에 적고, 확인하지 않았다면 `SKIPPED(미실행)`으로 둔다.
5. 선택한 모드에 테스트가 없으면 `NO_PROJECT_TESTS`이며 플레이 검증을 PASS로 말하지 않는다.
