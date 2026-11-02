---
summary: 대기실 배치 변경 시 비활성화된 옛 통로의 시점 영역과 옛 검사 좌표를 새 통로에 맞춰야 한다
status: active
updated: 2026-11-03
source: Task-20261031-086 / Task-20261102-094
---

# 대기실 통로 재배치와 카메라 영역

## 언제 읽나

대기실 무대 입구 통로를 옮기거나 복도에서 돌아올 때 시점이 일찍 전환될 때.

## 원인과 대응

`WaitingRoom_LayoutA` 재배치에서 옛 `Zone_Corridor`가 꺼진 채 새 통로가 구성될 수 있다. 현재 씬은 `S06_StageCorridor/StageProcessionalRoute` 아래 진입로와 공연 입구용 카메라 영역을 따로 둔다. 생성 도구 `WaitingRoomLayoutA.SourceFaithful.cs`도 두 영역을 생성한다. 경계에서 좁은 시점을 유지하도록 기존 1m 이탈 여유를 사용한다.

예전 테스트의 x12→9.6 좌표는 새 배치에서 무대 통로가 아니라 숙소 경계다. 공간 이동 후에는 실제 활성 구역의 위치·방향을 먼저 확인하고 통로와 방 경계를 따로 검사한다. 벽 충돌이 해소된 뒤 원래 카메라 거리로 천천히 돌아가는 시간도 고려한다.

## 확인 방법

`CameraViewsPlayTests`에서 통로 진입→문턱 유지→완전 이탈→여유 구간 재진입과 방 복귀 보간을 실행한다. 실제 키보드 조작의 체감은 별도로 확인한다.

## 복제 씬·충돌체 없는 천장

- `CameraViewDirector.UsesWallCollision`은 씬 이름으로 범위를 제한한다. WaitingRoomPursuit가 빠져 스프링 암과 고정 구조물 가림 제외 모두 꺼져 있었다. 원본/추격 씬·탈출로를 포함하며 새 테스트 씬을 복제할 때 이 조건도 확인한다.
- 천장/지붕의 장식 MeshRenderer에는 Collider가 없을 수 있다. 씬 적용 조건만 고치거나 임시 Cube 벽 검사만 통과하면 실제 천장 투과가 남는다. 추격 씬의 해당40개 평면 구조물에 `CameraCollisionSurface`를 연결했다. LocalBounds=Mesh.bounds인 OBB를 반경+패딩만큼 확장하여 카메라 거리만 제한하며 물리/NavMesh에는 관여하지 않는다.
- 실제 숙소 Ceiling을 사용하는 PlayMode 검사와 물리 벽/천장·거리 복귀 검사를 함께 실행한다. 임의 모양이나 구멍 있는 메시 전체를 OBB 하나로 막지 않는다. 원본 대기실에는 카메라 전용 면 컴포넌트를 추가하지 않았다.
