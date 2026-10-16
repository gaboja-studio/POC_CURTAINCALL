# Project Pitfalls

이 POC·이 개발 환경에서 겪은 함정. 상황에 맞는 파일 1개만 연다.

- [shell-rosetta-brew.md](shell-rosetta-brew.md) — brew install 실패 또는 pwsh를 찾지 못할 때
- [duplicate-unity-cli.md](duplicate-unity-cli.md) — doctor의 unity-cli가 WARN이거나 셸마다 CLI 버전이 다를 때
- [agent-command-timeout.md](agent-command-timeout.md) — 검증 명령이 거부되거나 시간 초과로 끊길 때
- [task-state-across-worktrees.md](task-state-across-worktrees.md) — doctor의 Task 상태 충돌이 서로 다른 worktree의 보관 시점 차이인지 확인할 때
- [space-branch-update-without-pr.md](space-branch-update-without-pr.md) — PR 없는 공간 개인 브랜치는 Update branch 불가, git pull 또는 PM 앞당기기·merges API. dev 공용 변경을 공간 작업 브랜치까지 내려보낼 때
- [integration-squash-sync.md](integration-squash-sync.md) — 통합 Squash 동기화의 계보 단절·재충돌과 생존 규칙 의존 코드. 인간 대포 상향 병합과 동기화 검사 때
- [balance-on-outside-tightrope.md](balance-on-outside-tightrope.md) — 외줄 밖 씬에서 균형이 시작부터 켜져 서 있기만 해도 추락. 외줄이 아닌 씬에서 캐릭터가 저절로 쓰러질 때
- [third-party-scripts-after-checkout.md](third-party-scripts-after-checkout.md) — 외부 에셋을 나중에 받은 뒤 DOTween UI 모듈이 컴파일 입력에서 빠질 때
- [changed-subscriber-advances-campaign.md](changed-subscriber-advances-campaign.md) — Changed 알림 안에서 구독자가 런을 진행시킬 수 있어 알림 뒤 공연 좌표를 재확인. 런·보상 이벤트를 보낸 뒤 이어서 종료·정산 처리할 때
