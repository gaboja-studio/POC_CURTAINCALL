# 월드 UI 설정 · 사용자 검증표

기준: 2026-11-17. 준비 표적은 **플레이어 발 위치 + 현재 몸 높이의 68%**에 놓인다. 서기/웅크리기/부상에 따른 몸 높이를 실제 그랩과 함께 사용한다. 준비가 끝나면 마지막 조준 지점이 고정되고, 선딜 후 그 방향으로 발사된다.

몸체가 표적/진행 바를 가리지 않도록 렌더 면만 카메라 방향으로 당긴다. 화면상 조준 위치·크기를 보존하며 전투의 Aim 좌표는 바꾸지 않는다.

## 설정 찾기

Unity Project에서 `Assets/Resources/Settings/GameSettings/` 폴더를 연다. 아래 에셋을 선택하고 Inspector에서 해당 항목을 조절한다. GameSettings.asset의 참조가 정본이며, 테스트 씬 전용 복제 설정이 아니다. ScriptableObject 에셋 값은 Play 중 수정해도 남을 수 있으므로 변경 전 값을 기록한다.

### WorldUiSettings.asset — 새 월드 UI 공통

| 조절할 내용 | Inspector 항목 | 현재 값/의미 |
|---|---|---|
| 공격·장애물·대포 월드 표시 | Enabled | ON. 실제 피해 판정은 유지. 화면 위험 효과/그림자는 아래 별도 스위치 |
| 표시 거리 | Max Distance | 카메라로부터 80m |
| 그랩 표적 크기 | Grab Target Size | 0.65m. 표적 브래킷 크기이며 피해 범위가 아님 |
| 그랩 경로 표시 | Show Grab Path | OFF. ON이어도 준비에는 숨김, 위치 확정 후 선딜·발사에만 표시 |
| 그랩 조준 위치 | 별도 UI 높이 설정 없음 | 실제 공격 Aim 사용. 이전 Target Head Offset 제거 |
| 탈출로 화면 테두리 | Escape Threat Enabled | ON |
| 탈출로 근접 감지 | Escape Threat Distance | 18m. 대상 지정이면 거리 밖이어도 효과 발생 |
| 대포 표시 길이 | Cannon Arc Length | 포구부터 곡선을 따라 8m. 충돌이 있으면 더 짧아질 수 있음 |
| 점선·끝 화살표 | Dash Length / Dash Gap / Arrow Size | 0.4 / 0.25 / 0.28m |
| 대포 계산 품질 | Cannon Step Seconds / Cannon Max Samples | 0.035초 / 256 |
| 관전 대상 대포 표시 | Cannon For Spectator | ON |
| 위험 영역 색·선·채움 | Danger / Line Width / Fill Opacity | 위험색 / 0.045m / 0.1 |
| 일반 선색·진행색 | Ivory / Progress Color | 밝은 아이보리 / 황색 |
| 진행 표시 굵기·간격 | Progress Width / Progress Offset | 0.055 / 0.11m. 시간은 원본 행동을 따름 |
| 바닥 띄움·조회 간격 | Surface Offset / Surface Refresh Seconds | 0.035m / 0.1초 |
| 빗금·원 품질 | Hatch Spacing / Hatch Depth / Circle Segments | 0.36m / 0.15m / 64 |
| 플레이어 실시간 그림자 해제 | Disable Player Realtime Shadows | ON. 기존 착지 원형 표시와 중복되는 몸체 casting 해제 |
| 추격자 원형 그림자 교체 | Pursuer Blob Shadows | OFF. 성능 이득 측정 후 채택할 선택 옵션 |
| 추격자 원형 그림자 크기 | Waiting Pursuer Shadow Radius / Escape Pursuer Shadow Radius | 대기실 0.8m / 탈출로 1.8m |
| 원형 그림자 농도·갱신 | Shadow Opacity / Shadow Refresh Seconds | 0.38 / 0.1초 |
| 표시 재료·프리팹 | Style, Circle/Rectangle/Volume/Path/Trajectory/Target Prefab | 연결된 공통 키트. 일반 수치 튜닝에는 교체 불필요 |

### EscapeRouteSettings.asset → Pursuer — 실제 공격 시간·범위

| 항목 | 현재 값 | 의미 |
|---|---|---|
| Grab Prepare Seconds | 0.35초 | 움직이는 플레이어 몸 조준을 따라가는 준비 시간 |
| Hall / Station / Wire Warning | 0.45 / 1.1 / 0.85초 | 위치 확정 뒤 통로 / 대포 탑승 / 봉·외줄 대상 선딜 |
| Boss Attack Warning | 0.55초 | 보스 공격 선딜 |
| Arm Strike Seconds / Arm Retract Seconds | 0.22 / 0.3초 | 그랩 발사 / 회수 시간 |
| Arm Radius | 0.22m | 손 sweep 반지름. 경로 ON일 때 표시 폭도 이 값을 읽음 |
| Boss Leap Warning / Boss Leap Seconds | 0.8 / 1.45초 | 보스 점프 예고 / 비행 시간 |
| Boss Dash Warning / Boss Dash Seconds | 0.2 / 0.3초 | 보스 돌진 예고 / 이동 시간 |
| Debris Warning / Debris Radius | 1.1초 / 0.45m | 추격자 투척 파편의 예고 / 반지름 |

위험 범위 크기를 표적 프리팹 Scale로 바꾸지 않는다. 손·착지·돌진·장애물은 원본의 실제 판정/몸체 크기를 읽는다. 장애물별 원점·목표·주기는 생성 코드/프리팹의 `HumanCannonHazardCue`(Origin, Destination, Warning/Active/Rest Seconds), 피해 footprint는 `HumanCannonHazard`가 제공한다. WorldUiSettings는 이들 표시의 공통 외형을 조절한다.

### 기존 화면 효과와 플레이어 착지 그림자

- `WaitingRoomSettings.asset → Pursuit → Edge Intensity / Edge Width / Pulse Amount`: 현재 0.2 / 0.1 / 0.15. 대기실과 탈출로가 공유하는 화면 위험 테두리 스타일. 탈출로 ON/OFF와 거리는 WorldUiSettings에서 조절한다.
- `BaseGameSettings.asset → Landing Marker`: 기존 플레이어 착지 그림자의 공간별 외곽선, 선명 높이(현재 10m), 번짐 구간(15m), 안전 체크 높이(5m), 안전/비안전 색상, 외곽선 두께·불투명도, 좁은 면 처리. 공간별 ON/OFF는 외곽선 설정이며 그림자 전체를 끄는 설정이 아니다.

## 빠른 외형 확인 — 테스트 씬

`Assets/Scenes/Tests/WorldIndicators/WorldIndicators.unity`를 열고 Play한다. Hierarchy의 `World UI review` → `WorldIndicatorPreview`에서 조절한다.

1. Camera Preset = 1, Frozen Progress = 0.10: 몸 영역 표적, 경로 없음.
2. Frozen Progress = 0.50: 마지막 지점에 고정된 표적. Show Grab Path를 ON/OFF해 통로 비교.
3. Show Grab Path = ON인 채 Frozen Progress = 0.10: 준비 중에는 통로가 여전히 없어야 함.
4. Frozen Progress = 0.82 / 0.95: 발사 / 종료. 종료에서는 표적·통로가 모두 사라짐.
5. Camera Preset = 3: 아이콘 없는 원/사각과 진행 바. 0: 대포 포함 전체. Cannon Arc Length = 3 / 8 비교.
6. Frozen Progress = -1: 자동 반복. Preview Danger Edge는 모양 검토용 강도이며 실제 거리 감지가 아님.

이 씬은 생산 프리팹을 쓰지만 인물·시간·화면 위험 강도는 시연용이다. 아래 실제 플레이 검증을 대신하지 않는다.

## 실제 게임에서 확인할 목록

Start 씬에서 평소 게임 흐름으로 대기실·탈출로에 진입한다. 네트워크 검증은 같은 빌드의 호스트/게스트 두 명으로 진행한다. 아래는 **사용자가 확인할 목록**이며, 실전 통과 기록이 아니다.

| 번호 | 준비·행동 | 정상 결과 |
|---|---|---|
| 1 | 탈출로에서 그랩 대상이 된 채 좌우 이동 | 준비 표적이 머리 위가 아닌 실제 몸 조준 위치를 따라감 |
| 2 | 서기·웅크리기, 부상으로 낮아진 몸, 공중/봉 매달림 상태에서 1 반복 | 현재 몸 높이에 맞는 영역을 조준. 공중 표적을 지면에 투영하지 않음 |
| 3 | 준비 완료 직후 옆으로 이동 | 선딜 중 표적이 따라오지 않음. 손이 고정 표적 방향으로 발사됨 |
| 4 | Show Grab Path OFF로 공격 관찰 | 준비·선딜·발사 모두 전체 경로 없음. 표적은 단계에 맞게 표시 |
| 5 | Show Grab Path ON으로 반복 | 준비에는 경로 없음. 위치 확정 후 표시, 발사 시 남은 구간 표시, 회수/종료에는 숨김 |
| 6 | 다른 플레이어가 발사 중 손의 경로를 가로지름 | 원래 대상이 아니어도 손과 실제로 접촉하면 잡힘. 경로 옵션 ON/OFF와 판정 동일 |
| 7 | 준비 도중 범위 이탈·벽 뒤 이동, 이후 라운드 재시작 | 준비 공격 취소, 남은 표적/통로 없음. 새 라운드에 이전 UI 잔존 없음 |
| 8 | 그랩 대상 지정, 별도로 비대상 상태에서 추격자에 접근/이탈 | 대상이면 거리 밖에서도 화면 위험 효과. 비대상이면 설정 거리 안에서 효과 발생 |
| 9 | Escape Threat Distance를 18→6으로 바꾸고 8 반복, Enabled OFF도 확인 | 근접 효과 시작 거리가 좁아짐. Escape Threat Enabled OFF에서 탈출로 효과 꺼짐 |
| 10 | 사망 후 관전 대상을 전환 | 이전 대상의 위험 효과가 남지 않으며 현재 관전 대상 기준으로 갱신 |
| 11 | 추격자 점프·보스 착지 및 돌진 관찰 | 실제 도착점/경로와 표시 일치. 진행 중 범위 자체가 줄지 않음. 지지면 없는 낭떠러지에 가짜 착지원 없음 |
| 12 | 드랍·이동 장애물과 투척 파편 관찰 | 원/사각/경로가 실제 범위에 맞고 중앙 아이콘 없음. 진행 바가 예고와 맞으며 작동 종료 시 정리 |
| 13 | 대포 탑승·조준 변경·발사/하차, 길이 3m와 8m 비교 | 포구에서 짧은 점선, 끝 화살표 하나. 길이 설정 반영, 발사/하차 뒤 해당 안내 숨김 |
| 14 | 플레이어 점프·낙하와 부상 모델 변경 | 기존 원형 착지 그림자 유지, 몸체 실시간 그림자 중복 없음. 소지품·분리 팔다리 그림자까지 강제로 바뀌지 않음 |
| 15 | Pursuer Blob Shadows ON 후 대기실·탈출로 추격자 관찰 | 테두리 없는 원형 그림자. 대기실 기존 방향 안내는 유지. OFF로 되돌리면 원래 casting 복원 |
| 16 | 호스트/게스트 각자 대상이 되어 1·3·5·8·11 반복 | 양쪽에 같은 대상·확정 위치·착지점. 화면 효과는 각자의 관찰 대상 기준 |
| 17 | 밝은 곳/어두운 곳, 1인칭/관전, 여러 명이 겹친 상황 | 표적과 진행 바가 읽히고 플레이/주변 시야를 과도하게 가리지 않음 |

검토 후 임시 변경은 기록한 값으로 복구한다. 현재 기본은 Show Grab Path OFF, Escape Threat Enabled ON/18m, Cannon Arc Length 8m, Pursuer Blob Shadows OFF다. 추격자 그림자의 성능 이득은 동일 장면·조명·카메라에서 Profiler CPU/GPU 비교가 추가로 필요하다.

피드백은 `번호 / 호스트 또는 게스트 / 장소·자세 / 기대와 다른 현상`으로 남기면 재현하기 쉽다.
