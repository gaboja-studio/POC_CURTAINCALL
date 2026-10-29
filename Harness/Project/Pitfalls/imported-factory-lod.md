---
summary: 공장 프리팹의 LOD 전환값과 근접 카메라 누락을 확인하고 원본 대신 씬 인스턴스에서 보정한다.
status: active
updated: 2026-10-27
source: Task-20261027-073
---

# 공장 에셋의 근접 렌더링 누락

## 언제 읽나
Leartes 공장 프리팹이 조감도에서는 보이지만 근접 카메라에서 사라질 때.

- 탈출로 제작에서 `AbandonedFactory/.../SM_ConcreteFloor.prefab`의 LOD 임계값이 `0, 0, 0.01`로 관찰됐다. Renderer.enabled만으로 실제 카메라의 표시 상태를 판단하지 않는다.
- LODGroup.enabled=false만 적용했을 때에도 근접 캡처에서 구조물이 누락되는 현상이 관찰됐다. 이 배경 초안은 인스턴스의 LODGroup을 제거하고 LOD0 렌더러만 활성화해 확인했다. 원본 프리팹은 변경하지 않았다.
- 임의 크기에 맞춰 배치할 때 모든 LOD의 bounds를 합치면 실제 표시되는 LOD0와 크기가 달라진다. LOD0 bounds로 맞추고 바닥 높이·단절 경계를 별도로 확인한다.
- 프리팹 인스턴스의 Transform/Renderer 변경은 `PrefabUtility.RecordPrefabInstancePropertyModifications`로 보존하고 저장·재개방 후 다시 확인한다.
- LOD0 고정은 공간 검토용 조치다. 배포용 최적화를 완료했다는 뜻이 아니며, 정식 아트 통합 시 올바른 LOD 임계값과 성능을 검증한다.
