---
name: verify-unity
description: Unity 컴파일·테스트·에디터 상태를 안전하게 검증하고 결과를 정직하게 보고한다.
---

# Verify Unity

1. `pwsh -File Scripts/verify-unity.ps1 -ProjectPath <task-worktree>`로 읽기 전용 상태부터 확인한다.
   - 에디터 연결, 경로 일치, Play Mode 정지, 열린 씬 clean 여부.
2. 컴파일이 필요하면 `-Compile`을 추가한다(격리된 AI worktree에서만).
3. 테스트가 있으면 `-RunTests`를 추가한다. `-Mode EditMode|PlayMode`, `-Filter <이름>`으로 좁힐 수 있다.
4. 테스트 0개는 `NO_PROJECT_TESTS`로 보고한다. PASS가 아니다.
5. 테스트 실패 / 컴파일 실패 / 시간 초과 / 인증 실패 / 에디터 없음을 구분해 보고한다.
6. 재컴파일 직후 값은 옛 값일 수 있다(`Pitfalls/stale-values-after-recompile.md`).
7. 결과 파일은 무시되는 `.harness/local/`에 두고 handoff에는 요약만 적는다.

## 상호작용 기능

Click/Drag/Drop/Touch/UI가 포함되면 결과를 Logic / Runtime Integration / Human Interaction으로 나눠 적는다.
내부 메서드 직접 호출 테스트를 실제 입력 PASS로 보고하지 않는다(`Policies/runtime-interaction.md`).
