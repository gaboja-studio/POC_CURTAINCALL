# 1인칭 이동 연결 이슈

## 사용자 요구 (2026-10-15)

- 1인칭에서는 카메라가 보는 수평 방향이 전방: W는 전방, S는 후방. FPS식 이동을 프로젝트 기본 이동에도 적용한다.
- 3인칭은 현재 구현을 유지한다. 외줄 제약 이동 규칙 변경으로 확대하지 않는다.

## 조사 결과

- 사용자 제보와 코드 경로로 연결 누락 확인. 이번 조사에서 Play·키 입력으로 2~3회 재현하지는 않음. 현재 Console 오류/경고 0·compileFailed false는 기능 정상 근거가 아님.
- `Assets/Scripts/Systems/Camera/CameraViewDirector.cs:59-63`: IsFirstPerson·로컬 대상·수평 ViewForward 공개됨.
- `Assets/Scripts/Systems/Control/PlatformControlScheme.cs:25-29`: 카메라 방향을 읽지 않고 Posture/Move를 Mover에 그대로 전달.
- `Assets/Scripts/Systems/Player/PlayerMover.cs:364-381`: 자유 이동은 CourseForward/Right로 월드 이동을 계산. 카메라 yaw와 코스축이 다르면 W/S가 시선과 다른 방향으로 이동함.
- ControlSettings의 프로젝트 기본 에셋과 빈 설정 fallback 모두 PlatformControlScheme을 사용. 입력 바인딩 W/S 자체는 정상.
- 예: 코스 +Z·카메라 yaw 90도(+X)에서 W 기대 +X / 현재 계산 +Z, S 기대 -X / 현재 계산 -Z. 코드 도출 예이며 실행 관찰 아님.

## 권장 수정 범위 (아직 미구현)

- 별도 공용 조작–카메라 연결 Task를 소속 통합에 준비한다. 035의 DummySpaces 작업 구역에서 고치거나, 병합된 037/038 브랜치를 재사용하지 않는다.
- PlatformControlScheme에서 활성 1인칭·로컬 대상 일치·유효 수평 방향일 때만 카메라 기준 wish를 계산하고, 길이 1 이하로 제한 후 기존 코스축에 투영해 Mover에 전달하는 최소안.
- 3인칭·카메라 없음·로컬 불일치·관전은 기존 경로 유지. CourseForward 자체, 외줄 제약 이동, 공중 운동·부상 속도·네트워크 권한은 변경하지 않는다.
- UI 입력 차단과 카메라 SetInputBlocked 연결은 별도 잔여 항목이며 이번 방향 수정에 몰래 포함하지 않는다.

## 검증할 항목

1. 수정 전 기본 이동으로 yaw 0/90/180도에서 W/S 실제 변위 재현·실패 근거 확보.
2. 1인칭 W/S·A/D, pitch 변경에도 수평 속도 유지, 회전된 대각선 방향·속도, 프로젝트 Default와 fallback 경로.
3. 구역 이탈 3인칭 복귀·카메라 없음·다른 플레이어·사망/리타이어 관전 fallback·입력 차단·점프·부상 회귀.
4. ControlRulesTests 단위 검사와 CameraViewsPlayTests의 입력→실제 변위 회귀 추가, 컴파일·테스트 후 사람 키보드/마우스·2인 확인.

## 상태

- 원인 조사 완료. 사용자 요청(10-15)에 따라 Task-20261015-045 / fix/first-person-movement 준비, Jira CC-131 등록(https://hrjoo122770.atlassian.net/browse/CC-131). 전용 빈 씬·좁은 수정 구역·공동 테스트 3개 확정. 10-15 PM 사용자 정식 배정 요청으로 assigned(71cb485), sync #83 Merge 수신 완료(42779cc). 구현·실제 키 입력 재현·담당자 실제 전달 확인은 미실행.
- 038 기존 공동 테스트 PASS는 시점/모델/관전 범위로 유지하며, FPS 이동 완료로 해석하지 않는다.
