# Setup

## JIRA

- 관련 JIRA: CC-131 — https://hrjoo122770.atlassian.net/browse/CC-131

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/Systems/FirstPersonMovement/` — PM이 FirstPersonMovement.unity 빈 씬 생성·재열기 확인. 구현 담당자가 전용 검증 구성을 채운다.
- 스크립트 폴더: `Assets/Scripts/Systems/FirstPersonMovement/` — 전용 테스트·테스트 준비 코드만. 제품 코드 중복 구현 금지.
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Systems/Control/PlatformControlScheme.cs` — 반환된 037 기본 이동의 최소 수정
- `Assets/Scripts/Systems/Control/Editor/Tests/ControlRulesTests.cs` — 기본 이동·fallback 회귀
- `Assets/Scripts/Systems/Camera/Tests/PlayMode/CameraViewsPlayTests.cs` — 반환된 038 테스트에 입력→변위 회귀 추가, 기존 9개 유지
- `Assets/Scripts/Systems/Camera/CameraViewPreset.cs` — 2026-10-15 PM 범위 확대: 3인칭 비고정(FreeThirdPerson) 시점 종류 추가
- `Assets/Scripts/Systems/Camera/CameraViewDirector.cs` — 2026-10-15 PM 범위 확대: 3인칭 비고정 마우스 시선·궤도 카메라, 커서 잠금 전환 때 키보드 재동기화
- `Assets/Resources/Settings/Camera/FreeThirdPerson.asset` — 2026-10-15 PM 범위 확대: 3인칭 비고정 기본 프리셋(새 파일)
- 위 파일의 기존 .meta 유지(새 에셋은 .meta 함께). 통합 meta 소유 표에 045 단독 배정.

## 예약 구역

- 없음. 구역 확대는 PM 요청 후 진행한다.

## 수정 금지

- `Assets/Scenes/` (위 FirstPersonMovement 테스트 폴더만 예외)
- `ProjectSettings/`, `Packages/`, `Assets/_ThirdParty/`
- 035 더미 공간·032 씬 전환, PlayerMover·PlayerController·카메라 제품 코드(위 배정 파일 제외)·기존 카메라 프리셋 에셋·입력 에셋·공용 Player 프리팹은 읽기·호출만. Course 축·외줄 제약·공중 운동·부상·네트워크 권한 변경 금지.

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-131)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (.meta/GUID 확인, 재열기 root 0·dirty false, 빌드 미추가)
- [x] 스크립트·에셋 폴더를 만들었다 (전용 스크립트 폴더 .gitkeep·.meta, 추가 제품 에셋 없음)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다 (2026-10-15 PM 사용자 정식 배정 요청에 따라 기존 3개 항목 확정. 실행 결과는 별도)

## 테스트 씬 처리

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
