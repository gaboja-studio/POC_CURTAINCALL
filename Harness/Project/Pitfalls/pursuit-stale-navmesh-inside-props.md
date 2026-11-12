---
summary: 대기실 배치를 바꾼 뒤 추격 NavMesh를 다시 굽지 않으면 길이 물체 몸통 안을 지나 추격자가 물체 속을 걷고 그 안에서 오른다. 실행 중 보정(PursuitNavMeshRepair)이 막지만 근본 해결은 다시 굽기다.
status: active
updated: 2026-11-12
source: Task-20261112-117 (실제 대기실 140개 물체 추격 조사)
---

# 낡은 추격 NavMesh가 물체 안을 지남

- 증상: 추격자가 콜라이더로 막힌 기계·랙·컨테이너 안으로 들어가거나, 물체 속에서 오르기를 시작한다. 대상이 물체 위에 있을 때 잘 보인다.
- 원인: `WaitingRoomPursuit.asset`은 2026-11-05에 구웠고 씬 배치는 그 뒤에도 바뀌었다. 2026-11-12 측정에서 물체 50개(SM_Machinery_03/04, 팔레트 랙, 컨테이너, 보일러, 사물함 등) 몸통 높이(발 위 0.6~1.2m)에 NavMesh가 있었다.
- 확인: NavMesh 삼각형 위 0.6~1.2m 캡슐이 정적 충돌체와 겹치는지 센다(`PursuitNavMeshRepair.Apply` 로그 `navmesh repair obstacles=N`, 0이면 깨끗함).
- 대응: 배치 작업 뒤 `Tools > Curtain Call > Pursuit > 2 Bake navigation`으로 다시 굽는다. 그 전에도 호스트는 추격 시작 때 겹치는 정적 물체를 실행 중 장애물로 깎는다(씬·에셋 수정 없음, 문·움직이는 물체 제외). 오르기 통로는 깎기 반영 다음 프레임에 만든다.
- 기록: 2026-11-12 Task-20261112-117.
