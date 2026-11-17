# 월드 UI 구현 · 검토 안내

## 최신 사용자 지시

2026-11-17 최신 피드백: 그랩은 준비 중 실제로 겨누는 플레이어 몸 영역(발 위치 + 현재 몸 높이의 68%)에 표적을 표시한다. 전투와 UI가 같은 좌표 계산을 공유하며, 준비 완료 뒤 마지막 위치를 공중 월드 표적으로 확정한다. 선딜·발사는 확정한 지점을 사용한다. 전체 경로는 기본 OFF이며, 켜도 위치 확정 뒤에만 표시한다. 위험 범위 중앙 아이콘은 계속 없고, 새 표적은 대상 지정/점 위치 안내다.

탈출로 추격자의 공격 대상이거나 설정 거리 안에 접근한 경우 대기실과 같은 화면 가장자리 위험 효과를 사용한다. 준비 시간·기존 선딜·표시 옵션·위험 감지 거리는 공통 설정에서 조절한다.

## 키트

- 스프라이트: `Assets/_ThirdParty/curtaincall_world_ui_v1/Sprites/` — CircleFrame, CircleFill, RectangleFrame, RectangleFill, Hatch, Dash, Arrowhead, ProgressTrack, TargetReticle. 독립 RGBA PNG, 흰색 알파 마스크, 게임 색은 설정에서 tint.
- 프리팹: `Assets/Resources/Prefabs/UIs/WorldIndicators/` — DangerCircle, DangerRectangle, DangerPath, AirborneGrab, CannonTrajectory, GrabTarget.
- 공통 자식 레이어: Fill / Boundary / Hatch / Progress / Direction / Arrow. 필요한 레이어만 렌더한다. collider·shadow caster 없음.
- 원/사각/통로의 경계·빗금·진행 메시를 월드 크기로 생성하여 범위를 늘려도 선 두께가 늘어나지 않는다. 전체 프레임/빗금/트랙 마스크는 키트에 보관하며 현재 정본 뷰는 Stroke(Dash) 재료로 해당 선을 조립한다. 모든 sprite가 각자 draw call을 만드는 구조가 아니다.
- 승인 원화와 채택하지 않은 생성 마스크는 private `Source~`에 보존한다. 생산 마스크는 native Unity analytic authoring으로 재작성했다. 시안 합성 이미지를 잘라 쓰지 않았다.

## 설정과 연결

정본: `Assets/Resources/Settings/GameSettings/WorldUiSettings.asset` → `GameSettings.asset`의 World Ui.

| 설정 | 의미 |
|---|---|
| Show Grab Path | 위치 확정 뒤에만 추가하는 공중 경로, 기본 OFF |
| Grab Target Size | 표적 크기. 피해 반지름 아님. 조준 위치는 실제 공격 좌표를 사용하며 Target Head Offset은 제거 |
| Escape Threat Enabled / Distance | 탈출로 화면 위험 테두리·근접 거리(기본 18m), 대기실 On/Off와 독립 |
| Cannon Arc Length | 포구부터 곡선을 따라 잰 최대 길이, 기본 8m |
| Dash Length / Gap / Arrow Size | 점선·한 개의 끝 화살촉 크기 |
| Danger / Fill Opacity / Line Width | 아이콘 없는 위험 영역 색·채움·선 두께 |
| Progress Width / Offset | 실제 준비시간 진행 표시 |
| Disable Player Realtime Shadows | 플레이어 몸체 casting 해제, 기본 ON |
| Pursuer Blob Shadows | 추격자 원형 그림자 교체, 기본 OFF(성능 이득 미확정) |

스크립트는 `IWorldIndicator`/`WorldIndicatorView`를 공통 계약으로 사용한다. 기능별 adapter는 실제 공격 반지름·목표·공유 시각·장애물 footprint를 읽는다. UI에는 피해 판정을 복제하지 않는다. 새로운 그랩 준비 단계와 위치 확정은 사용자 추가 지시에 따른 전투 동작 변경이다.

그랩 준비 시간은 `EscapeRouteSettings.asset → Pursuer → Grab Prepare Seconds`(기본 .35초). 선딜은 같은 곳의 Hall/Station/Wire Warning, 보스는 Boss Attack Warning이다. 화면 테두리의 강도·폭·pulse는 기존 `WaitingRoomSettings → Pursuit → Edge Intensity/Width/Pulse Amount`를 공유한다. v10 상태 메시지이므로 네트워크 peer는 같은 빌드가 필요하다.

플레이어 기존 착지 마커는 그대로 사용하며 몸체의 Realtime casting만 해제한다. 부상 대체 바디를 포함하되 소지품·분리 팔다리는 바꾸지 않고, 모델 분리·설정 해제 때 원래 casting을 복원한다. 추격자 원형 그림자는 테두리가 없고 shared material·단일 quad·주기적 지지면 조회를 사용한다. 최종 GPU/CPU 비용 비교가 없는 상태에서 성능 개선 완료라고 주장하지 않는다.

## 테스트 씬

`Assets/Scenes/Tests/WorldIndicators/WorldIndicators.unity`를 열고 Play. `World UI review`의 Preview에서:

- Frozen Progress: -1 자동 반복, 0.10 대상 추적, 0.50 위치 고정/선딜, 0.82 발사, 0.95 종료.
- Camera Preset: 0 전체, 1 표적·공중 그랩, 2 측면, 3 지면 위험 영역.
- Preview Danger Edge: 같은 생산용 `PursuitEdgeGraphic`의 검토 강도 표시.
- 경로 On/Off·길이·색은 위 정본 설정에서 조절한다. 검토 후 바꾼 에셋 값은 의도한 경우에만 저장한다.

이 씬은 생산 프리팹의 시각 검토용이다. 타이밍·이동 인물·화면 위험 강도는 재현 가능한 검토용 값이며, 두 클라이언트 네트워크 실전 플레이 녹화가 아니다. 실제 어댑터/공유 상태는 별도 회귀 검사로 확인한다.

## 검증 기록

- 최신 몸 영역 조준 수정: PursuerWorldUiTests 24/24, WorldIndicatorTests 9/9 통과. 몸 높이 1.8m/0.9m에서 UI와 확정 Aim 일치, 원근/직교 카메라에서 몸 가림 보정 전후 화면상 위치·크기 보존을 검사했다. Evidence/tests-body-aim.json, tests-body-target-projection.json.

- Unity 6000.3.25f1 실제 컴파일 PASS. check-work 안전성·문서·컴파일·공통 설정 검사 PASS. 자동 테스트는 check-work 옵션으로 재실행하지 않고 아래 개별 실행 결과를 기록했다.
- Pursuer 회귀 **348/348**, 공통 WorldIndicator **7/7**, ActorShadowPresenter **6/6**, WarningBatch **2/2**, PursuitFeedback **15/15** 통과. 중복 포함 suite별 수치이며 하나의 전체 실행 수치로 합산하지 않는다. 결과 JSON은 Evidence에 있다.
- alpha audit: 독립 마스크9종의 유효 픽셀 RGB가 모두 흰색이며 alpha가 형태를 결정한다. 원화의 배경색/글자가 마스크로 들어가지 않는다.
- 실제 Play frame587/time9.80 → frame1355/time23.21 → frame5327/time89.93 → frame12817/time215.40 진행 확인.
- 옵션 ON + 준비(.1): target=true/path=false. 종료(.95): target=false/path=false. 3m/8m 궤적 캡처 후 설정을 기본값(showGrabPath=false, cannonArcLength=8)으로 복구.
- 캡처 중 게임 런타임 예외는 없었다. CLI `unity status`가 설치된 구형 Pipeline에 `editor status`를 보내는 도구 오류1건은 확인·기록했으며, 직접 `editor_status` 조회는 정상이다.
- 두 클라이언트 실전 플레이, 4인 중첩/1인칭 최종 가독성, 추격자 그림자의 GPU/CPU 성능 비교는 미실시. 사용자 최종 검토 항목으로 남긴다.

## 실제 Play 캡처

04–06은 Game view의 화면 효과까지 포함한다. 07–09는 Play 중 Review Camera를 직접 캡처하여 HUD overlay가 없다. 모든 인물·타이밍은 위 검토 씬의 더미/스크립트다. 01–03은 추가 사용자 지시 전 이력이며, 04의 머리 위 표적은 최신 몸 영역 조준 방식으로 대체되었다. 설정별 위치와 실제 플레이 검증 순서는 [manual-verification.md](manual-verification.md)를 따른다.

| 파일 | 상태 |
|---|---|
| [10-body-target.png](Evidence/10-body-target.png) | 최신 준비 표적: 몸 조준 영역, 진행 바 가림 보정, 경로 OFF. 시연 씬 Play 카메라 캡처 |
| [04-target-tracking.png](Evidence/04-target-tracking.png) | 준비 중 머리 위 표적·경로 없음·위험 테두리 |
| [05-target-locked.png](Evidence/05-target-locked.png) | 이동한 인물과 떨어진 고정 공중 표적·기본 경로 OFF |
| [06-optional-locked-path.png](Evidence/06-optional-locked-path.png) | 위치 확정 후 경로 옵션 ON |
| [07-iconless-areas.png](Evidence/07-iconless-areas.png) | 아이콘 없는 원형/사각 범위·독립 진행 표시 |
| [08-overview-final.png](Evidence/08-overview-final.png) | 전체 키트·대포8m |
| [09-cannon-3m.png](Evidence/09-cannon-3m.png) | 같은 카메라에서 대포3m 비교 |
