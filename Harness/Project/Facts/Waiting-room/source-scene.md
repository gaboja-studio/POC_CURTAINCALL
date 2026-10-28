---
summary: 기존 대기실 복제는 WaitingRoomBase의 WaitingRoomGreybox와 분산 Mood·Walls·기능 묶음을 기준으로 한다
status: active
updated: 2026-10-28
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

- LayoutA의 방 확장·지하 추가 후에는 루트 밖 `ExpertServiceMouseLook` 영역도 전체 활성 배치에 맞춰 갱신한다. 이전 영역을 유지하면 범위 밖에서 `DefaultThirdPerson`으로 복귀해 마우스 회전이 꺼진다. `WaitingRoomLayoutA.Finish`가 `WaitingRoomLayoutFitter.FitMouseLookZone`을 호출한다(Task-075, 2026-10-28 실행 재현).

- 2026-10-28 현재 무대 기준: `WaitingRoomLayoutA.SourceFaithful.cs`가 Greybox의 S06 바닥/커튼 전면, S07, Mood/Tunnel 전체·커튼·가구·조명을 하나의 프레임에서 65×14m, 아치 높이13m에 맞춰 확대한다. 개별 파이프/커튼 위치만 벌리지 않는다. 원본 상대 배치·메시·머티리얼을 보존한다. 독자 재제작한 벽·마키·얇은 배관 생성은 제거했다.
- 연습 링은 0.6m 높이, 남/서쪽 3단 계단으로 접근한다. 원래 링 아트·치마를 올리고 높이에 종속되는 대포 받침/착지 매트/표시도 함께 조정한다. 얇은 사다리 충돌 수정은 유지한다.
- 기존 물류 LogisticsFrontages는 보존한다. 무대/링 정정 전용 `RestoreSourceStageAndRaisedRing`은 전문가·물류·기계실을 재생성하지 않는다. 상세는 Task-075 `source-faithful-stage-report.md`.
- 전문가 홀은 로비 직결 승강기 하나, 기계실은 약2.63배 확장 피트와 설비 뒤 -10m 탈출 연결이다. 이 변경은 현재 무대/링 정정에서 보존한다. 이전 세부 결과는 Task-075 `facility-detail-report.md`.

## 확인 방법
대상 씬에서 Editor 계층과 직렬화 참조를 조사한다. 단계 결과·복제 원본 ID는 Task-075의 stage-01-report.md를 참고한다.
