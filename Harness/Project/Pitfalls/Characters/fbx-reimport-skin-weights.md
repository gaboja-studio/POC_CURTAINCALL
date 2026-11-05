---
summary: Blender FBX 재수입 후 원래 Unity 애니메이션의 신체 변형이 달라질 때
status: active
updated: 2026-11-04
source: Task-20261104-102, PursuitTroupeV6Importer.cs, Source~/V6/integration-verification.json
---

# FBX 재수입 가중치와 스킨 좌표계

## 언제 읽나
기존 Unity 스킨에 Blender 의상을 결합하면서 기존 애니메이션을 그대로 보존해야 할 때.

## 함정과 해결
- 같은 FBX라도 Blender와 Unity importer의 가중치 정리 결과가 다를 수 있다. bind pose만 같아 보이는 것으로 원래 변형 보존을 판정하지 않는다.
- Pursuit v6는 Unity 원래 신체 vertices/normals/triangles/bindposes/boneWeights를 직접 재사용하고 추가 의상만 결합했다. 의상 생성도 Unity 원본 가중치를 참조하도록 맞췄다.
- 이 프로젝트 Pursuit는 renderer scale100이다. BakeMesh 기본 호출과 useScale:true의 좌표를 섞으면100배 잘못된 비교/경계를 얻었다. 이번 구조에서는 BakeMesh(mesh,true)로 원본/새 메시를 동일 좌표계에서 비교하고 localBounds를 계산했다. 다른 스킨은 실제 transform/scale을 확인해야 한다.
- 기존9클립을 각9표본 샘플링하여10종 원래 신체 표면 오차0m를 확인했다. 추가 의상 관통·최종 외관은 별도 시각 검토가 필요하다.
