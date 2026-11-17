# 월드 UI 조사 · 2026-11-17

실행 화면이 아닌 현재 브랜치의 코드·프리팹 정적 조사다. 표시 의도와 실제 씬 연결/가시성은 Unity 검증에서 대조한다. 아래 경로는 저장소 루트 기준이다.

## 우선 구현 대상과 공통 묶음

| ID | 상황 | 현재 근거·문제 | 제안 |
|---|---|---|---|
| A1 | 플레이어 착지 그림자 | `LandingShadowMarkers.cs:133`의 공중 원형 표시와 `Rabbit.prefab:43`의 모델 그림자 생성이 병존. 마커의 casting Off는 모델에 영향 없음 | 완성된 원형·외곽선 유지. 모델 몸체 casting만 해제; receiving 유지 |
| A2 | 대기실 추격자 그림자 | `PursuitAppearance.cs:37`의 바닥 표시는 `FacingLight` 기반 방향 안내이며 원형 그림자 아님 | 방향 안내 보존. 테두리 없는 원형 그림자와 몸체 casting Off 비용 비교 |
| A3 | 탈출로 추격자 그림자 | `EscapeUnderstageChase.Doll.cs`의 authored doll이 실외형. legacyMeshes만 꺼서는 안 됨 | 몸체 단위 원형 그림자, 외곽선 없음. 대형 리그 크기에 맞춘 반지름 프로필 |
| F1 | 추격자 그랩 공격 이동 범위 | `EscapeUnderstageChase.Capture.cs:292` Origin→Aim의 프레임별 손 이동 구간과 모든 플레이어 몸체 캡슐을 검사. `:332` SweptHandTouches가 ArmRadius 사용 | 공중 3D 캡슐 통로 경계·방향 대시/끝 화살촉·경계의 예고 바. 지면 원형만 표시하는 v1 제안 철회 |
| B2 | 추격자 예상 착지 위치 | `EscapePursuerBoss.Presentation.cs:85`: LeapWarning/Leaping 동안 Goal에 landingRing, 고정 scale 4. 경로 점프는 `EscapeUnderstageChase.Jump.cs` 별도 흐름 | 아이콘 없는 원형 키트. 보스 도약과 경로/우회 점프 모두 목록에 포함; 후자는 착지 좌표·공유 상태 연결 필요 |
| C1 | 장애물 드랍 가이드 | `HumanCannonHazardCue.cs:33` 예고 시간에 warning 오브젝트 점멸. `EscapeMidEncounterVisuals.cs`와 `EscapeFlameSawRepair.cs`가 여러 막대 메시로 범위 구성 | 실제 footprint에 맞춘 사각형·옅은 빗금·진행 바, 중앙 아이콘 없음. 원형 범위는 B |
| D1 | 장애물 이동 위치 가이드 | `HumanCannonHazard.cs:125` `SetVisible`: 왕복 레일 양끝 직선, cue의 origin/destination 이동은 별도. 위치만 같아도 시간 의미가 다름 | 실제 이동 통로 폭을 표시하는 경계 + 이동 방향 + 목표 끝 표시. 현재 왕복/단방향을 구분 |
| E1 | 대포 탑승 시 짧은 비행 궤적 | `HumanCannonSystem.cs:81` SeatedStation, `HumanCannonStation.cs:56` LaunchVelocity, `HumanCannonPilot.cs:271` 공통 중력·BallisticMotion.Step. 조사 범위에서 전용 궤적 UI 구현을 찾지 못함 | 포구에서 시작하는 공중 곡선 `— — — ▶`, 끝 화살촉 하나. 곡선을 따라 잰 표시 길이를 설정으로 제한 |

## 추가로 확인한 관련 표시

| 우선순위 | 상황 | 현재 근거 | 처리 |
|---|---|---|---|
| 이번 키트 | 추격자 투척물의 예상 낙하 지점 | `EscapeUnderstageChase.Threat.cs:214` PresentThreat. DebrisWarning에만 aimRing; cue.To에서 y -0.9 보정 | 아이콘 없는 B. 비행 중에도 SweepProjectile 접촉이 있으므로 도착 원이 전체 위험을 뜻하지 않음; F 곡선 변형의 편입 검토 |
| 이번 키트 | 붙잡힌 플레이어를 던질 도착점 | `EscapeUnderstageChase.Capture.cs:476` Throw/SwingTime 동안 cue.Drop에 aimRing. `:105` 비행 sweep으로 다른 플레이어 충돌도 검사 | 아이콘 없는 B. 빈 공간의 도착점에 가짜 바닥 원을 만들지 않음; F 곡선 변형으로 공중 위험을 표시할 수 있게 설계 |
| 이번 키트 | 보스 돌진 경로 | `EscapePursuerBoss.Presentation.cs:87` DashWarning에도 도착점 원 하나만 사용 | D의 위험 통로 변형. From→Goal, 실제 몸체/접촉 범위에 맞춰 폭 결정 |
| 이번 키트 | 화염 분출 예고 영역 | `HumanCannonHazardCue`를 사용하는 FireJet, `EscapeFlameSawRepair.cs:29` 기존 사각 점멸선 | 아이콘 없는 C. 기존 피격 범위에 맞춰 크기만 조절 |
| 보존·회귀 | 외줄 안전 착지 강조 | `HumanCannonGroundMarkers.Feedback.cs:35` 짧은 외줄 표시, 기존 안전색과 지지면 내부 클리핑 | 완성된 착지 그림자와 함께 유지. 이번 원화로 임의 재디자인하지 않음 |
| 후속 검토 | 협동 외줄 소멸 예고 | `EscapeCooperativeRopes.cs:177` warningArcs | 선형 위험 재료 재사용 후보. 플레이 가시성 확인 후 편입 |
| 후속 검토 | 추격자 절단 가능 부위 강조 | `EscapePursuerBoss.Presentation.cs:123` 노출 부위 색 tint | 바닥 영역 UI와 별개인 대상 표시. 필요 시 표적 프레임 별도 시안 |
| 별도 HUD 범위 | 보스 절단 진행 텍스트·절단기 조준점 | `EscapePursuerBoss.Presentation.cs:175` OnGUI | 미고도화 후보로 기록하되 월드 바닥 UI 키트와 분리 |

## 그림자 교체의 조건

- 플레이어는 사용자 요청대로 Realtime casting을 해제한다. 현재 원형 표시는 공중에서만 나온다. 지상에서도 원형 그림자를 계속 표시하도록 확장하는 변경은 이번 요청의 확정 사항으로 간주하지 않는다.
- 대기실 방향 안내는 그림자가 아니다. 그 셰이더나 65회/갱신의 지면 투영을 원형 그림자의 필수 비용으로 복제하지 않는다.
- 추격자는 동일 카메라·조명·화질·동작에서 shadow caster draw 수, GPU shadow 패스 시간, CPU 투영 비용, 전체 프레임을 비교한다. 그림자 없는 조명에서는 casting Off의 절감 효과가 없을 수 있다.
- 교체 시에는 보이는 캐릭터당 원형 면 1개, 공유 머티리얼/프로필, 거리·가시성에 따른 투영 갱신을 우선한다. 비용 개선과 지면 접촉 가독성을 모두 확인한 경우 적용한다. 측정값은 아직 없다.
- 모델 전용 렌더러를 캐시하고 inactive 부상 대체 바디까지 포함한다. 소지품·분리된 팔다리·보스 절단 파편은 임의로 변경하지 않는다.
