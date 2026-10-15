# Plan

## Context

카메라 코드가 공용 폴더(`Assets/Scripts/Systems/Camera/`)에 있지만 3인칭은 외줄 구도에 묶여 있고, 1인칭은 맵 배치 확인용(`Systems/LevelPreview/`, 게임 카메라 아님)뿐이다(Confluence 19398663 6장, 2026-10-14 확인).

- `TightropeCameraFraming`: 외줄 세팅(카메라) 값을 매 프레임 넣는 외줄 전용 구도인데 공용 폴더에 있다.
- `CameraOcclusionFade`: 가림 흐리기는 공용으로 쓸 만하지만 `TightropeCourse.Current`·외줄 세팅(반경)을 직접 읽는다.
- `LocalPlayerCamera`(내 캐릭터 따라가기)·`CameraSpectator`(사망 후 관전)는 공용으로 유지.
- 대기실·백 스테이지 탐색, 공연장 묘기 등 공간·상황마다 1인칭/3인칭을 바꿀 수 있어야 한다(PM 2026-10-14).

## Approach

1. **시점 정의**: 1인칭·3인칭을 공용 "시점" 에셋으로 만든다(거리·높이·화각·내 모델 숨김 여부 등 기획자가 인스펙터에서 조정). 1인칭은 `LevelPreviewCamera`의 눈높이·마우스 둘러보기·내 모델 숨김을 참고해 게임용(온라인, 내 캐릭터 기준)으로 만든다. 맵 배치용 `LevelPreview`는 그대로 둔다.
2. **시점 전환기**: 카메라 리그에 공용 시점 전환기를 둔다. 기본 시점 + "요청" 목록(누가 어떤 시점을 우선순위 몇으로 원하는지)으로 결정하고, 요청이 빠지면 이전 시점으로 돌아간다. 관전(`CameraSpectator`)은 시점과 별개로 대상만 바꾼다.
3. **공간이 공용 수정 없이 시점을 바꾸는 방법**: (a) 씬에 놓는 공용 "시점 구역" 컴포넌트(들어가면 지정 시점, 나오면 해제), (b) 코드에서 부르는 공용 요청 함수 1개. 공간은 자기 폴더의 규칙(예: 추격당할 때 3인칭)이 이 함수만 부른다.
4. **외줄 분리**: `TightropeCameraFraming`을 공연장 폴더로 `.meta`째 옮기고, 외줄은 자기 3인칭 시점(외줄 구도)을 기본 시점으로 요청한다. `CameraOcclusionFade`는 "흐리지 않을 대상"을 콘텐츠가 등록하게 바꾸고 반경 등 수치는 공용 카메라 설정으로 옮기되 외줄에서의 값은 같게 둔다.
5. 지도 `Docs/Domains/common-game-systems.md`·`tightrope-course.md`·`player-sync.md` 갱신. Confluence 19398663 갱신은 PM.

## Files

- 수정 예정: `Assets/Scripts/Systems/Camera/`, `Assets/Scripts/Systems/Network/PlayerSync/LocalPlayerCamera.cs`, `Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab`, 새 `Assets/Resources/Settings/Camera/`, 공연장 `MainStage/Tightrope/Camera/`(새로 받는 곳), 지도 3개
- 읽기 전용 참고: `Systems/LevelPreview/`, `Systems/Settings/TightropeSettings.cs`(외줄 카메라 값), `proto_0.0.2.unity`

## Risks

- 리그 프리팹(PlayerFollowCamera)에 붙은 컴포넌트를 `.meta` 없이 옮기면 Missing이 된다.
- 1인칭에서는 이동 방향이 "바라보는 방향 기준"이어야 한다. 조작 규칙은 037 소유라 이 Task는 고치지 않고, 시점 전환기가 "지금 바라보는 수평 방향"을 공개해 규칙이 읽을 수 있게만 한다(037과 연결 약속, 1인칭 정식 조작 규칙은 PM 결정).
- 1인칭에서 내 모델 숨김이 부상 표시(잘린 팔다리 `ModelLimbs`)와 겹치지 않게 한다(내 화면만 숨김, 다른 사람 화면은 그대로).
- 외줄 세팅 파일(`TightropeSettings`)은 이 Task가 고치지 않는다. 옮길 수치가 있으면 새 카메라 설정에 같은 값으로 만들고 외줄 칸 정리는 PM 요청.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 시점 요청 우선순위·해제 시 복귀 EditMode 테스트, 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: `proto_0.0.2.unity`·이 Task 테스트 씬에서 Missing 0, Console 오류 0

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 외줄타기(`proto_0.0.2.unity`)의 화면 구도·사망 후 관전·가림 흐리기가 전과 똑같다.
2. 테스트 씬에서 시점 구역에 들어가면 1인칭(내 모델 숨김·마우스 둘러보기), 나오면 3인칭으로 돌아온다. 다른 플레이어 화면에서는 내 모델이 그대로 보인다.
3. 기획자가 시점 에셋 값(거리·높이·화각)을 인스펙터에서 바꾸면 플레이 중 바로 반영된다.
