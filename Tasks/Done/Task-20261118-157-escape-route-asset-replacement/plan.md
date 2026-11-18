# Task-20261118-157

- **Title:** 탈출로 마지막 구간 침지 추격자 컨테이너
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/escape-route-asset-replacement
- **Base:** dev

## 대상

탈출로 A의 `Assets/Scenes/Levels/escape_route.unity` → `25_FinaleHazards_A/12_DropFinal` 한 곳. 포박된 추격자가 액체에 완전히 잠긴 유리 컨테이너를 천장 설비가 떨어뜨리고, 받침에 내려앉은 탱크를 옆 롤러가 수납구 안으로 보낸다. 현재 단계는 계획이다. 데스크톱 미병합 작업이 dev에 들어오면 낙하 위치와 주변 공간을 다시 대조한다.

## 보유 에셋 조합

| 부위 | 소스 | 조합 |
|---|---|---|
| 탱크 골격 | `Assets/_ThirdParty/Conveyor Works/Prefabs/Structure/StructureSteelBeamSmall.prefab`, `Assets/Scenes/Levels/EscapeFactoryArt/factory-steel.mat` | 네 모서리 기둥과 상·하부 프레임. 하부에는 강철 받침을 두고 기존 4.5 × 3 × 4m 낙하 박스 안에 맞춘다. 카메라가 처음 보는 면은 기둥 사이를 넓게 비운다. |
| 유리·수면 | `Assets/Scenes/Levels/EscapeFacilityMotion/settler-laminated-glass.mat`, `settled-liquor.mat` | 기존 연한 녹색 유리를 얇은 면에 적용한다. 내부 청록색은 옅게 표현하고, 액체 재질은 상부 수면 한 장에 쓴다. 탱크 높이 약 85~90%를 채우고 머리 위에도 액체가 보이게 배치한다. 수면선과 기포로 액체 깊이를 읽히게 한다. |
| 추격자 | `Assets/Resources/Models/Characters/Enemies/CircusGolemRevision7/circus-golem-look.fbx`와 같은 폴더의 `Materials/` | 얼굴과 봉제 의상이 보이는 쪽을 진입 카메라에 사선으로 돌린다. 탱크 안에서 머리를 숙이고 다리를 굽힌 조형으로 배치하며 기존 게임플레이용 추격자와 독립된 외형을 사용한다. |
| 포박 표현 | `Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed/Old costume straps.mat` | 재질을 단순 띠 메시와 탱크 뒤 지지대에 사용한다. 몸통 앞을 가로지르는 띠와 팔 쪽 띠가 한눈에 보이도록 배치한다. 얼굴과 흉부 전체를 띠로 가리지 않는다. |
| 기포 | `Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelBubbles.mat`와 기존 `Bubble_Texture.png` | 탱크 바닥 양옆에서 소량을 올리고 수면 아래에서 사라지게 한다. 평상시에는 드문 기포, 착지 순간에만 짧은 기포 증가를 준다. |
| 투입·반출 설비 | 씬의 `OverheadDrop_03` 호이스트, `Assets/_ThirdParty/Conveyor Works/Prefabs/Conveyor/Rotor/ConveyorBeltStraightRotor.prefab`, `Assets/_ThirdParty/Dzendor/StylizedFactory/Prefabs/DoorsAndWindows/SM_Shutter_Roller_4m.prefab` | 기존 호이스트 아래 탱크가 매달리는 투입부를 연결한다. 착지점 바깥 방향으로 롤러를 이어 측면 셔터 안에서 탱크가 가려지도록 한다. 롤러 높이는 탱크 하부 받침과 맞춘다. |

## 동작 순서

1. **예고·낙하.** 투입부 아래에 유리 탱크와 내부 실루엣이 먼저 보인다. 현재 dev의 0.9초 예고와 0.85초 수직 낙하를 기준으로 기존 타이밍과 피해 판정을 사용한다.
2. **착지.** 하부 프레임이 먼저 받침에 닿는다. 탱크 본체는 수직을 유지하고 수면만 짧게 출렁인다. 기포를 한 차례 올려 충격을 보여준다.
3. **측면 이송.** 현재 2.8초 휴지 구간에서 약 0.2초 정지 후 약 1.9초 동안 진행축 바깥으로 이동한다. 나머지 시간은 탱크 후단이 셔터 안으로 들어가 다음 주기의 위치 전환을 가리는 데 쓴다. 측면 이송은 시각 연출로 연결한다.

## 구현 지시 후 검증

- 작업 시작·재개 전과 제출 직전에 Unity 종료를 확인하고 `origin/dev`를 fetch해 작업 브랜치에 반영한다.
- 먼저 진입, 착지, 이송 세 장면을 기본 3인칭 시야에서 본다. 얼굴과 포박이 읽히고, 머리 위 수면이 보이며 유리·액체·기포의 투명 렌더 순서가 이동 중에도 안정적인지 확인한다.
- 롤러·수납구와 기존 동선이 겹치지 않는지, 탱크가 수납구에서 벽을 뚫거나 노출된 상태로 사라지지 않는지 확인한다. 기존 낙하 회피 폭과 피해 구간을 대조한다.

[보유 에셋 조사표](../../../Harness/Project/Facts/asset-package-inventory.md)
