---
summary: Unity Behavior 추격 그래프·발소리·렌더 숨김·포획 좌표 및 유혈 진입점
status: active
updated: 2026-11-03
source: Task-20261102-094 사용자 요청, Unity 패키지 코드와 PlayMode 회귀
---

# 추격 인지·포획 연출

## 언제 읽나
추격 AI의 반응, 화면에서 사라지는 마네킹, 포획 각도나 유혈 표시를 수정할 때.

- `com.unity.behavior@1.0.16`의 실제 authoring/compiled 그래프를 사용한다. `_ThirdParty/curtaincall/Pursuit/Behaviour/PursuitBehaviour.asset`를 Director.BehaviourGraph에 연결한다. Inspector에서 그래프를 열어 개별 동작과 우선순위를 볼 수 있다.
- Actor.Behaviour는 준비/추격/조사/확인/수색/순찰 leaf Action의 조건·실행을 제공한다. 반복 Try In Order가 행동을 선택한다. BehaviorGraphAgent의 자동 Update는 꺼두고 호스트 Director.ServerTick에서만 수동 tick한다. Reset 후에는 그래프 Restart가 필요하다(End만 하면 started 플래그는 남음).
- PursuitBehaviourBuilder는 1.0.16의 internal authoring API에 editor-only reflection을 사용한다. 런타임은 패키지 공개 API만 사용하며 Builder 재실행은 기존 그래프를 덮어쓰지 않는다.
- Director.Perception은 호스트 위치 변화+NetworkPlayer.PresentationSprinting/IsAirborne로 발소리를 생성한다. 각 Actor는 소리 발생 위치만 기억하고 시야 확인 전에는 소리 주인의 Transform을 추적하지 않는다. Stage별 청각·조사·준비·수색·추적 거리를 조절한다.
- 렌더 거리 판단은 최대 추격속도+플레이어 달리기속도 × (최소5초+.3초) + 여유8m. 모든 플레이어/카메라 거리·카메라 시야·포획을 보수적으로 유지한다. Renderer.forceRenderingOff만 임시 적용하며 AI/NavMesh/모션·동기화는 끄지 않는다. 기존 forceRenderingOff 값도 복원한다.
- 포획 Snapshot의 anchor/upright는 누울 몸 좌표를 고정한다. VictimPose는 -88°로 누웠을 때 머리 축과 골반 위치를 제공하고 Actor LateUpdate(1100)는 VictimPose(1000) 이후 그 위치/방향으로 정렬한다. 포획 중 일반 상체 yaw 보정을 중단한다.
- PursuitCaptureBlood는 InjuryBloodEffects.IsVisible(게임+개인 혈흔 설정)을 따른다. 포획 중 하나의 파티클 시스템에 반복 분출하고 피해자 Canvas의 최대12개 혈흔을 재사용한다. 설정 OFF/Hidden이면 즉시 clear, Pose 파괴 시 함께 제거한다. 에셋은 위 Behaviour 폴더에 있다.
- 회귀 `PursuitBehaviour_` 2개: 호스트 그래프 행동 전환/걷기·달리기 청각 차이/렌더 숨김 중 이동 및 복귀, 실제 휴머노이드 네 방향 머리 축 정렬/유혈 ON·OFF/포획 종료 정리.
- F9 대기실 치트는 `PursuitDirector.Debug`를 통해 타이머/단계를 조작한다(Dev+RestTime+호스트/오프라인). 안전 구역은 Running을 즉시 멈추므로 즉시 만료는 `DebugSetRemaining(0)` 대신 `DebugBeginPursuit`로 Timer=Pursuit를 직접 설정해야 한다.
- 단계 고정/자동 선택은 `CurrentStageRules`를 공유하여 AI·발소리·알림에 같이 적용한다. 철회는 ResetStep+빈 피해자 Snapshot을 전송하며 수신 측은 누락된 피해자 Pose도 제거한다. 부상을 보존하며 리타이어만 풀 때는 `ServerRestoreDevInjury(LostParts)`를 사용한다(일반 공연 이동용 ServerReleaseFromPursuit는 외팔 강제).
- 단계별 `PatrolLeashDistance`(배치 창: 순찰 경로 이탈 허용)는0이면 무제한. 개별/공용 순찰 경로의 닫힌 선분까지 3D 최단 거리로 판단하며 경로가 없으면 최초 배치점 기준이다. 추격자/대상 이탈 시 타겟·소리·수색을 지우고 가까운 순찰 지점으로 복귀한다. 복귀 중 재탐지/청각 반응 금지, 도착 후 재개. AlwaysNearest도 이 제한을 지킨다.
- 일반 단계는 시야 상실 유예(`LoseTargetSeconds`, 기본3초) 또는 추격자↔대상 거리 `max(SightRadius,TrackingRadius)` 초과 시 마지막 위치 수색. AlwaysNearest는 이 조건을 무시한다. 배치 창에는 공통 유예와 각 단계의 실제 조건, 현재 온전한 플레이어 걷기/달리기 기준 속도를 표시한다.
