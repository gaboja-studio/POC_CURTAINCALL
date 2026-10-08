# Unity Pitfalls

Unity 프로젝트 공통 함정. 상황에 맞는 파일 1개만 연다.

- [stale-values-after-recompile.md](stale-values-after-recompile.md) — 재컴파일 직후 값으로 수정 효과를 판단할 때
- [scene-not-saved.md](scene-not-saved.md) — 씬/프리팹 수정 후 "배치 완료"를 판단할 때
- [playmode-frame-freeze.md](playmode-frame-freeze.md) — AI가 Play Mode에서 코루틴·애니메이션을 검증하려 할 때
- [linerenderer-local-space.md](linerenderer-local-space.md) — LineRenderer 위치 값을 다른 좌표와 비교할 때
- [pipeline-command-drift.md](pipeline-command-drift.md) — Pipeline/CLI 업그레이드 후 또는 verify-unity가 명령 없음으로 실패할 때
- [mppm-virtual-player-status.md](mppm-virtual-player-status.md) — MPPM 가상 플레이어가 unity status에 같은 경로 에디터로 잡힘. 저장·검사가 "에디터 2개, 모호"로 멈출 때
- [editor-not-visible.md](editor-not-visible.md) — unity status가 에디터 0개를 반환할 때
- [multiple-editor-versions.md](multiple-editor-versions.md) — 프로젝트·worktree를 열거나 batch 실행할 때
- [yaml-trailing-whitespace.md](yaml-trailing-whitespace.md) — Unity YAML 줄 끝 공백으로 diff --check 실패. verify-fast·save-work가 trailing whitespace로 멈출 때
- [pipeline-socket-disposed-log.md](pipeline-socket-disposed-log.md) — Console의 ObjectDisposedException(BasePipelineServer)은 에디터 브리지 로그, 게임 오류 아님. 그 예외가 Console에 보일 때
- [material-color-float-churn.md](material-color-float-churn.md) — 재질(.mat) 색 값이 소수점만 바뀜(0.2 → 0.19999996). 재질을 안 고쳤는데 .mat이 git에 잡힐 때
- [unity-test-editor-open.md](unity-test-editor-open.md) — 에디터가 열려 있으면 unity test가 거부돼 check-work 테스트가 FAIL. 테스트 단계가 '테스트 결과 없음'으로 실패할 때
- [tmp-dynamic-font-churn.md](tmp-dynamic-font-churn.md) — TMP 동적 폰트 .asset이 저절로 수정됨(아틀라스 채움·비움). 폰트를 안 고쳤는데 SDF.asset이 git에 잡힐 때
- [resources-path-after-move.md](resources-path-after-move.md) — Resources 안 폴더 이동 후 Resources.Load 경로가 깨짐, 패키지 설정 에셋은 루트 고정. Resources 하위를 옮기거나 세팅이 기본값으로 돌 때
