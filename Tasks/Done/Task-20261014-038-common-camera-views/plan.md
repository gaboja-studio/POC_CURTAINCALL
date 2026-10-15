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

## 승인된 구현 세부 (2026-10-15 PM)

- 규칙은 구역 진입·이탈 + 코드 요청/해제 API. 높은 숫자 우선순위, 동률 최근 등록. 동일 source 값 갱신은 등록 순서를 바꾸지 않고 해제 후 재등록은 새 요청이다.
- 즉시 전환 기본, 에셋 전환 시간이 양수면 현재 출력 pose·화각에서 보간. 외줄 즉시 적용·기존 Follow/Composer damping은 유지한다.
- 기본 3인칭: 월드 offset `(0, 3.75, -4.5)`, 캐릭터 로컬 look offset `(0, 1.2, 1.75)`, FOV 60. 외줄 요청 priority 0, 테스트 구역 10. 코스가 없으면 외줄 요청은 해제한다.
- 1인칭: 눈높이 0은 BodyHeight × 0.9(없으면 1.6m), 감도 0.1, pitch ±85°, FOV 60·near 0.1. 진입 yaw는 현재 화면 수평 방향, pitch 0. 수동 전환 키·3인칭 orbit/zoom은 추가하지 않는다.
- 단일 리그의 director가 구도 값을 적용하고 Cinemachine 최종 extension이 1인칭 pose를 적용한다. 관전은 기존 대상 선택을 유지하고 1인칭·모델 숨김을 중지한다.
- 1인칭 커서는 포커스 시 잠금, Esc 해제·게임 영역 클릭 재잠금(테스트 UI 제외), 종료 때 원복. 037 입력 차단 API 연결은 후속 통합 항목이다.
- 로컬 CurrentModel renderer만 게임 카메라 render 범위에서 forceRenderingOff 저장/복구. Renderer.enabled·부상 상태는 변경하지 않고 교체·탈착·래그돌·disable을 정리한다.
- BoxCollider 구역 쪽에 kinematic Rigidbody 배치. 로컬 소유자만 감지하고 다중 collider·teleport·Exit 누락·비활성화·despawn을 정리한다.
- 가림의 코스 루트·같은 목마 예외와 외줄 수치는 이전한 외줄 코드가 공급한다. 높이 0.8/1.35, 반경 0.4, 알파 0.35, 속도 6·관전 점진 복구 유지.
- 순수 요청 resolver만 작은 noEngineReferences asmdef로 분리해 EditMode 테스트. 나머지는 기존 Assembly-CSharp 유지. 합성 PlayMode probe는 실제 입력 검증으로 보고하지 않는다.
- 테스트 씬: Main Camera에 Brain, 기존 카메라 리그·세션 UI/NetworkManager 참조·출발점·바닥·1인칭 구역/경계 표식 배치. 기존 LAN 접속 재사용, 원본 Player/세션/UI 수정 없음. 2026-10-15 사용자 요청: CameraViewsTestBootstrap으로 이 씬의 로컬 균형만 비활성화(접속·재시작 회귀 검사), 실제 외줄은 유지.
- 에디터 미연결이면 Unity 에셋 생성·이동·저장 금지. 10-15 직접 명령 ready·대상 경로·dirty 0 확인. beta.6 status 검색 불일치는 원인 미확정이며 지원되는 명령으로 진행한다.

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
- 3 테스트: 요청 판정 EditMode 11개(`Camera/Tests/EditMode/`) + 실제 씬·LAN 호스트 PlayMode 9개(`Camera/Tests/PlayMode/`, 리타이어 관전·CameraViews 균형 회귀 포함, 이동·요청은 합성)
- 4 실행 로그: `proto_0.0.2.unity`·이 Task 테스트 씬에서 Missing 0, Console 오류 0

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 외줄타기(`proto_0.0.2.unity`)의 화면 구도·사망 후 관전·가림 흐리기가 전과 똑같다.
2. 테스트 씬에서 시점 구역에 들어가면 1인칭(내 모델 숨김·마우스 둘러보기), 나오면 3인칭으로 돌아온다. 다른 플레이어 화면에서는 내 모델이 그대로 보인다.
3. 기획자가 시점 에셋 값(거리·높이·화각)을 인스펙터에서 바꾸면 플레이 중 바로 반영된다.
- 추가 회귀(위 1·2항목): merge로 받은 리타이어 관전 결정도 확인: 2인에서 A Retired→B 관전, 모두 Retired→대상 없음, 재시작→각자 자기 카메라·조작·구역 전환 복귀. 2026-10-15 사용자 전부 PASS(상태는 공개 API 주입, 실제 잡힘 경로 검증 아님).
