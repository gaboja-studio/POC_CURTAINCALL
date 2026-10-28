# Project Pitfalls

- [Telemetry/](Telemetry/index.md) — 로그·설문의 연결 종료와 최초 실행 시점을 확인할 때

- [death-ragdoll-reset.md](death-ragdoll-reset.md) — 공연 사망 뒤 조작 정리가 레그돌을 복구할 때

- [session-join-cancelled.md](session-join-cancelled.md) — 방 코드·LAN 참가의 취소 예외와 제한시간·취소 원인 로그를 확인할 때

이 POC·이 개발 환경에서 겪은 함정. 상황에 맞는 파일 1개만 연다.

- [Ui/](Ui/index.md) — UI 키트 수신 뒤 공통 타입 중복 컴파일 오류가 발생할 때
- [shell-rosetta-brew.md](shell-rosetta-brew.md) — brew install 실패 또는 pwsh를 찾지 못할 때
- [duplicate-unity-cli.md](duplicate-unity-cli.md) — doctor의 unity-cli가 WARN이거나 셸마다 CLI 버전이 다를 때
- [agent-command-timeout.md](agent-command-timeout.md) — 검증 명령이 거부되거나 시간 초과로 끊길 때
- [task-state-across-worktrees.md](task-state-across-worktrees.md) — doctor의 Task 상태 충돌이 서로 다른 worktree의 보관 시점 차이인지 확인할 때
- [space-branch-update-without-pr.md](space-branch-update-without-pr.md) — PR 없는 공간 개인 브랜치는 Update branch 불가, git pull 또는 PM 앞당기기·merges API. dev 공용 변경을 공간 작업 브랜치까지 내려보낼 때
- [integration-squash-sync.md](integration-squash-sync.md) — 통합 Squash 동기화의 계보 단절·재충돌과 생존 규칙 의존 코드. 인간 대포 상향 병합과 동기화 검사 때
- [balance-on-outside-tightrope.md](balance-on-outside-tightrope.md) — 외줄 밖 씬에서 균형이 시작부터 켜져 서 있기만 해도 추락. 외줄이 아닌 씬에서 캐릭터가 저절로 쓰러질 때
- [hub-spawn-before-environment-enable.md](hub-spawn-before-environment-enable.md) — 로비 환경 활성화보다 출발 위치 조회가 먼저 실행되면 기본 위치로 복귀한다. 공간에서 로비로 돌아올 때 위치가 틀리면 읽기
- [third-party-scripts-after-checkout.md](third-party-scripts-after-checkout.md) — 외부 에셋을 나중에 받은 뒤 DOTween UI 모듈이 컴파일 입력에서 빠질 때
- [third-party-branch-sync.md](third-party-branch-sync.md) — 브랜치 전환 전에 public/private Git·LFS를 준비하고 실패 시 사용자 파일을 보존한다. 안전 전환이 차단되거나 바이너리 씬 때문에 _ThirdParty가 항상 modified로 보여 checkout이 막힐 때
- [asset-helper-lfs-restore.md](asset-helper-lfs-restore.md) — 에셋 도우미의 LFS 파일 보관·복구를 바꿀 때
- [asset-helper-sparse-cleanup.md](asset-helper-sparse-cleanup.md) — 에셋 공유 뒤 임시 worktree·브랜치가 남을 때
- [third-party-first-import-rewrite.md](third-party-first-import-rewrite.md) — 새 worktree 첫 import 때 외부 에셋 씬이 빈 씬으로 덮이고 머티리얼 ~220개가 변환된다. 에디터를 닫고 서브모듈 복구. PR 스크립트가 "저장하지 않은 변경"으로 막힐 때
- [ubuntu-git-executable-resolution.md](ubuntu-git-executable-resolution.md) — Ubuntu CI에서 Git 실행 경로가 여러 개로 반환되어 PowerShell 회귀가 실패할 때
- [powershell-error-output-wrapping.md](powershell-error-output-wrapping.md) — PowerShell 오류 출력의 줄바꿈 때문에 예상 거부 정규식이 불일치할 때
- [remote-player-judged-before-placement.md](remote-player-judged-before-placement.md) — 공연 씬 로드 직후 호스트가 원격의 옛 위치로 사망 판정한다. 연결 공연에서 참가자만 시작부터 죽어 있을 때
- [demo-ui-includes-hud.md](demo-ui-includes-hud.md) — Demo 이름의 줄타기 UI를 통째로 끄면 HUD·게이지도 사라진다. 연결 모드에서 시연 UI를 숨길 때
- [changed-subscriber-advances-campaign.md](changed-subscriber-advances-campaign.md) — Changed 알림 안에서 구독자가 런을 진행시킬 수 있어 알림 뒤 공연 좌표를 재확인. 런·보상 이벤트를 보낸 뒤 이어서 종료·정산 처리할 때
- [inactive-root-component-scan.md](inactive-root-component-scan.md) — 비활성 루트에서 GetComponentsInChildren()이 꺼진 원본까지 반환한다. 원본을 꺼서 보존하고 복제본을 찾는 코드를 쓸 때
- [mesh-copyserialized-stale-render.md](mesh-copyserialized-stale-render.md) — 쓰이는 메시 에셋을 CopySerialized로 덮어쓰면 화면은 예전 메시를 그린다. 절차적 메시를 같은 경로에 다시 저장할 때
- [revived-before-performance-unload.md](revived-before-performance-unload.md) — 공연 씬 해제 전 부활자가 옛 낙하·함정 판정으로 다시 사망. 부활 로그와 달리 대기실에서 관전이 지속될 때
- [textmesh-default-shader-through-walls.md](textmesh-default-shader-through-walls.md) — 생성한 TextMesh 기본 재질은 깊이를 무시해 벽 너머로 보인다. 에디터 코드로 표지·번호 글자를 만들 때
