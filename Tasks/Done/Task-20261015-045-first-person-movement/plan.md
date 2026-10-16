# Plan

## Context

- CC-131: 기본 Platform 이동은 Course 축을 사용하며 CameraViewDirector.ViewForward 연결이 없다. 사용자 요구는 1인칭 수평 시선 W/S·기본 이동 적용·3인칭 유지다.
- 조사 근거는 통합 first-person-movement-issue.md. 코드 도출 예만 있으며 실제 키 입력 재현은 미실행이다. 2026-10-15 PM 사용자 정식 배정·별도 worktree 생성 승인, 구현은 작업자 세션에서 시작한다.

## Approach

1. 착수 후 수정 전 코스 +Z·카메라 yaw 90도에서 W/S 실제 변위 재현·실패 테스트를 확보한다.
2. PlatformControlScheme에서 활성 1인칭·로컬 대상 일치·유효 ViewForward일 때만 카메라 수평 전방/우측 wish를 계산한다. 길이 1 이하로 제한한 뒤 기존 CourseForward/Right에 투영하여 SetMoveInput에 전달하는 최소안을 검토한다.
3. 3인칭·카메라 없음·로컬 불일치·관전은 기존 호출 유지. Course 축 자체·외줄·공중 운동·점프·부상 속도·네트워크 권한은 바꾸지 않는다.
4. 회귀 테스트와 실입력 검증 후 fix/first-person-movement → integration/common-game-systems Squash PR. 035 구현에 섞거나 병합된 037/038 브랜치를 재사용하지 않는다.

## Files

- 수정: setup에 배정된 PlatformControlScheme 및 ControlRulesTests·CameraViewsPlayTests, 전용 테스트 폴더
- 읽기·호출: CameraViewDirector, PlayerMover, PlayerController, ControlSettings, NetworkPlayer, 기존 CameraViews/ControlRules 씬

## Risks

- wish를 먼저 제한하지 않으면 Mover의 축별 clamp 때문에 회전한 대각선 방향·속도가 왜곡될 수 있다.
- 카메라/다른 로컬/관전 상태에서 잘못 변환하거나 콘텐츠 전용 입력까지 회전시키지 않는다. UI·카메라 입력 차단 기능 추가는 별도 범위다.

## Auto Verification

- check-work: 구역 → 문서 → 컴파일 → EditMode/PlayMode. 테스트 없음은 NO_PROJECT_TESTS, 실패·환경 제약은 그대로 기록한다.
- EditMode: yaw 0/90/180·W/S/A/D·대각선 길이, 3인칭 비변환, Default와 fallback 경로
- PlayMode: 실제 Input System 입력→변위, pitch 수평 속도, 구역 이탈 3인칭 복귀·로컬 불일치·관전 fallback. 기존 카메라 9개·조작 테스트 유지
- 합성 상태 주입과 실제 키보드/마우스 관찰을 분리 기록한다. Console 오류·경고 확인.

## 공동 테스트 항목

2026-10-15 PM 사용자 정식 배정 요청에 따라 아래 3개 항목 확정. 실행 시 실제/예상·참여자·일시·로그 또는 캡처를 handoff에 기록하며, 계획 확정은 테스트 통과가 아니다.

1. CameraViews.unity 또는 구성 완료한 FirstPersonMovement.unity, PM 1명 호스트: 기본 Platform 및 프로젝트 Default/fallback으로 1인칭 진입, yaw 0/90/180에서 W/S·A/D·대각 입력. W는 수평 시선 전방·S 반대, pitch로 속도/상하 변위가 바뀌지 않고 대각선 과속 없음.
   - 2026-10-15 PM 범위 확대 추가: FirstPersonMovement.unity의 3인칭 비고정 구역(z 18~26)에서 마우스로 시선이 돌고 카메라가 캐릭터 뒤 3인칭 위치에서 따라 돌며, W/S/A/D가 1인칭과 같은 시선 기준. 이동 키를 누른 채 1인칭/고정 3인칭/비고정 구역을 넘나들면 키를 뗄 때까지 방향 유지(1인칭↔비고정은 끊김 없음).
2. 같은 호스트: 구역 이탈 3인칭 후 기존 이동·Space 점프·부상 속도·UI 차단 확인, proto_0.0.2에서 외줄 조작 확인. 변경 전 동작 유지 예상. API 주입은 실제 입력 증거와 구분.
3. 동일 빌드 호스트 A·참가 B 2명: 각자 시선을 다르게 하고 이동, A/B 교대 후 사망·Retired 관전/복구 확인. 자기 이동만 자기 시선 적용, 원격 이동·모델·관전·복구는 기존 동작 유지 예상.
