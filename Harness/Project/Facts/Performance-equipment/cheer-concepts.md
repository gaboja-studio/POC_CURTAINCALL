---
summary: 보유 Synty 고리·불꽃·가면을 조합한 환호 포인트 시안 3종과 독립 공연장 갤러리
status: active
updated: 2026-11-18
source: Task-20261117-155
---

# 환호 포인트 시안

## 언제 읽나
고리·가면 환호 포인트 프리팹을 열거나 실게임 획득 판정을 연결하기 전.

## 사실
- private 폴더 `Assets/_ThirdParty/curtaincall/CheerPointConcepts/`: `A_FireHoop.prefab`, `B_EncoreHalo.prefab`, `C_MaskApparition.prefab`.
- A는 화염·불티, B는 발광 금속·회전 불티, C는 붉은 화염 고리 안의 광대 가면. 그네와 혼동되는 상부 막대·케이블은 사용자 요청으로 제거했다. 원본의 금이 간 표현을 주장하지 않는다.
- 원본은 PolygonHorrorCarnival의 `SM_Prop_Hoops_Hoola_01`, `SM_Chr_Attach_Clown_Female_Mask_01`, `FX_Fire_01`, `FX_Sparks_01`. 화염 텍스처는 PolygonGeneric의 `Gen_FireFrames_Grey_01.png` 10×10 시트다. 파생 URP 재질은 시안 폴더에 둔다.
- `CheerPointVisualPreview`가 부유·기울임·광원 맥동을 재생한다. 시각 프리팹 자체에는 콜라이더·대미지가 없다. 사용자는 A 불 고리를 선택했으며 불은 무해하다.
- `PerformanceCheerPoint`와 `PerformanceCheerRuntime`이 고리 내부 평면 통과·호스트 선착순 보상·공유 획득 상태를 처리한다. 외줄·인간대포 실제 씬 각각 5개 초기 배치. 외형 전환은 `GameSettings.PerformanceEquipment.cheerStyle`에서 가능하다.
- 메뉴 `CurtainCall/Performance/Cheer Point Placement`는 고리 추가/선택과 외형 설정을 제공한다. 판정 평면은 시각 부유와 분리한다. 위치를 편집한 기존 코스는 PlacePoints가 덮어쓰지 않는다.
- 씬 `Assets/Scenes/Tests/MainStage/CheerPoints/CheerPointGallery.unity`는 실제 `performance_background.unity`의 배경을 복사한 독립 비교 씬이다. 원본 배경/플레이 코스의 배치 변경이 아니다. 비교용 조명·색보정이 포함된다.
- 메뉴 `CurtainCall/Preview/Build Cheer Point Concepts`, `Open Cheer Point Gallery`. 생성기는 위 시안 파일을 재생성하므로 수동 수정 후에는 덮어쓰기 여부를 확인한다.

## 확인 방법
- 갤러리를 열고 Play하여 3종 움직임을 확인한다. `CheerPointConceptBuilder.Capture`/`CaptureMotion`은 실제 Unity 카메라로 PNG/48프레임을 만든다.
- 공용 코드·갤러리와 private 파생 에셋 모두 있어야 참조가 복원된다.
