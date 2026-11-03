---
summary: 탈출로 공장 외형 교체 때 전체 패키지 재임포트·원본 변경·첫 프레임 누락을 피한다
status: active
updated: 2026-11-02
source: Task-20261027-073 공장 외형 적용
---

# 공장 외형의 선별 재사용

- AbandonedFactory와 Steampunk Clock Factory의 기존 private 모델/텍스처부터 조회한다. 다운로드 unitypackage에는 중첩 URP 패키지가 있으므로 전체 import 전에 구성과 기존 GUID를 대조한다.
- 업체 모델을 정규화하거나 합친 메시도 private Task073/FactoryMeshes에 둔다. 이번에 쓰는 경량 텍스처만 별도 복제해 원본과 다른 씬의 importer 설정을 보존한다.
- 벽·바닥의 충돌/편집용 오브젝트와 움직이는 기믹은 유지하고, 참조 없는 정적 장식만 24m 구역별로 묶는다. LODGroup 없는 disabled LOD만 정리하며 실행 중 켜지는 톱/배선 템플릿은 보존한다.
- URP17 공장 콘크리트 셰이더는 _CLUSTER_LIGHT_LOOP, renderinglayer 인스턴싱과 RenderingLayers.hlsl이 필요하다. 누락하면 에러 없이 점광원 조명이 빠진다. SRP Batcher 호환 코드0도 확인한다.
- 편집 렌더 첫 준비 프레임에서 셰이더가 누락될 수 있다. 같은 카메라를 한 번 더 렌더해 확인하고 형상 삭제로 오판하지 않는다. 실제 Play/FPS/로딩 시간 검증과 구분한다.
