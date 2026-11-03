---
summary: 포획 후 공연에서 외팔 판정인데 온전한 팔다리가 보일 때
status: active
updated: 2026-11-03
source: Task-20261102-094 사용자 Play 상태 조사·회귀 테스트
---

# 포획 가림 해제와 부상 메시

- 증상: NetworkPlayer.LostParts=13, HumanoidModel.Stage=3으로 기어가지만 Original이 켜지고 Bodies[2]가 꺼져 온전한 몸으로 보인다.
- 원인: PursuitVictimPose가 Start에서 Renderer.enabled를 기억하고 OnDestroy에서 복원했다. 포획 중 순차 절단이 선택한 메시를 포획 전 상태로 덮어썼다. 다음 장소에 모델이 유지되면 부상 변경 이벤트도 다시 오지 않는다.
- 수정: enabled는 HumanoidModel의 부상 표시가 소유한다. 포획 연출은 Hidden이 될 때 forceRenderingOff만 임시 적용하고, 해제 시 그 플래그의 원래 값만 복원한다.
- 검사: 데이터/Stage뿐 아니라 Original·각 Bodies의 enabled와 forceRenderingOff도 확인한다. 숨김 중 부상 변경·가림 해제 2회, 실제 호스트 포획 후 공연 이동 2회에서 외팔 메시 유지 확인.
- 테스트: DevStartLauncherPlayTests.PursuitCapture_HideAndReleasePreserveCurrentInjury / PursuitCapture_OneArmMeshSurvivesPerformanceTransfer.

## 공연 진입 후 정지 상태에서 부유
- 메시가 올바르더라도 ItemCrawl_Cut3로 CrossFade 중 Animator.speed=.0001이면 전환도 느려져 서 있던 포즈가 남는다. 실제 재현에서 rootY=.08/hipsY=.96/transition=true, 수정 후 hipsY=.28/transition=false.
- HumanoidModel.Play는 정지용 속도(.001 이하)로 새 모션을 선택할 때 즉시 Play+Update(0)로 샘플링한다. 걷기/달리기 등 정상 속도의 전환은 기존 CrossFade를 사용한다.
- 포획 해제 시 저장한 높이는 부상 전 값일 수 있다. Animator를 복원한 뒤 현재 LostParts로 Apply(spawn=false)하여 모델 소유의 부상 높이와 메시를 복원한다. 토끼 높이 보정은0이라 이번 부유의 직접 원인은 전환 지연이며, -.05 보정 모델도 별도 검사한다.
- 회귀는 부상 메시뿐 아니라 포획 해제 높이, 공연 진입 시 기어가기 상태/전환 종료, CharacterController 접지 및 실제 허리 높이를 확인한다.
