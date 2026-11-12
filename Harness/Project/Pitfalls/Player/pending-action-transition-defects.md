---
summary: 목마 복귀·메뉴 입력·장치 초기화·사지 조각 충돌의 상태 인계 회귀와 방지 규칙.
status: active
updated: 2026-11-13
source: Task-20261109-114, RehearsalLifecycleTests, InputBindingPreferencesTests, TrampolineCarryPoseTests
---

# 행동 전환과 입력 초기화

목마 상태로 연습을 종료하거나, 메뉴 뒤 F가 누락되거나, 사지 조각 때문에 기립·운반이 막힐 때 읽는다.

- 대포 연습 종료는 살아 있는 참가자의 기물·목마 연결도 해제한다. 클라이언트가 종료 상태를 먼저 받았다면 연결 해제까지 기다린 뒤 Follow/Pilot의 임시 이동 소유권을 반환하고 복귀 잠금을 저장한다. Simulated=false를 그대로 복원값에 저장하거나 링크 해제보다 먼저 순간이동하면 복귀가 다시 동료 머리 위로 덮인다.
- CommonInteractionSelection.Forget은 Held·대상·목마 하차 대상을 함께 지운다. 현재 프레임은 소비된 것으로 유지하고 다음 새 제스처는 선택을 다시 한다. 메뉴 중에는 Selected가 호출되지 않을 수 있으므로 중립 프레임에 의존하지 않는다.
- InputAction.canceled는 물리 키 해제 외에 장치 초기화에서도 발생한다. 눌렀던 장치의 reset/disable/disconnect/remove 알림에서 대기 중 탭·홀드를 폐기한다. 실제 키 해제의 경과 시간은 기존 GameSettings 기준을 사용한다.
- PlayerMover.HasHeadroom은 DismemberedLimb를 제외한다. 이동에서 무시하는 사지 조각을 천장으로 처리하면 앉기·점프가 잠긴다. 실제 벽·천장 차단은 유지한다.
- 트램펄린 운반 sweep에서 사지를 제외하는 것만으로 실제 물리 접촉은 없어지지 않는다. 활성화와 매 FixedUpdate에서 본체·자식 충돌체에 IgnoreCollision을 적용한다. 내려놓음·새 조각·충돌체 재활성화도 포함하고 일반 바닥 충돌은 유지한다.
- 대포 Space 발사는 공통 Action 입력을 사용한다. ActionHeld까지 중립 확인에 포함해야 탑승 전부터 누른 키가 즉시 발사로 재사용되지 않는다. F 탭 발사·F 홀드 하차를 유지하고 동시 입력에서는 하차를 우선한다.

자동 검사는 임시 객체와 실제 코드 경로를 사용한다. 다른 PC 2·3·4인 플레이 통과를 의미하지 않는다. 검사 결과와 기존 전체 검사 실패는 Task-114의 Inventory/remaining-test-failures.md에 구분해 기록한다.
