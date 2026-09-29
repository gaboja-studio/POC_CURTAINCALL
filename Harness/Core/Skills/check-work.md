---
name: check-work
description: 작업자의 검사 요청. 구역·문서·컴파일·테스트를 차례로 검사하고 결과를 handoff에 기록한 뒤 쉬운 말로 보고한다.
---

# Check Work

기준: `Harness/Core/Policies/verification-ladder.md`

1. Unity에서 저장했는지 확인한다.
2. `pwsh -File Scripts/check-work.ps1`
   - 0 구역 → 1 문서 → 2 컴파일 → 3 테스트를 실행하고 결과를 `handoff.md`의 Verification에 적는다.
   - Unity 에디터가 연결되어 있지 않으면 2·3단계는 `ENV_BLOCKED`로 적힌다. 에디터를 열고 다시 할지 사용자에게 묻는다.
3. 결과를 쉬운 말로 보고한다. 예:
   - "✅ 내 구역만 고쳤어요 / ✅ 오류 없음 / ⚠️ 자동 테스트가 없어요(테스트 없음)"
   - 실패가 있으면 무엇이 문제인지와 다음 행동 하나를 제안한다(예: "Console 오류를 같이 볼까요?" → `investigate-bug`).
4. 4단계(실행 로그)는 사용자가 Play로 확인한 내용을 물어서 적는다. 확인하지 않았으면 "미실행"으로 둔다.
5. 테스트가 없을 때 PASS라고 말하지 않는다.
