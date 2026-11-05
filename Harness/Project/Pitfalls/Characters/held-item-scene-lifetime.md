---
summary: 손에 든 기물 표시는 씬을 이동하는 플레이어와 수명을 맞춘다
status: active
updated: 2026-11-05
source: Task-20261105-109
---

# 손에 든 기물의 씬 수명

## 언제 읽나
기물을 든 채 씬을 이동한 뒤 손의 표시가 달라지거나 소지 상태는 남았는데 기물 표시가 사라질 때.

## 원인과 대처
- HumanoidPlayerPresentation에서 parent 없이 만든 heldItemVisual은 생성 당시 활성 씬에 속한다. 플레이어가 유지되어도 이전 씬을 언로드하면 표시가 파괴된다.
- shownItem이 그대로이면 선택 변경 분기로 들어가지 않아 표시가 다시 생성되지 않았고, ItemHandGrip.Apply도 호출되지 않았다.
- 기물 표시는 플레이어 루트의 자식으로 만들고 LateUpdate에서 실제 손에 맞춘다. 표시 유실 복구와 선택 변경을 분리하여 복구 때 꺼내기 동작을 다시 시작하지 않는다.
- 모델 슬롯 자식으로 만들면 모델 교체·래그돌 분리에 수명이 묶인다. 플레이어 루트에 붙이고 소지 표시 정리는 ClearItemVisual이 맡는다.
- 씬 이동과 별개로 Animator 재초기화/외부 포즈 복귀 시 실제 재생 상태가 캐시와 달라질 수 있다. 캐시한 itemLayerState나 HumanoidModel.stateHash만으로 Play를 생략하면 잘못된 손·팔 자세가 남는다. 현재 상태 또는 전환 목적 상태가 원하는 상태인지 함께 검사한다. 비활성 Animator에는 쥠 레이어/손가락 보정을 적용하지 않는다.

## 확인 방법
HeldKeySurvivesSceneUnloadWithoutRestartingHoldPose는 실제 씬 언로드 후 표시 생존·소속 씬·연속 쥠 상태·손목의 급격한 회전 여부를 검사한다. 수정 전 표시 생존에서 실패했고 수정 후 통과했다. 걷는 몸통·왼팔은 기존 HeldKeyKeepsLocomotionTorsoAndLeftArmSwing으로 검사한다.
후속 검사에서 Animator.Rebind 후 쥠 상태 복구가 실패함을 재현했고 실제 상태 검사 추가 후 통과했다. 사용자 화면의 지속 떨림과 이 초기화 결함의 동일성은 자동 검사만으로 단정하지 않는다.
