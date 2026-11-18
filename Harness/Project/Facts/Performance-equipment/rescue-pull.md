---
summary: 소지자 전용 구조 대상 표식, 연속 끌어당김과 모든 피어의 위치 동기화
status: active
updated: 2026-11-18
source: Task-20261117-155, 사용자 구조 장갑 피드백
---

# 구조 장갑 표식과 끌어당김

## 언제 읽나
구조 대상 선택·표시, 이동 중 잠금, 위치 동기화 또는 관련 테스트를 수정할 때.

## 경로
- `NetworkPlayer.RescueTargeting`: 거리/전방 반각, 화면 안/카메라 시야·몸 시야, 대상 공중/매달림 상태, 안전 도착 공간과 캡슐 경로 검사. 가장 가까운 유효 대상 한 명을 선택한다.
- `RescueTargetIndicator`: 소유자에게만 생성하는 ScreenSpaceOverlay. 표적 머리 위 금빛 표식·해당 슬롯의 현재 재지정 키. NetworkObject가 아니므로 다른 피어에 생성되지 않는다. 입력 잠금/기물 없음/내구도0/구조 중/가림 시 숨긴다.
- 표식 외형은 `Assets/Resources/Prefabs/UIs/Roles/Retained/Interaction/RescueTargetMarker.prefab`. `GameSettings.PerformanceEquipment.rescueTargetVisual`로 연결한다. `RescueTargetMarkerView`가 위치·0.16초 등장·1.5px 부유·재지정 키캡을 갱신하며 코드에서 UI 하이어라키를 생성하지 않는다.
- 기존 장갑 슬롯 아이콘, world_ui_v1 원형/포인터, performance_hud_v1 정본 키캡을 참조한다. 문장 라벨 없이 아이콘 메달+키캡으로 구성. 프리팹의 개별 Image/RectTransform과 View 모션값으로 조절한다. 입력을 가로채지 않으며 타겟 소실 시 즉시 숨긴다. 재지정 키 문자열은 30~96px 키캡/자동 글자 크기로 수용한다.
- 생성 도구: `RescueTargetMarkerAuthoring.Build`. 외부 패키지 복사본에는 Sprite 임포트가 없는 파일이 있으므로 실제 HUD가 참조하는 정본 스프라이트를 사용한다. 임의로 원본 임포트 설정을 바꾸지 않는다.
- `GameSettings.PerformanceEquipment.rescueTargetOpacity`(Inspector: 구조 대상 표식 불투명도)는 기본0.75, 범위0~1. 아이콘·테두리·배경·키캡 전체 CanvasGroup에 등장 알파와 곱해 적용한다. 실행 중 변경도 다음 프레임 반영. 0은 숨김, 1은 원래 불투명도이며 대상 선택·구조 규칙에는 영향 없음.
- `RequestEquipmentRpc`가 선택한 NetworkObjectId를 전송하고 서버가 같은 대상의 물리 조건을 재검증한다. 실패 시 다른 동료로 몰래 바꾸거나 내구도를 소모하지 않는다.
- `NetworkPlayer.RescuePull`: 서버가 대상의 `rescueCaster`를 예약하여 중복 구조를 차단한다. 소유자 RPC가 기존 탑승·매달림을 풀고 PlayerController/일반 이동·중력을 잠근다.
- `GameSettings.PerformanceEquipment.rescuePullSeconds` 기본0.8초. SmoothStep의 연속 CharacterController.Move를 기존 소유자 권한 NetworkTransform이 모든 관찰자에게 전달한다. 이동 중 Relocate/Teleport 없음.
- 완료 시 일반 조작/중력/균형과 추락 추적을 복구. 서버는 완료 보고와 도착 위치를 기다린다. 사망/재시작/소환 해제/구조자 부재/시간 초과는 예약·잠금을 정리한다. 새 장애물에 막히면 관통하지 않고 현재 위치에서 종료한다.
- `EquipmentUseVisual.ShowRescue`는 실제 움직이는 대상에 금빛 줄/링을 붙인다. 나머지3종 연출에는 변경 없음.

## 검사
- `Performance_RescueRequiresSafeTargetAndMovesItWithoutInjury`: 화면/벽 가림 표식, 내구도, 중간 이동·중복 제한, 도착 후 복구.
- `RescueNetworkPlayTests`: 실제 NGO/UTP 호스트·대상 소유 클라이언트·관찰 클라이언트 3피어의 중간 위치 전달. 같은 프로세스의 독립 NetworkManager를 사용한다.
- `CaptureEquipment_Rescue`: 실제 인간대포 공연, 소지자 표식과 연속 이동을 녹화한다.
