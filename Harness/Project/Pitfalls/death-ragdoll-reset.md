---
summary: 공연 종료 조작 정리의 BalanceReset이 사망 레그돌까지 복구함. 사망한 몸이 서 있거나 공중에 남는 문제를 조사할 때
status: active
updated: 2026-10-29
source: Task-20261021-069, Task-20261027-076
---

# 사망 레그돌이 조작 정리 때 복구됨

## 언제 읽나

외줄·대포에서 Fallen 확정 뒤 모델이 다시 서 있거나 레그돌이 사라질 때.

## 원인

SetBalanceActive는 ResetBalance를 부르고 BalanceReset은 PlayerRagdoll.Restore를 호출한다.
외줄 공연 종료의 ReleaseControls가 이를 실행해 사망 모델을 원래 슬롯에 다시 만들었다.
ForceFall은 이미 HasFallen이면 Fell을 다시 발행하지 않아 확정 사망 때 시각 보장이 누락될 수도 있다.

## 해결

- 콘텐츠 정리는 사망 상태를 유지하면서 SetDisplayedState로 균형 표시만 끈다.
- PlayerRagdoll.Restore는 네트워크 Fallen 동안 복구하지 않는다. 정상 부활·재시작은 Normal 확정 뒤 복구한다.
- 확정 Fallen은 ForceFall 여부와 별개로 멱등 GoLimp를 보장한다.
- 레그돌은 isKinematic=false·useGravity=true·constraints=None을 명시한다.

## Humanoid 매달림 포즈가 그대로 떨어질 때 (2026-10-28)

- 기존 Humanoid 제품 모델에는 뼈 Rigidbody가 없어 루트 한 덩어리 낙하만 적용됐다. Animator를 끄더라도 Hanging 포즈 자체는 풀리지 않는다.
- `PlayerRagdoll.GoLimp`에서 분리 직후 `HumanoidModel.EnableRagdoll`로 남은 뼈에 Rigidbody·CharacterJoint를 구성한다. 루트 BoxCollider는 끄고 뼈의 CapsuleCollider만 쓴다. 비Humanoid는 기존 fallback을 유지한다.
- Hanging 중 관절을 만들면 Hanging이 제한각의 기준이 된다. 생성 때 저장한 중립 뼈 회전에서 관절·뼈 방향 축을 구성한 뒤 실제 사망 포즈로 복귀한다. 동일 회전장 대신 위/아래 관절에 서로 다른 초기 회전을 줘 상대 포즈가 풀리게 한다. 자기 충돌과 절단된 사지의 관절/충돌은 제외한다.
- 스킨의 루트는 추락 시작 위치에 남으므로 `updateWhenOffscreen`을 켜 뼈를 따라 경계가 갱신되게 한다. 물리 낙하 검사는 모델 루트 대신 뼈 Rigidbody를 읽는다.
- 회귀는 Animator 중지·관절 개수만으로 충분하지 않다. 2cm 상대 이동 기준은 실제3cm만 움직이는 Hanging 잔류도 통과했다. `HangingDeathImmediatelyReleasesAnimatedArmsAndFalls`는 몸통 기준 손/표시 스킨20cm 변화를 검사한다. 절단은 정적 BakeMesh 대신 독립 복제 뼈·스킨·무릎/팔꿈치 관절을 쓰며 원본 뼈 참조가 없어야 한다. 조각 렌더러의 원본 메시 좌표계를 유지해야 생성 순간 캡/위치 오차를 막는다. 절단4종은 초기 정점3mm·관절15°/스킨3cm 변형·색·복구를 검사한다.

## 줄에 박힌 래그돌·흔들리는 손 접점 (2026-10-29)
- 줄에는 보이는 메시보다 넓은 `ContinuousSupport` 보행 충돌체가 있다. 공중에서의 GoLimp 검사만으로 실제 매달림 사망의 줄 이탈을 보장할 수 없다. 사망 래그돌은 `TightropeCourse.IgnoreRopeCollision`으로 등록된 줄 지지면만 제외한다(바닥·플랫폼 유지).
- 팔을 완전히 편 상태의 새 손 목표는 도달 범위를 벗어나므로 몸을8cm 올려 팔꿈치 여유를 둔다. 줄 두께 중심에 손을 맞추고 모델 흔들림 후 양손 IK를 적용한다. `Base.Presentation`의 Rope/PoleHangSwayAngle로 추가 흔들림을 각각 조정한다.
- `DeathRagdoll_HangingTimeoutClearsActualRopeSupport`는 실제 외줄 호스트·줄 지지면·7초 만료·0.6초 물리 낙하를 검사한다. 손 접점은 Animator/IK가 끝난 `WaitForEndOfFrame`에 측정한다(프레임 중간에는 보정이 해제되어 오차가 크게 측정됨).
- 공중 사망용1.5m 카메라 하강을 지면 사망에도 적용하면 땅을 뚫는다. CameraViewDirector는 사망 위치 아래 지면을 감지하면 하강 없이 지면 위 최소 높이/거리에서 사망 지점을 조준한다. 공중 하강도 SphereCast로 바닥 앞에서 제한한다. 지면/공중·Brain 출력과 대포 호스트 ServerFallInjury→4곳 절단 사망 회귀를 함께 검사한다(통제 바닥, 실제 입력·두PC는 별도).
