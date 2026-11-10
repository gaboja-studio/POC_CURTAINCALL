# Task-20261110-115

- **Title:** 대기실 통로 기믹
- **Type:** feat
- **Assignee:** @gyumin9048-ctrl
- **Branch:** feat/waiting-room-passage-gimmicks
- **Base:** dev
- **Jira:** CC-250~259 (에픽 CC-3)

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

## 미정·주의
- 휴대 절단기: prefab 추출 + 기존 EscapeExitSaw 소폭 수정 방식 승인 필요.
- 좌석 없는 HumanCannonSystem 상시 배치 승인 필요(평소 조작 동일, 근처 F로 트램펄린·절단기 잡기 추가, 착지 부상 경로 켜짐).
- HumanCannonSystem·트램펄린은 씬당 1개 → 트램펄린 구역 1곳. 반동 약 7m라 평지 착지 시 낙사 기준(5m) 초과.
- 1인 수직 이동 대안 미정(스프링 신발은 3m 이상 낙하에서만 작동).
- 셔터·조명은 밤 정비 코드(NightDoorLock·NightPower)를 쓰지 않는다. NightPower Light와 겹치지 않게.
- WaitingRoom.unity는 대형 씬 YAML(약 79MB). 수정 전 asset-ownership 확인, 겹치는 수정은 알린다.
