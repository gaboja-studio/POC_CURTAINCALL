---
summary: 기존 대기실 복제는 WaitingRoomBase의 WaitingRoomGreybox와 분산 Mood·Walls·기능 묶음을 기준으로 한다
status: active
updated: 2026-10-27
source: Task-20261027-075, 사용자 기준 정정 및 Editor 계층 조사
---

# 대기실 복제 원본 씬

## 언제 읽나
기존 대기실을 복제·재배치하거나 Levels/waiting_room과 그레이박스 중 원본을 고를 때.

## 사실
- 사용자가 지정한 기준은 `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`의 `WaitingRoomGreybox`다. `Assets/Scenes/Levels/waiting_room.unity`의 `WaitingRoom`은 별도 프로토타입이며 이번 복제 원본이 아니다.
- 방은 S01_Lobby, S02_Dormitory(하위 Restroom), S03_Shop, S04_PropRoom, S06_StageCorridor_Temp, S07_StageEntrance_Temp다.
- 완성 아트·조명은 `Mood/` 여러 그룹과 공용 `Walls/`에도 분산된다. 방 루트만 복제하면 누락된다.
- 공간 종속 기능은 `WaitingRoomGameplay/`(스폰·선반·집결·전문가실), `NightSchedule/`(셔터), `ExplorationTimer/`(탐색 경계)와 상점 방에 있다. 루트 밖 서비스·UI·카메라는 중복 생성하지 않고 새 공간 참조로 연결한다.
- Task-075 1단계에서 원본 Greybox는 내용·ID를 보존하고 비활성화했다. 새 맵은 `WaitingRoom_LayoutA`이며 `Assets/Scripts/WaitingRoom/Base/Editor/WaitingRoomLayoutA.cs`가 방 단위로 생성한다(로비 `BuildLobby`, 상점 `BuildShop`). 기준 좌표는 조감도 A 18px = 1m, 로비 29.4×28m 폐공장 홀.
- 상점은 원본 `S03_Shop` 기둥(수화기·배송구)을 그대로 복제해 쓴다. 새 부스로 바꾸지 않는다(2026-10-27 사용자 지시).
- 원본 루트가 비활성이어도 `GetComponentsInChildren()`은 원본 컴포넌트를 반환한다. 기능 검색은 활성 컴포넌트로 거른다(`Harness/Project/Pitfalls/inactive-root-component-scan.md`).
- 일정판은 `NightSchedule/ScheduleCloseUp` 카메라를 참조한다. 일정판 복제 시 이 카메라도 복제·재연결해야 비활성 원본 참조가 남지 않는다.

## 확인 방법
대상 씬에서 Editor 계층과 직렬화 참조를 조사한다. 단계 결과·복제 원본 ID는 Task-075의 stage-01-report.md를 참고한다.
