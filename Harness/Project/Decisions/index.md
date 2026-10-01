# Project Decisions

이 POC에서 사람이 확정한 결정. 상황에 맞는 파일 1개만 연다. AI 추천안은 여기가 아니라 Task의 Open Decisions에 둔다.

- [branch-strategy.md](branch-strategy.md) — 브랜치 6종, PR 흐름, 병합 방식, PM 전용 영역. 브랜치를 만들거나 PR 대상을 정할 때
- [pr-issue-format.md](pr-issue-format.md) — PR·이슈 제목과 본문 형식, JIRA·이슈 번호 규칙. PR·이슈를 만들거나 .github 템플릿을 고칠 때
- [harness-tooling.md](harness-tooling.md) — 스크립트 언어·Skill 형식·worktree 위치. 하네스 도구를 바꿀 때
- [multiplayer-stack.md](multiplayer-stack.md) — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지는 PM 선설치. 네트워크 코드·패키지를 다룰 때
- [asset-placement.md](asset-placement.md) — 코드는 Assets/Scripts/, 씬은 Assets/Scenes/(테스트는 Scenes/Tests/), 불러와 쓰는 에셋은 Resources. 스크립트·씬·프리팹 위치를 정할 때
- [tightrope-rules.md](tightrope-rules.md) — 외줄타기 조작·협동/단독 옆줄(콜라이더 합체·1m 50% 감소)·목마 올라타기·목마 점프·전원 추락 재시작·1명 도착 성공. 외줄·목마·입력을 구현할 때
- [prototype-verification.md](prototype-verification.md) — AI는 컴파일만 검사, 플레이 테스트는 작업자에게 받음. 프로토타입 Task를 검사·제출할 때
- [tightrope-balance.md](tightrope-balance.md) — 외줄 균형(기획 문서 rope_balance 기준): 초록 ±40·빨강 2초 추락, 자연 흔들림·기울기 가속·목마 배율·착지/옆줄 충격, 수치는 인스펙터. 균형·게이지·추락을 구현하거나 튜닝할 때
