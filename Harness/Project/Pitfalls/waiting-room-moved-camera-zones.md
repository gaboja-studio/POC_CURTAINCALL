---
summary: 대기실 배치 변경 시 비활성화된 옛 통로의 시점 영역과 옛 검사 좌표를 새 통로에 맞춰야 한다
status: active
updated: 2026-11-03
source: Task-20261031-086, Task-20261102-094, Task-20261102-097-rehearsal-flow
---

# 대기실 통로 재배치와 카메라 영역

## 언제 읽나

대기실 무대 입구 통로를 옮기거나 복도에서 돌아올 때 시점이 일찍 전환될 때.

## 원인과 대응

`WaitingRoom_LayoutA` 재배치에서 옛 `Zone_Corridor`가 꺼진 채 새 통로가 구성될 수 있다. 현재 씬은 `S06_StageCorridor/StageProcessionalRoute` 아래 진입로와 공연 입구용 카메라 영역을 따로 둔다. 생성 도구 `WaitingRoomLayoutA.SourceFaithful.cs`도 두 영역을 생성한다. 경계에서 좁은 시점을 유지하도록 기존 1m 이탈 여유를 사용한다.

예전 테스트의 x12→9.6 좌표는 새 배치에서 무대 통로가 아니라 숙소 경계다. 공간 이동 후에는 실제 활성 구역의 위치·방향을 먼저 확인하고 통로와 방 경계를 따로 검사한다. 벽 충돌이 해소된 뒤 원래 카메라 거리로 천천히 돌아가는 시간도 고려한다.

## 확인 방법

`CameraViewsPlayTests`에서 통로 진입→문턱 유지→완전 이탈→여유 구간 재진입과 방 복귀 보간을 실행한다. 실제 키보드 조작의 체감은 별도로 확인한다.

## 완료 후 길안내 범위

2026-11-03 리허설 완료 검사에서 집결 위치(-6,0,-10.15)가 고정 NavMesh 생성 범위(z>=0) 밖이라 PathInvalid가 됐다. 맵 이동 후에는 카메라 구역뿐 아니라 런타임 NavMesh 범위도 실제 출발점·목적지까지 포함해야 한다. RehearsalPathGuide는 두 지점과 여유 12m를 포함해 굽는다. 경로가 없을 때 벽을 관통하는 직선으로 대체하지 않는다.

## 밤 튜토리얼 카메라 연속 이동 (2026-11-04)

- 목표 근처의 가장 가까운 NavMesh 점이 상점 카운터 안쪽처럼 다른 섬에 있을 수 있다. `SamplePosition` 한 번으로 결정하지 말고 목표 주변 후보들의 완전 경로를 계산해 출발점과 연결된 점을 고른다.
- 복귀 시간은 진입 시간의 1/3로 하고, 원래 카메라 복귀와 브레인 블렌드 복원이 모두 완료되기 전에는 다음 튜토리얼 카메라를 시작하지 않는다.

## 복제 씬·충돌체 없는 천장

- `CameraViewDirector.UsesWallCollision`은 씬 이름으로 범위를 제한한다. WaitingRoomPursuit가 빠져 스프링 암과 고정 구조물 가림 제외 모두 꺼져 있었다. 원본/추격 씬·탈출로를 포함하며 새 테스트 씬을 복제할 때 이 조건도 확인한다.
- 장식 MeshRenderer에는 Collider가 없을 수 있다. 최초40개 천장만 보완했을 때 돔/외벽/엘리베이터 등에 누락이 남았다. 현재 Base/Pursuit 모두 검토한430개 구조물에 CameraCollisionSurface를 적용한다. Tools/CurtainCall/Camera/현재 대기실 벽·천장 충돌 보완은 활성 씬만 Undo 가능하게 보완하며 별도로 저장한다. 새 구조물은 Editor/WaitingRoomCameraCollisionAuthoring의 명시적 대상 규칙에도 반영한다.
- Cube만 OBB 사용, 나머지76개는 UseMeshGeometry로 실제 면·모서리·꼭짓점 양면 구체 스윕. 특히 PassageFloorCeiling6개를 Mesh.bounds로 막으면 빈 통로까지 채워져 카메라가 눈높이에 고정된다. 복합 통로7개의 내부 여유와 모든 구조물 면 샘플을 WaitingRoomCameraCoverageTests로 검사한다. 물리/NavMesh는 변경하지 않는다.
- MeshFilter 메시가 읽기 가능해야 하며 월드 삼각형은 메시 참조/Transform이 바뀌면 다시 만든다. 같은 메시를 런타임에서 직접 변형하는 용도는 아니다. Renderer.enabled=false인 패널과 비활성 오브젝트는 차단하지 않는다. 카메라가 일시적으로 숨기는 forceRenderingOff는 충돌에서 무시한다.
- 실제 방9곳씩의 천장·돔·물리 벽·거리 복귀 Play 검사를 함께 실행한다. 한 천장의 효과를 비교하려면 보강판·보 같은 이웃 차단 면을 테스트 중에만 잠깐 제외하고 복원한다. 카메라 pivot을 내부 천막에 겹쳐 놓으면 거리0은 정상 접촉이므로 돔 단독 검사는 천막 위의 빈 공간에서 한다.
