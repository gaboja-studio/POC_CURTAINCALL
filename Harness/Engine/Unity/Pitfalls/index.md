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
- [unity-test-editor-open.md](unity-test-editor-open.md) — (해결 2026-10-14) batch unity test는 열린 에디터와 충돌 → verify-unity가 에디터 Pipeline run_tests로 실행. 테스트 단계가 실패하거나 테스트 실행 방식을 바꿀 때
- [tmp-dynamic-font-churn.md](tmp-dynamic-font-churn.md) — TMP 동적 폰트 .asset이 저절로 수정됨 → 한글 폰트 Static으로 구워 해결, 새 글자는 Bake Static Fonts 메뉴. 폰트 .asset이 git에 잡히거나 □·글자 없음 경고가 날 때
- [resources-path-after-move.md](resources-path-after-move.md) — Resources 안 폴더 이동 후 Resources.Load 경로가 깨짐, 패키지 설정 에셋은 루트 고정. Resources 하위를 옮기거나 세팅이 기본값으로 돌 때
- [urp-light-data-churn.md](urp-light-data-churn.md) — URP 씬을 열면 Light·Camera에 추가 데이터 컴포넌트가 저절로 붙어 .unity가 바뀜. 열어 보기만 한 씬이 git에 잡힐 때
- [mppm-clone-missing-files.md](mppm-clone-missing-files.md) — MPPM 복제본(Library/VP/)이 새 폴더·asmdef를 놓쳐 가상 플레이어가 "Could not read file …asmdef"로 꺼짐. 2번 플레이어 창이 시작하자마자 꺼질 때
- [additive-scene-swap-order.md](additive-scene-swap-order.md) — 추가 로드로 씬을 바꿀 때 새 씬을 먼저 붙이면 EventSystem 등이 겹침, 이전 씬을 닫고 붙임. Additive 씬 전환을 만들거나 EventSystem 2개 경고가 뜰 때
- [overlay-ui-capture.md](overlay-ui-capture.md) — 카메라 캡처에 Overlay UI가 빠짐, capture_game_view source=screen 사용. AI가 UI를 캡처해 확인할 때
