# Task-20261113-125

- **Title:** 공연장 배경 밝기 및 연출 고도화
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/main-stage-background-enhancement
- **Base:** dev

## Context

공용 공연장 배경의 밝기와 동적 연출을 조정하고, 기존 소품·재질·이펙트의 교체 및 추가 후보를 비교한다.

## 계획

1. `Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity` 원본과 인간 대포·외줄의 실제 게임 카메라에서 같은 구도로 기준 화면을 캡처한다. 묘기에서는 `PerformanceBackgroundLoader`가 배경을 1/3 배율로 로드하고 광원 세기를 보정하며 `PerformanceBackgroundEnvironment`가 앰비언트와 안개를 적용한다.
2. 관중석, 외곽 철골, 하부 정비 구역의 밝기를 각각 비교한다. 기존 작업등·쇼 스폿·빛기둥과 안개부터 조정해 플레이어와 코스의 시인성, 하부의 깊이를 확인한다.
3. `28_DynamicShow`와 `31_ShowExpansion`의 스폿 스윕, 크레인 움직임, 톱 접촉 스파크, 안개를 기존 track과 Scene 미리보기로 조정한다. 원본 씬과 실제 묘기 카메라에서 움직임과 화면 대비를 비교한다.
4. 후보 에셋은 소수의 프리팹·메시·텍스처부터 시험한다. 프로젝트 환경인 Unity 6000.3.25f1 / URP 17.3에서 머티리얼과 셰이더를 확인하고, 실제 플레이 카메라에서 전후 화면과 프레임 시간을 비교한다.
5. 작업 시작·재개 전과 제출 직전에 Unity 에디터 종료를 확인하고 `origin/dev`를 fetch해 현재 작업 브랜치에 반영한다.

## 에셋 후보

[Unity 에셋 뷰어](https://devel-rocket-education.github.io/unity-assets-viewer/)에서 후보를 골라 제품 페이지와 현재 체크아웃을 대조했다.

| 우선 | 후보 | 적용 위치와 방식 | 호환성 및 확인 항목 |
|---|---|---|---|
| 1 | [Abandoned Industrial Factory Environment](https://assetstore.unity.com/packages/3d/environments/industrial/abandoned-industrial-factory-environment-262233) | 외벽 서비스 구역의 소품 교체. `SM_BigPipe`, `SM_Cablespool`, `SM_Metalplane-rust`로 배관, 케이블 릴, 녹슨 패널을 시험한다. | `Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Prefabs`에 추적된 원본이 있다. 스토어는 URP를 지원하지만 현재 체크아웃에는 해당 URP 프리팹이 없어, 기존 메시와 URP 재질 조합부터 확인한다. |
| 2 | [200 Smoke & Fog & Mist Texture](https://assetstore.unity.com/packages/2d/textures-materials/200-smoke-fog-mist-texture-275875) | 하부 `DistantAmberHaze`의 텍스처 교체 후보. 기존 안개 카드의 반복 패턴을 줄이고 측면에서 카드 경계가 드러나는지 확인한다. | 스토어 URP 호환 표기. 기존 안개 재질에서 텍스처를 쓸 수 있는지와 투명 오버드로를 확인한다. |
| 3 | [Industrial Elevator Modular](https://assetstore.unity.com/packages/3d/props/industrial/industrial-elevator-modular-265318) | 하부 외벽 타워 한 곳에 녹슨 정비용 리프트를 추가해 수직 깊이와 움직임을 강조한다. | 스토어 URP 호환 표기. 1/3 적용 배율에서 실루엣과 도어·리프트 애니메이션을 확인한다. |
| 4 | [Fire & Smoke - Dynamic Nature](https://assetstore.unity.com/packages/vfx/particles/fire-explosions/fire-smoke-dynamic-nature-217775) | 심부 배기구 한 곳에 연기·불씨를 추가하거나 기존 환경 파티클과 교체한다. | 스토어 Unity 6000.3.9f1의 URP 호환 표기. 연기 겹침과 GPU 프레임 시간 확인이 필요하다. |
| 예비 | [Industrial Warehouse Pack](https://assetstore.unity.com/packages/3d/environments/industrial/industrial-warehouse-pack-285176) | 중·원경 정비 발판의 상자·드럼통·호이스트 교체 후보. | 스토어 URP 호환 표기, 3.4 MB. 가까이서 보이는 기존 철골·기계와 재질 밀도를 비교한다. |

## 미정·주의

밝기와 스폿 속도는 실제 카메라 전후 비교 후 정한다. 에셋별 Unity 6 임포트, 실게임 시야, 성능은 후속 검증 항목이다.

## 2026-11-13 적용 범위

관중석과 배경 조명의 과노출을 줄이고 외줄 2m 간격 표식으로 전진을 읽게 한다. 선두 플레이어가 결승에 접근하면 공연장 조명을 서서히 감광하고 생존 플레이어를 개별 스포트라이트로 비춘다. 폐공장 배관과 녹슨 패널은 외벽 한 구역에서 먼저 비교한다.

## 공용 배경 공간 재배치

공용 배경 내부에 여섯 구역의 정비 발판을 서로 다른 높이로 배치하고 교량·철골·배관으로 연결한다. 밧줄 표식은 제거한다. 묘기 씬과 묘기 요소는 읽기 전용이며, 코스 조명과 결승 조명을 공용 배경에서 운용한다.
