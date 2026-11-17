---
summary: 승인된 월드 UI의 표적 확정·선택형 경로·설정 연결과 그림자 수명 규칙
status: active
updated: 2026-11-17
source: Task-20261117-153 사용자 추가 지시; 실제 Unity 컴파일·EditMode 검사·Play 캡처
---

# 착지·위협 월드 표시

## 언제 읽나

플레이어 그림자 중복, 탈출로 그랩·착지·장애물 표시, 화면 위험 효과를 변경할 때. 구현·검증 상세는 `Tasks/Active/Task-20261117-153-ingame-world-ui/implementation-report.md`.

## 현재 계약

- 정본 `GameSettings.WorldUi` → `Assets/Resources/Settings/GameSettings/WorldUiSettings.asset`. 프리팹은 `Resources/Prefabs/UIs/WorldIndicators`, 스프라이트는 private `_ThirdParty/curtaincall_world_ui_v1/Sprites`.
- 위험 영역 중앙에는 아이콘이 없다. 실제 크기와 분리된 진행 아크/바만 채워진다. `GrabTarget`은 영역 아이콘이 아닌 대상 지정/공중 점 위치 안내다.
- 그랩 `Preparing` 동안 실제 몸 영역(발 위치+현재 몸 높이68%)을 추적한다. 전투·UI는 GrabAimPoint를 공유하며 머리 위 높이 설정은 제거했다. 기본 준비 .35초 후 마지막 Aim으로 고정, 기존 Warning 시간을 선딜로 사용한 뒤 Strike. 준비 중 취소/거리/장애물을 재검사하며 속도 선행 예측은 사용하지 않는다.
- `showGrabPath`는 기본 false. true여도 Preparing에는 숨기고 고정된 Warning/Strike에서만 Origin→Aim의 구형 손 sweep 경계를 표시한다. Strike에는 손 앞의 남은 구간만 표시한다. Retract/Grab/취소/라운드 무효 시 숨긴다.
- 실제 손 sweep은 모든 유효 플레이어 캡슐을 검사하므로 대상 외의 플레이어도 통로 중간에서 잡힐 수 있다. 경로 표시를 끈 것은 판정 변경이 아니다. Retract에는 손의 새 sweep이 없지만 별도 몸체 근접 포획은 계속 가능하다.
- 네트워크 상태 메시지 v10: Preparing 단계와 확정된 점프 Takeoff/Touchdown/JumpRevision 포함. 서로 같은 빌드를 사용해야 한다. 원격 착지 표시는 보간된 현재 위치에서 추측하지 않는다.
- 투척 도착점/점프 착지는 실제 지지면을 확인하고, 낭떠러지에 가짜 바닥 원을 만들지 않는다. 투척 도착점 원이 비행 중 전체 위험 체적을 뜻하지는 않는다.
- 장애물은 원본 HumanCannonHazard의 physical footprint, cue의 공유 시각과 원점/목표를 읽는다. 왕복 경로는 양 끝 방향 표시. 동적 warning/새 UI는 정적 배칭 대상이 아니다.
- 대포 표시도 Muzzle/LaunchVelocity·공통 중력·BallisticMotion.Step·원본 바람을 사용한다. Cannon Arc Length는 곡선을 따라 잰 거리이며 입력 보정까지 보장하는 최종 착지 예측이 아니다.
- 탈출로 대상 지정/설정 거리 이내 접근은 대기실의 `PursuitEdgeGraphic`을 재사용한다. 카메라의 로컬/관전 대상, 라운드·사망·전환 수명과 겹치는 overlay를 처리한다. 근접 거리는 WorldUi에 있고 스타일은 대기실의 기존 강도·폭·pulse를 공유한다.

## 그림자

- 기존 LandingShadowMarkers는 공중 플레이어 아래의 원형 표시다. 지상/최소 높이 이하에서는 사라지며 상시 지상 그림자를 추가하지 않는다.
- PlayerModelSlot에서 ActorShadowPresenter를 연결하여 몸체 casting만 끈다. inactive 부상 Bodies 포함, receiveShadows·소지품·분리 팔다리는 보존. 분리/설정 해제/비활성 때 원래 모드를 복원한다.
- 대기실 PursuitAppearance의 GroundMaterial은 방향 안내이므로 삭제하지 않는다. 추격자 원형 그림자는 별도 무테두리 quad이며 기본 OFF다. 몸체 shadow pass 절감과 새 투영/렌더 비용을 측정하기 전 성능 개선으로 확정하지 않는다.

## 제작·검증 주의

- Builder의 NewScene 호출은 생성 시 보관한 prefab handle을 해제할 수 있다. 씬 인스턴스는 AssetDatabase 경로로 다시 로드한다.
- UI의 절차적 메시를 편집 모드 테스트에서 정리할 때 DestroyImmediate, 런타임에는 Destroy를 쓴다. 공유 material/texture asset은 파괴하지 않는다.
- 검토 씬은 생산 프리팹을 실제 Play로 렌더하지만 타이밍·인물 움직임·위험 강도는 스크립트로 재현한다. 두 클라이언트의 실전 플레이 증거로 바꾸어 설명하지 않는다.
