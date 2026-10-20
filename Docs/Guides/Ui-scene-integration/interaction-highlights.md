# 공통 상호작용 표시와 대기실 셔터
2026-10-20 사용자 사진/요청 후속. 기존 Assets 프리팹과 실제 상호작용 API를 사용한다.

## 셔터와 전화기
- 실제 동쪽 통로: X=10, Z=70, 폭3m, 높이3m. 기존 셔터는 Z=67.5에 있고 부모 Z 배율이 약1.61이라 통로보다 넓었다.
- `WaitingRoomGreybox/NightSchedule/WaitingRoomShutter`를 Z=70, Z 배율1로 수정했다. 기존 패널 폭3.1m(양쪽5cm 겹침)·높이·밤 잠금/아침 개방 애니메이션은 유지한다.
- 내부 판정 Area_EastRooms의 남쪽 경계를 실제 북쪽 복도 벽 Z74로 맞춰 Z70 통로가 대기실로 집계되지 않게 했다. Area_Expert의 이중 Z 오프셋도 실제 전문가실 바닥 범위(X10~18/Z74~83)로 맞췄다. 퇴장/재진입 규칙 코드는 변경하지 않았다.
- 전화기는 **대기실 서쪽 벽, 배송구 위**, world **(-18,1.45,70)** 의 `Phone`이다. 기존 작은 임시 모델/위치를 유지하고 밝은 외곽선과 `F 수화기 들기 (상점)` 안내로 식별한다.
- 상점 품목창과 스케줄 작성창은 기존 기능을 유지한다. 이번 C6 교체는 사물 근접 안내다.

## 화면 구성과 편집
- 공통 씬: `Assets/Scenes/UI/CommonInteractionUI.unity` → `UI/InteractionPromptCanvas/30_Prompts/C6_InteractionPrompt`.
- 원본: `Assets/Resources/Prefabs/UIs/Interaction/InteractionPrompt.prefab`. 프리팹 연결·계층·그래픽·파일명·9-slice 유지. 외부 인계 package는 사용하지 않는다.
- Lobby/WaitingRoomBase/Tightrope/HumanCannon의 `UI`에 `InteractionSceneBinding`. `CommonUiSceneLoader`가 로컬 additive로 한 번 로드하고 마지막 대상 공간이 해제되면 닫는다. NGO 동기화에서는 제외한다.
- 거리/외곽선 굵기/색: 공통 씬 `UI > CommonInteractionUI` Inspector. 기본 **4m/3px**, 최대 **2개**.
- C6는 대상 화면 위치를 따라가며 화면 밖으로 나가지 않게 제한한다. Play 중 위치는 자동 계산한다. 편집 모드에서는 기존 `Preview selected UI in Scene View` 메뉴로 프리팹 구성을 볼 수 있다.

## 데이터와 선택
| 공간 | 대상 | 데이터 원천 |
|---|---|---|
| 대기실 | 전화기, 배송 물품, 바닥 기물, 인계 선반, 스케줄표, 이용 가능한 전문가 방 커튼 | `ShopReach.Targets` + `IShopPresentation`; 이름·실제 이동 좌표·사용 가능 여부를 매 프레임 읽음 |
| 로비 | 기존 F 상호작용 사물 | `LobbyInteractable.Prompt(out usable)`; 기존 로비 입력/안내 유지 |
| 인간대포 | 빈 대포, 잡을 수 있는 트램펄린/봉 | `HumanCannonSystem`, `HumanCannonTrampoline`, `HumanCannonPoleSystem`의 로컬 상태/점유/거리 API; 기존 공연 안내 유지 |
| 외줄 | 등록된 사물 대상 | 공통 씬 연결. 현재 외줄 장애물/불/플레이어는 F 사물 대상으로 등록하지 않음 |

- 플레이어 기준 범위 안, 카메라 화면 안, 벽 등에 가려지지 않은 **이용 가능한** 사물 중 가까운2개를 선택한다. 사물 이동/시선/삭제/비활성/점유/입력 차단을 반영한다.
- 밝은 메시 셸만 임시 생성한다. 원래 재질/색/충돌체를 바꾸지 않으며 원본 LOD와 투명 가림 상태를 따른다. 선택 해제/씬 종료 때 생성 메시·셸을 해제한다.
- 대기실 C6의 실제 F 대상은 기존 `ShopReach.Focused`. 표시 범위4m 안이지만 조작 거리 밖이면 `가까이 다가가세요`와 비활성 안내를 표시한다. 2개를 강조해도 F는 기존 판정의 하나만 실행한다.
- 스케줄표도 동일한 F 대상 선택 경로에 등록해 한 번의 입력으로 여러 창이 열리지 않게 했다. 실제 작성/준비/밤 진행 로직은 유지한다.
- 상점/스케줄 창이 열려 입력이 막히면 외곽선과 C6를 숨긴다. 대체된 대기실 `[F]` IMGUI 안내만 숨기며 독립 테스트 씬에서는 기존 안내로 돌아간다.

## 실제 수정 파일 (이번 후속)
- 신규: `Assets/Scenes/UI/CommonInteractionUI.unity`, `Assets/Scripts/UI/SceneIntegration/{CommonInteractionUI,InteractionOutline,InteractionSceneBinding}.cs`, `Editor/InteractionSceneInstaller.cs`, `Tests/PlayMode/InteractionPresentationPlayTests.cs` 및 각각 meta.
- 기존: 위4개 공간 씬, `ProjectSettings/EditorBuildSettings.asset`, SceneIntegration의 `CommonUiSceneLoader.cs`, `UiRuntimeFontFallback.cs`.
- 기존 대기실 코드: `ShopStorage/{ShopReach,ShopPhone,ShopDeliverySlot,FloorPropsView,HandoverShelfView}.cs`, `NightSchedule/ScheduleBoardView.cs`, `ExpertService/ExpertRoomView.cs`.
- 회귀 테스트: `Assets/Scripts/Systems/SpaceLink/Tests/PlayMode/SpaceLinkPlayTests.cs`의 셔터/퇴장 위치를 실제 통로 Z70으로 수정하고 아침 통로 충돌 해제를 확인한다.
- 현재 Task와 적용/검증 문서. 이전 대기실 HUD의 미커밋 변경도 함께 보존되어 있다.

## 사용자가 확인할 것
1. WaitingRoomBase Scene View에서 셔터 프레임이 동쪽 통로와 맞는지 확인. Start부터 진행하여 밤에는 실제 통로를 막고 다음 아침에는 지나갈 수 있는지 확인한다.
2. 서쪽 벽 배송구 위 전화기에 접근. 외곽선/C6 → F 짧게 누르고 떼기 → 상점 창 → ESC → 이동 복귀를 확인한다.
3. 바닥 기물3개를 가까이 놓고 이동·시선 회전. 보이는 가장 가까운2개까지만 강조되며 벽 뒤/범위 밖/주운 물건의 강조는 사라져야 한다.
4. 스케줄표 F/ESC 및 밤 전원 준비, 인간대포의 빈 대포/트램펄린/봉, 공간 전환 뒤 중복 없는 UI를 호스트·클라이언트에서 확인한다.

검증 수치/미실행 항목은 `interaction-verification.md` 참조.
