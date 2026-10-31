# Task-20261101-089

- **Title:** 플랫폼 점프 공중 조작·자유 3인칭 시점 통일
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/movements/jump
- **Base:** dev

## Context
같은 플랫폼 조작(줄·대포 등 공연 오브제를 쓰지 않는 일반 지면)이 공간마다 다르지 않게 한다(사용자, 2026-11-01).
1. 점프 후 이동: 탈출로처럼 점프 뒤 공중에서 방향을 바꾸고 이동할 수 있게 모든 플랫폼에 적용한다.
2. 카메라: 플랫폼 기본 시점을 자유 3인칭(마우스 공전, `FreeThirdPerson`)으로 통일한다.
   공연 출발/도착 플랫폼, 대기실, 탈출로 플랫폼(공연 오브제 사용 외), 대포 비행 중.

## 현재 구조 (조사 결과)
- 공중 조작은 `PlayerMover.FreeAirControl`. 지금은 `HumanCannonControlScheme`만 켠다.
  탈출로·인간 대포 씬에는 `HumanCannonSystem`이 있어 이 규칙이 붙으므로 공중 조작이 된다.
  대기실·외줄 플랫폼은 기본 `PlatformControlScheme`이라 이륙 속도가 고정된다.
- 공용 카메라 리그(`PlayerFollowCamera`)는 탈출로 3종·인간 대포·외줄·대기실 씬에만 있다.
  요청이 없으면 `CameraViewSettings.DefaultThirdPerson`(고정 3인칭)을 쓴다.
- 1인칭 요청: 외줄 플랫폼(`TightropeCameraFraming`), 대포 평상시·비행(`HumanCannonPilot.Camera` flightView, 우선순위 50/200),
  탈출로 계단 끝 이후(`EscapeSceneArrival` firstPersonView, 우선순위 75). 대기실 구역(`CameraViewZone`)은 이미 자유 3인칭.

## 계획
1. 점프: `PlatformControlScheme.Enter/Exit`에서 `FreeAirControl`을 켜고 끈다. 대포 규칙의 중복 설정은 지운다.
   외줄 규칙(옆줄 점프 방향 고정)·배치 미리보기 규칙은 그대로 둔다.
2. 카메라 기본값: `CameraViewSettings.DefaultThirdPerson`을 `FreeThirdPerson.asset`으로 바꾼다.
   관전·사망 중에는 디렉터가 같은 거리값의 고정 추적으로 처리하므로 관전 동작은 유지된다.
3. 외줄: 플랫폼 1인칭 대신 공용 자유 3인칭 사본을 요청한다(줄 위 구도 맞춤 3인칭은 유지).
4. 대포: 평상시·비행 시점을 1인칭 사본 대신 자유 3인칭 사본으로 바꾼다. 탑승·운반 시점은 유지.
5. 탈출로: 계단 끝 이후 1인칭 요청 대신 요청을 해제해 공용 자유 3인칭을 쓴다. 접근 구간 근접 3인칭은 유지.
6. 관련 테스트·문서(조작 지도, 탈출 진입 결정) 갱신, `check-work` 실행.
7. 카메라 벽 설정을 탈출로에도 같게 적용(사용자 추가 지시): 스프링 암 충돌 + 고정 구조물 반투명 제외.
   판정은 `CameraViewDirector.UsesWallCollision`(활성 씬 `WaitingRoomBase`, `escape_route*`) 하나로 모은다.
8. 테스트 도구(사용자 추가 지시): Dev 시작 창(F8)에 탈출로 바로 시작, Dev 치트 창(F9)에 공연·탈출로 순간이동·추락 무시·되살리기·재시작.
   탈출로는 대기실 합류를 건너뛰어 직접 개발 모드로 동작한다(출발점 배치·사망 후 복구는 `EscapeDevelopmentSession`). 안내: `Docs/Guides/Ui-scene-integration/dev-start.md`.

## 미정·주의
- `escape-entry.md`의 "계단 끝 1인칭"은 이번 지시로 대체된다.
- 공용 `CameraViewDirector`·플레이어 조작 규칙 수정이므로 카메라(086)·외줄·대포 담당과 겹칠 수 있다. PR에 적는다.
- 실제 플레이 체감(공중 조작 속도, 카메라 거리)은 사람이 확인한다.
