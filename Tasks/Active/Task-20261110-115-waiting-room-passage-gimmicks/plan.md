# Task-20261110-115

- **Title:** 대기실 통로 기믹
- **Type:** feat
- **Assignee:** @gyumin9048-ctrl
- **Branch:** feat/waiting-room-passage-gimmicks
- **Base:** dev
- **Jira:** CC-250~265 (에픽 CC-3, CC-260~265는 Task 문서 미반영)

## JIRA
CC-250, CC-251, CC-252, CC-253, CC-254, CC-255, CC-256, CC-257, CC-258, CC-259

## Context
[M2] 대기실 통로 기믹 기획(Confluence 38764736)을 대기실에 구현한다. 대상 씬은 게임 설정(waitingRoomVariant=Integrated)의 실제 대기실 `WaitingRoom.unity`. 아이디어 구조도: [gimmick-map.png](gimmick-map.png).
탈출로는 A/B/C 3개(A 제작됨). 대기실→탈출로 길을 여러 개 만들고, 가는 방법·도착 입구에 따라 진입 탈출로가 달라진다. 숙소 환풍구 → 지하 기계실 연결은 구상 확정, 나머지 배치는 제안·확인 단계. 대기실 핵심 배치 구조는 유지하고 통로·공간만 추가·변경한다.

## 원칙 (사용자 지시 2026-11-10)
- 기믹은 맨몸으로 열 수 있는 것과 다른 기물이 필요한 것으로 분리한다.
- 협동형(두 사람이 동시에 상호작용)은 필요한 경우에만 제한적으로 쓴다.
- 숏컷은 한 번 열면 계속 열려 있다.
- 목마·트램펄린·휴대 절단기는 공연에 완성된 최신본을 가져와 쓴다. 새 기물·스크립트·에셋은 만들지 않는다.
- 기능 구현 우선, 수치 조정은 후순위.

## 계획
1. 공연 기물 연결: 목마(이미 자동 활성, 확인만) → 트램펄린(좌석 없는 HumanCannonSystem + 트램펄린 1개, 착지 단) → 휴대 절단기(탈출로 오브젝트 prefab화, EscapeExitSaw 절단 완료를 ExplorationState 대상 ID로 연결).
2. 대기실→탈출로 전환(씬당 1개 제약)·F 겹침 검증.
3. 맨몸 기믹: ExplorationGate(Breakable/Open, toolId 비움)·빗장 숏컷·낮은 환풍구 통로·openedBy 연쇄·ExplorationSlide 셔터.
4. 기물 필요 기믹: BreakableWall·ToolTarget(Pry/HairpinLock)·Gate toolId(차단기·동력 아이템)·절단기 배선·UvMark.
5. 협동 기믹 1~2곳(Gate Cooperative).
6. 조합 루트 1개 구성·2인 검증, check-work.
7. 수치 조정(후순위).

## 확정 (사용자 2026-11-10)
- 목마: 조건이 되면 기존 목마 상호작용 패널을 띄운다.
- 트램펄린: 인간대포 해금 뒤에만, 연습실 장식 트램펄린 자리의 공연용 운반 트램펄린(RoundTrampolinePrepared). 대포 리허설 중엔 비우고 끝나면 원래 자리.
- 휴대 절단기: 탈출로 2번 절단기를 prefab으로 추출해 무대 장치 보관실에 1대. 들기·톱날 발사까지(배선 연결은 CC-256).
- 맨몸 기믹: 벽(금 간 벽·배관함) 맨몸 15/뽑개 6, 상자(파괴 가능·낡은 상자·판자 상자·관리실 서랍) 맨몸 5/뽑개 1. 섞으면 비율 합산.

## 미정·주의
- HumanCannonSystem(좌석 없음)을 대기실에 상시로 둔다(대포 리허설 중 제외). 평소 조작은 공용 플랫폼과 같다.
- 트램펄린 반동 약 7m라 평지 착지 시 낙사 기준(5m) 초과 가능(수치 후순위).
- 1인 수직 이동 대안 미정(스프링 신발은 3m 이상 낙하에서만 작동).
- 셔터·조명은 밤 정비 코드(NightDoorLock·NightPower)를 쓰지 않는다. NightPower Light와 겹치지 않게.
- WaitingRoom.unity는 대형 씬 YAML(약 79MB). 수정 전 asset-ownership 확인, 겹치는 수정은 알린다.
