# Unity Pitfalls

Unity 프로젝트 공통 함정. 상황에 맞는 파일 1개만 연다.

- [stale-values-after-recompile.md](stale-values-after-recompile.md) — 재컴파일 직후 값으로 수정 효과를 판단할 때
- [scene-not-saved.md](scene-not-saved.md) — 씬/프리팹 수정 후 "배치 완료"를 판단할 때
- [playmode-frame-freeze.md](playmode-frame-freeze.md) — AI가 Play Mode에서 코루틴·애니메이션을 검증하려 할 때
- [linerenderer-local-space.md](linerenderer-local-space.md) — LineRenderer 위치 값을 다른 좌표와 비교할 때
- [pipeline-command-drift.md](pipeline-command-drift.md) — Pipeline/CLI 업그레이드 후 또는 verify-unity가 명령 없음으로 실패할 때
- [editor-not-visible.md](editor-not-visible.md) — unity status가 에디터 0개를 반환할 때
- [multiple-editor-versions.md](multiple-editor-versions.md) — 프로젝트·worktree를 열거나 batch 실행할 때
