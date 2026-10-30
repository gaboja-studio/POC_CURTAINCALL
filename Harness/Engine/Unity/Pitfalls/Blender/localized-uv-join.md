---
summary: 현지화된 primitive UV 이름과 직접 만든 UVMap을 병합하면 빈 첫 UV 채널이 생길 수 있다
status: active
updated: 2026-10-30
source: Task-20261030-082
---

# 병합 FBX에서 일부 부품 색이 바뀜

## 언제 읽나
Blender의 개별 부품은 올바르지만 Unity로 합친 모델의 아이보리·안감이 같은 색으로 표시될 때.

## 원인과 대처
Blender 5.2 한국어 primitive의 기본 `UV 맵`과 직접 만든 `UVMap`은 join 시 서로 다른 채널로 남았다. 첫 UV 채널이 없는 부품이 (0,0)을 샘플링했다. 모든 입력 메시의 active UV 이름을 `UVMap`으로 통일한 뒤 병합하고 FBX를 내보낸다. 원본 부품의 UV를 보존한다.

## 확인
병합 후 UV 레이어 수와 Unity Mesh.uv의 아틀라스 타일 분포를 확인한다. Task-082 `OriginalPropAtlasKeepsIvoryUvAndNonMetallicRoughFabric`이 아이보리 타일이 실제 첫 채널에 있는지 검증한다.
