---
summary: 추격자 체형·결손·원화 및 독립 레이어 재질 구현 기준을 확인할 때
status: active
updated: 2026-11-04
source: Task-20261104-102, Source~/V7/integration-verification.json, editmode-appearance-result.json, playmode-appearance-result.json
---

# 추격자 외형 원본

## 언제 읽나
추격자의 체형·관절 봉제·의상·혈흔을 제작하거나 다시 빌드하기 전에.

## 사실
- private Assets/_ThirdParty/curtaincall/Pursuit/Prefabs는 남01~04, 여01~06의10종이다. Models/Pursuit_*.fbx가 체형·결손 정본이다.
- M01 왼발, M03 왼손, M04/F04/F06 머리, F05 왼팔 전체, F06 오른전완/손 결손을 유지한다.
- 최신 사용자 방향은 원래 무표정 민머리 얼굴, 크림색 인형 바탕, 몸에 함께 꿰맨 옷 조각이다. v6 완성형 옷과 도색 얼굴은 대체되었다.
- 최신 원화는 Concepts/JointSeams/V7/*-six-views-v7.png 10장. 원화/초기 제작은 Source~/V7, 최신 봉제 보강/레이어 제작·렌더·검증은 Source~/V8이다. 런타임 TroupeV7의 LayerTextures를 사용한다. private resource/pursuit-joint-seams-v5에 미커밋 보관한다.
- PursuitTroupeV7Importer와 PursuitAppearanceBuilder 메뉴는 원본 Reclaimed 신체 메시에서 UV만 재배치한다. 위치/노멀/삼각형/본/가중치/클립을 보존하며 UV 경계 정점을 복제한다. 추가 의상 형상은 없다.
- Blender 천/실밥 메시와 *-mesh.json은 베이크 원본이며 게임에 넣지 않는다. 알베도/노멀2048, 표면 마스크1024, 모델별 재질1개와 공유 셰이더를 쓴다.
- SurfaceState Clean/Worn/LightBlood/Bloodied/HeavyBlood는 동일 메시/재질을 공유한다. SurfaceSeed는 분포 차이, CaptureCount×BloodPerCapture는 추가 혈흔이다. InjuryBloodEffects.IsVisible로 기본/누적 혈흔 모두 숨긴다.
- 각 프리팹 기본값은 Bloodied(중간)다. 배치는 기본 Varied로 계층 이름/순서의 안정 해시를 사용해5상태로 다양화한다. Appearance/AppearancePattern(0~15)을 씬에 저장하며 같은 씬의 호스트/복제에 동일하게 적용한다. 새 패킷/실행 중 무작위 추첨은 없다.
- 10개×9클립×9표본의 원래 표면 오차0m, 원본 삼각형 총10993개 보존. 최종 미술 승인·4인 성능은 별도다.

- 배치 메뉴 Tools/Curtain Call/Pursuit/Mannequin Designer → 선택 / 순찰 → 외관 레이어. 선택 외관 가까이 보기/저장/Undo 지원. 에디터 미리보기는 혈흔을 표시하며 게임은 사용자 피 숨김 설정을 따른다.

- 레이어 순서: 기본 피부·관절/천 봉제 → 때·마모 → 혈흔. CustomSurfaceLayers 기본false는 기존 선택을 보존하고, 직접 조절 시 DirtAmount/WearAmount/BloodAmount와 WearPattern/AppearancePattern을 독립 저장한다. 피 숨김은 아래 두 층을 유지한다.
- v8는 새 UV/메시 없이 기존 UV에 관절 교차 실밥·바늘 자국을 더 베이크했다. 같은 재질1개에서 합성하며 다른 재질 슬롯/런타임 기하를 추가하지 않았다.

## 확인 방법
Source~/fbx-proportion-audit.json의 원본 해시와 Source~/V8/README.md, V7/integration-verification.json, V8/*-layer-result.json을 확인한다.
Task102 design-brief의 관절 배치표를 따른다. Source~/V6/unity-baseline.json은 적용 전 원본 증거이므로 덮어쓰지 않는다.
