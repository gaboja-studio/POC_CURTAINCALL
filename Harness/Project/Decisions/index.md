# Project Decisions

이 POC에서 사람이 확정한 결정. 상황에 맞는 파일 1개만 연다. AI 추천안은 여기가 아니라 Task의 Open Decisions에 둔다.

- [performance-background-density.md](performance-background-density.md) — 최초 공연장의 밀도·분위기·압도감 유지, 플랫폼별 기둥 반복과 접합 불량 정리. 공연장 철골 배치를 조정할 때

- [escape-background-direction.md](escape-background-direction.md) — 탈출로의 중성 콘크리트·흐린 내부/햇빛 강한 갈대밭 대비·기존 캐릭터 사용. 공통 맵과 B/C 배경을 제작할 때
- [escape-control-baseline.md](escape-control-baseline.md) — A 우선 완성·B/C M2 이후 일정, 공통 1인 개발환경·인간대포 기본 조작·외줄 전환·그네 기준

- [branch-strategy.md](branch-strategy.md) — 브랜치·PR 흐름, UI 제작→연결 직접 pull 예외, 병합 방식, PM 전용 영역. 브랜치를 만들거나 PR 대상을 정할 때
- [pr-issue-format.md](pr-issue-format.md) — PR·이슈 제목과 본문 형식, JIRA·이슈 번호 규칙. PR·이슈를 만들거나 .github 템플릿을 고칠 때
- [harness-tooling.md](harness-tooling.md) — 스크립트 언어·Skill 형식·worktree 위치. 하네스 도구를 바꿀 때
- [plan-value-changes.md](plan-value-changes.md) — 기획과 다른 수치는 인스펙터 값으로, 끝나면 "어떤 기획: 어떤 변경"으로 보고. 기획 수치를 구현·보고할 때
- [multiplayer-stack.md](multiplayer-stack.md) — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지는 PM 선설치. 네트워크 코드·패키지를 다룰 때
- [voice-chat.md](voice-chat.md) — 음성 채팅(2026-10-20): Vivox, 상시 마이크+음소거 키, 생존자 거리 감쇠 2m~20m·사망자(Fallen·Retired) 거리 무관, 서로 안 들림, Start 흐름 확정 씬만(백스테이지는 M2에서 대기실로 통합). 음성 기능·음량·사망 상태를 다룰 때
- [asset-placement.md](asset-placement.md) — 코드는 Assets/Scripts/, 씬은 Assets/Scenes/(테스트는 Scenes/Tests/), 불러와 쓰는 에셋은 Resources. 스크립트·씬·프리팹 위치를 정할 때
- [tightrope-rules.md](tightrope-rules.md) — 외줄타기 조작·협동/단독 옆줄(콜라이더 합체·1m 50% 감소)·목마 올라타기·목마 점프·추락 시 매달림(M2). 외줄·목마·입력을 구현할 때
- [prototype-verification.md](prototype-verification.md) — AI는 컴파일만 검사, 플레이 테스트는 작업자에게 받음. 프로토타입 Task를 검사·제출할 때
- [tightrope-course-rules.md](tightrope-course-rules.md) — 외줄 상세 규칙: 코스·진행·톱날·M2 매달림·동료 F 2초 구조. 2026-10-29 구조 판정 완화·구조자 균형 보호 포함. 코스·구조 규칙을 구현할 때
- [tightrope-balance.md](tightrope-balance.md) — 외줄 균형(기획 문서 rope_balance 기준): 초록 ±40·빨강 2초 추락(M2: 먼저 매달림), 자연 흔들림·기울기 가속·목마 배율·위층 전달·목마 중 추락(위 전원 추락·아래 반동)·착지/옆줄 충격, 수치는 세팅. 균형·게이지·추락을 구현하거나 튜닝할 때
- [player-control-architecture.md](player-control-architecture.md) — 플레이어 조작 3층(키→공통 역할 입력 에셋, 콘텐츠별 조작 규칙=전략, 동작 부품). 입력·조작을 추가하거나 콘텐츠별 키 동작을 바꿀 때
- [feature-with-network.md](feature-with-network.md) — 007 이후 기능 Task는 기능+동기화를 같이, 007 공개 기능 사용 가능, 009·010 축소. 기능 Task를 계획하거나 네트워크 코드를 다른 기능에서 쓸 때
- [injury-rules.md](injury-rules.md) — 공통 부상 규칙(2026-10-13 부상 기획 + M2): 절단 순서 고정(인간 대포 추락은 2곳), 잃은 팔다리 수 0~4, 사망=4곳·즉사 추락·외팔로 쉬는 시간 잡힘, 묘기 재시작 없이 관전 후 외팔 부활, 신체 복구는 쉬는 시간·밤 언제든·1~3부위·부위당 100토큰(+25)·역순·공동 자금, 탐색 중/공연 중 패널티, 팔 개수 무관 기물 기본 2칸·배낭 삭제. 절단·사망·부활·복구·패널티를 다룰 때 (body-damage.md보다 우선)
- [reward-rules.md](reward-rules.md) — 토큰 공동 자금·생존 호응×2/사망½×2·도착자 완주 보너스·환호/야유/개인 관심 감소·공용 환호 포인트·복구100/+25 확정(27459631), 최대3,000 목표와2,700 계산 구분·기물7종 새제품 가격·내구도 확정(효과 수치 등 남은 초안 유지). 보상·정산·복구·경제를 다룰 때
- [item-durability.md](item-durability.md) — 기물7종 새제품 내구도·성공 시 차감·마지막 효과 완료 후 파괴·슬롯 제거(2026-10-28 확정), 세부 타이머·판정 순서 미정. 기물 사용·인벤토리·탐색 판정을 만들 때
- [run-rules.md](run-rules.md) — 런 공통 규칙: 쉬는 시간 시작 즉시 시계·A/B 삭제(M2, 구현은 아직 A/B)·생존 1명까지 계속·전원 사망 실패(2026-10-16 공통화)·재접속·최소 인원·더미 우선. 공용 종료/런 계약을 정할 때
- [control-keys.md](control-keys.md) — 공통 조작키(2026-10-15 확인): 단독 hold/조합 pressed toggle·F(매달린 동료 2초 끌어올리기 포함)·카메라/UI, 위기/QTE 예시 구분. 입력·조작 규칙을 추가할 때
- [body-damage.md](body-damage.md) — (사망·재시작·수리·복구·추락은 injury-rules.md로 대체) 팔·다리 손실은 게임 전체 상태(완전히 죽기 전까지 묘기·재시작을 넘어 유지), 팔=상호작용·다리=이동 계열 패널티(크기는 묘기마다), 네 팔다리 모두 손실 = 사망, 잘린 팔다리 날아감, 두 다리 없으면 기어가기, 패널티는 콘텐츠별 교체 정책, 이후 돈으로 부품 수리. 부위 손실·패널티·수리를 다룰 때
- [game-settings.md](game-settings.md) — 기획 조정값은 `Assets/Resources/Settings/GameSettings/` 한 폴더(공통·묘기별·대기실·탐색·탈출로 5묶음 + 교체형 보상, 2026-10-14; 탐색 제한 시간 A/B는 M2에서 삭제, 구현 칸은 남음), 칸마다 한글 설명·단위, 플레이 중 반영. 수치를 만들거나 옮길 때
- [third-party-assets.md](third-party-assets.md) — 외부 에셋은 무료·유료 구분 없이 private 레포를 `Assets/_ThirdParty` submodule로, 공개 레포엔 직접 만든 것만. 외부 에셋을 넣거나 옮길 때
- [claude-mods.md](claude-mods.md) — 팀 공용 Claude Code 모드는 저장소 마켓플레이스(curtaincall)로 배포·version 올려 업데이트, 진행 패널의 todo 색·버튼·권한 규칙. 모드를 고치거나 배포·끄는 법을 안내할 때
- [space-workspaces.md](space-workspaces.md) — 공간별 경로·현재 담당(공연장·탈출로 @RagSpirin)·인간 대포 자식 통합과 순차 구역, M2 기획은 백스테이지를 대기실로 통합(구현 구조는 분리 유지). 공간 Task·통합을 준비할 때
- [Waiting-room/index.md](Waiting-room/index.md) — 대기실 2차 기획(2026-10-15 컨펌 대기) + M2(2026-10-27, 우선): 백스테이지 통합·런마다 랜덤 방·탐색 제한 시간(시작 즉시, 안전지대 없음, 무대 입구 통로 복귀)·밤 정비(철창·침대 수면 종료)/신체 복구 서비스 상시·하루 시계/스케줄표/난이도 단계·상점(기물 구매 밤만)/가판대·미정, 게임 흐름과 충돌 시 게임 흐름 우선. 대기실 Task를 계획할 때
- [Human-cannon/index.md](Human-cannon/index.md) — 인간 대포 v0.5 조작·협동 경로·최종 도착 예외·부상/함정(M2: 추락 2곳 절단·난이도 단계 선택)·미정. 단계별 작업을 계획할 때

- [harness-simplification.md](harness-simplification.md) — 2026-10-27 현재 작업·권한·문서·공유 기준. 이전 구역·인계 제한보다 우선.
