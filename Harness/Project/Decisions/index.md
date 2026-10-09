# Project Decisions

이 POC에서 사람이 확정한 결정. 상황에 맞는 파일 1개만 연다. AI 추천안은 여기가 아니라 Task의 Open Decisions에 둔다.

- [branch-strategy.md](branch-strategy.md) — 브랜치 7종, PR 흐름(fix·resource 직행), 병합 방식, PM 전용 영역. 브랜치를 만들거나 PR 대상을 정할 때
- [pr-issue-format.md](pr-issue-format.md) — PR·이슈 제목과 본문 형식, JIRA·이슈 번호 규칙. PR·이슈를 만들거나 .github 템플릿을 고칠 때
- [harness-tooling.md](harness-tooling.md) — 스크립트 언어·Skill 형식·worktree 위치. 하네스 도구를 바꿀 때
- [plan-value-changes.md](plan-value-changes.md) — 기획과 다른 수치는 인스펙터 값으로, 끝나면 "어떤 기획: 어떤 변경"으로 보고. 기획 수치를 구현·보고할 때
- [multiplayer-stack.md](multiplayer-stack.md) — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지는 PM 선설치. 네트워크 코드·패키지를 다룰 때
- [asset-placement.md](asset-placement.md) — 코드는 Assets/Scripts/, 씬은 Assets/Scenes/(테스트는 Scenes/Tests/), 불러와 쓰는 에셋은 Resources. 스크립트·씬·프리팹 위치를 정할 때
- [tightrope-rules.md](tightrope-rules.md) — 외줄타기 조작·협동/단독 옆줄(콜라이더 합체·1m 50% 감소)·목마 올라타기·목마 점프·전원 추락 재시작·1명 도착 성공. 외줄·목마·입력을 구현할 때
- [prototype-verification.md](prototype-verification.md) — AI는 컴파일만 검사, 플레이 테스트는 작업자에게 받음. 프로토타입 Task를 검사·제출할 때
- [tightrope-course-rules.md](tightrope-course-rules.md) — 외줄 상세 규칙(2026-10-03 기획): 4명·5분·1명 도착 즉시 클리어, 4줄·간격 3m·100m, 이동 0.5·점프 0.8·캡슐 1.5/0.6, 톱날. 코스 수치·진행 판정·톱날을 구현할 때
- [tightrope-balance.md](tightrope-balance.md) — 외줄 균형(기획 문서 rope_balance 기준): 초록 ±40·빨강 2초 추락, 자연 흔들림·기울기 가속·목마 배율·위층 전달·목마 중 추락(위 전원 추락·아래 반동)·착지/옆줄 충격, 수치는 세팅. 균형·게이지·추락을 구현하거나 튜닝할 때
- [player-control-architecture.md](player-control-architecture.md) — 플레이어 조작 3층(키→공통 역할 입력 에셋, 콘텐츠별 조작 규칙=전략, 동작 부품). 입력·조작을 추가하거나 콘텐츠별 키 동작을 바꿀 때
- [feature-with-network.md](feature-with-network.md) — 007 이후 기능 Task는 기능+동기화를 같이, 007 공개 기능 사용 가능, 009·010 축소. 기능 Task를 계획하거나 네트워크 코드를 다른 기능에서 쓸 때
- [body-damage.md](body-damage.md) — 팔·다리 손실은 게임 전체 상태(완전히 죽기 전까지 묘기·재시작을 넘어 유지), 팔=상호작용·다리=이동 계열 패널티(크기는 묘기마다), 네 팔다리 모두 손실 = 사망, 잘린 팔다리 날아감, 두 다리 없으면 기어가기, 패널티는 콘텐츠별 교체 정책, 이후 돈으로 부품 수리. 부위 손실·패널티·수리를 다룰 때
- [game-settings.md](game-settings.md) — 기획 조정값은 `Assets/Resources/Settings/GameSettings/` 한 폴더(게임 기본 1 + 묘기별 1 + 교체형 보상), 칸마다 한글 설명·단위, 플레이 중 반영. 수치를 만들거나 옮길 때
- [third-party-assets.md](third-party-assets.md) — 외부 에셋은 무료·유료 구분 없이 private 레포를 `Assets/_ThirdParty` submodule로, 공개 레포엔 직접 만든 것만. 외부 에셋을 넣거나 옮길 때
- [space-workspaces.md](space-workspaces.md) — 공간 4곳(대기실·공연장·탈출로·백스테이지)별 통합 브랜치·맵 씬·테스트 씬·스크립트 폴더·담당자, 공용은 Scripts/Systems/·Resources. 공간 작업 Task·통합을 만들거나 작업 구역을 정할 때
