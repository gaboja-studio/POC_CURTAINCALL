---
summary: 공간 4곳(대기실·공연장·탈출로·백스테이지)마다 통합 브랜치·맵 배치 씬·테스트 씬 폴더·스크립트 폴더·담당자를 나눈다. 여러 공간이 쓰는 코드는 Scripts/Systems/, 불러와 쓰는 에셋은 Resources 공용
status: active
updated: 2026-10-08
source: human (2026-10-08 PM @MoHoDu)
---

# 공간별 작업 공간

## 언제 읽나

공간(대기실·공연장·탈출로·백스테이지) 작업의 통합 브랜치·Task를 만들거나, Task `setup.md` 작업 구역을 적거나, 내 공간 밖 파일을 고쳐도 되는지 판단할 때.

## 결정

- 2026-10-08 / PM(@MoHoDu): 외줄 프로토타입 통합(`integration/tightrope-prototype`)을 dev로 닫은 뒤, 공간마다 담당자와 작업 공간을 분리한다.

| 공간 | 통합 브랜치 | 맵 배치 씬 | 테스트 씬 폴더 | 스크립트 폴더 | 담당 |
|---|---|---|---|---|---|
| 대기실 | `integration/waiting-room` | `Assets/Scenes/Levels/waiting_room.unity` | `Assets/Scenes/Tests/WaitingRoom/` | `Assets/Scripts/WaitingRoom/` | @gyumin9048-ctrl |
| 공연장 | `integration/main-stage` | `Assets/Scenes/Levels/main_stage.unity` | `Assets/Scenes/Tests/MainStage/` | `Assets/Scripts/MainStage/` | @lakuase2 |
| 탈출로 | `integration/escape-route` | `Assets/Scenes/Levels/escape_route.unity` | `Assets/Scenes/Tests/EscapeRoute/` | `Assets/Scripts/EscapeRoute/` | @RagSpirin |
| 백스테이지 | `integration/back-stage` | `Assets/Scenes/Levels/backstage.unity` | `Assets/Scenes/Tests/BackStage/` | `Assets/Scripts/BackStage/` | @gyumin9048-ctrl |

- 공간 작업 브랜치(`feat/*` 등)는 자기 공간의 통합 브랜치로만 PR한다.
- 맵 배치 씬은 해당 공간 통합 안에서 Task 1개만 소유한다(공용 파일 소유 표).
- 여러 공간이 함께 쓰는 코드는 `Assets/Scripts/Systems/`(네트워크·플레이어·카메라·세팅·기록), 테스트 씬은 `Assets/Scenes/Tests/Systems/`에 둔다. 공간 Task는 읽기만 하고, 수정은 PM이 배정한 Task에서만 한다.
- `Assets/Resources/`는 모든 공간 공용이다. 공간 Task가 Resources 하위 폴더를 고치려면 해당 통합의 공용 파일 소유 표로 배정받는다.
- 2026-10-09 / PM: 맵 배치 씬에는 둘러보기용 `PlaySetup`(Player·임시 바닥)과 Main Camera의 1인칭 카메라(`Assets/Scripts/Systems/LevelPreview/`, 내 모델 숨김)를 둔다. 게임 기능이 아니며, 바닥을 만들면 `TempFloor`는 지워도 된다. 공간 Task는 별도 에셋 폴더 없이 `Assets/_ThirdParty/` 에셋을 맵 배치 씬에 바로 배치한다.
- 외줄 묘기(기존 프로토타입)는 공연장 소속이다: `Assets/Scripts/MainStage/Tightrope/`, `Assets/Scenes/Tests/MainStage/Tightrope/`.
- 참고: 공간 분리 문서(Confluence `bYmkRlnpusng/pages/10454669`).

## 이유

공간마다 담당자가 생겨 같은 씬·폴더를 동시에 고치는 충돌을 막고, 공간별로 따로 dev에 올릴 수 있게 한다.

## 바꾸려면

공간·담당자·경로가 바뀌면 이 표와 해당 `Integrations/Active/Integration-<slug>/meta.md`를 함께 고친다.
