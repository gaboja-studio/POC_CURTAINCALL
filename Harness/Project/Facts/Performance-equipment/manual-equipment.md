---
summary: 수동 공연 기물 4종, Q/E 슬롯 입력과 무해한 환호 고리의 공통 구현 경로
status: active
updated: 2026-11-18
source: Task-20261117-155, 사용자 확정, Confluence 44793873
---

# 공연 기물 수동 발동

## 언제 읽나
공연 기물 발동·내구도·매달림 시간·환호 수집·사용키를 수정할 때.

## 구현
- `GameSettings.PerformanceEquipment` → `Resources/Settings/GameSettings/Items/PerformanceEquipment.asset`. 점프 높이/구조 거리·각도/장갑 시간/벌룬 시간·하강속도·조작 배율/고리 외형·판정·환호량.
- `NetworkPlayer.PerformanceEquipment`가 슬롯의 UID를 서버에 요청한다. 신발/벌룬은 소유자 효과 적용 확인 뒤 정확한 UID의 내구도를 1 소모한다. 처리 중 해당 아이템 버리기는 제한한다. 무효 조건·실패에는 소모 없음.
- 신발: 일반 PlayerMover 및 대포/봉 비행 HumanCannonPilot 모두 상승 속도를 적용한다. 착지 전 1회, 기존 자동 착지 반동 호출은 폐기. 트램펄린 확정 접촉도 새 체공이다. 매달림 자체로 사용권을 초기화하지 않는다.
- 구조 장갑: 전방 가까운 공중/매달림 동료, 화면 안·시야 차단 없음, 내 주변 안전 지지면/빈 캡슐 공간·이동 경로를 검사한다. 소지자 전용 대상 표식과 현재 사용키. 서버가 그 대상을 재검증·예약하고 소유자가 기본0.8초 동안 CharacterController.Move로 이동한다. Owner NetworkTransform이 중간 위치를 공유하며 Teleport를 호출하지 않는다. [구조 표식과 이동](rescue-pull.md).
- 끈끈이: 외줄 HangBook은 정지 구간과 실제 Tick 시간의 교집합만 제외한다. 공통 봉은 각 Rider의 마지막 처리 시각으로 계산하여 중복 요청의 시간 증가를 막는다. 고정 위험물 재가동 시각은 연장하지 않는다. 놓기/다른 매달림/사망 시 효과 종료.
- 벌룬: 하강 속도 상한과 공중 수평 조작 배율. 최대3초, 한 체공1회, 원래 사용 슬롯의 재입력은 접기. 마지막 내구도로 칸이 비어도 접을 수 있다. 지상/매달림/사망 시 종료.
- `Resources/Input/PlayerControls` Common/UseSlotOne(Q), UseSlotTwo(E). 기존 InputBindingPreferences/CommonSettingsUI 조작 탭·저장/복원·Reader 경로 사용. CommonInventoryUI는 실제 바인딩과 발동 가능 상태를 표시한다. 1/2 칸 선택·기존 탐색 사용 흐름은 유지한다.
- 내구도 감소·소진은 기존 PropCarry.LastUse → 공통 슬롯 연출. EquipmentUseVisual은 4종별 월드 연출이며 마지막 내구도 후에도 효과 수명 유지. [사용 연출과 녹화](use-effects.md).

## 확인 및 주의
- `DevStartLauncherPlayTests.Performance_*`: 실제 캠페인 호스트의 통과 보상·기물 소모/제한/접기·매달림 정지·구조·입력 재지정.
- `PerformanceEquipmentTests`: 고리 중앙/외부/역방향/고속 통과, 봉 시간과 위험물 시간 분리, 무해한 3종 외형 참조.
- 별도 클라이언트 프로세스와 사람 공동 플레이는 자동 호스트 테스트와 별개다.
- 초기 수치는 밸런스 확정값이 아니다. 통과 가능 경로·조작감은 실제 플레이로 조정한다.
