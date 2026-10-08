# Integration-waiting-room

- **Feature:** 대기실
- **Status:** preparing
- **Branch:** integration/waiting-room
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 미정 (PM이 공간 기획 확정 후 결정)
- **Updated:** 2026-10-08

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 대기실 공간의 맵 배치와 공간 콘텐츠를 dev에서 플레이할 수 있다. (세부 목표는 PM이 공간 기획 확정 후 채움)

## 작업 공간

2026-10-08 PM 결정: `Harness/Project/Decisions/space-workspaces.md`. 이 공간 작업은 아래 경로 안에서만 하고, 작업 브랜치는 이 통합 브랜치로만 PR한다.

| 항목 | 경로·값 |
|---|---|
| 담당 | @gyumin9048-ctrl |
| 맵 배치 씬 | `Assets/Scenes/Levels/waiting_room.unity` |
| 테스트 씬 폴더 | `Assets/Scenes/Tests/WaitingRoom/` |
| 스크립트 폴더 | `Assets/Scripts/WaitingRoom/` |
| 공용 (읽기 전용) | `Assets/Scripts/Systems/`, `Assets/Scenes/Tests/Systems/`, `Assets/Resources/` (수정은 아래 소유 표로 배정받은 Task만) |


## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Scenes/Levels/waiting_room.unity` | 미배정 (첫 맵 배치 Task에 배정) | 대기실 맵 배치 씬은 Task 1개만 수정 |

## Decisions

- Open: 공간 세부 목표·병합 예정 시각, 첫 Task 구성 (PM)
- Decided: None
