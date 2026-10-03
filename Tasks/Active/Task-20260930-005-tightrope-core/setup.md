# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-41

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/Tightrope/` (Tightrope.unity, 테스트 전용 프리팹은 Prefabs 하위. 플레이어는 실제 프리팹 사용)
- 스크립트 폴더: `Assets/Scripts/Tightrope/Rope/` (디버그 조작 코드 포함)
- 에셋 폴더: 아래 공용 파일
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` — 외줄 코스 스테이지 프리팹 (다른 Task는 배치만)
- `Assets/Scripts/Network/PlayerSync/TestRoundRestart.cs` — 007 테스트 부품. 묘기 진행으로 대체한 뒤 삭제만 (2026-10-02 PM 배정)
- `Assets/Scripts/Player/PlayerMover.cs` — `LaneSpacing` 설정(#14), 플랫폼 일반 이동(8방향·이동 방향 바라보기), 출발 위치만 바꾸기(순간이동 없이), 추락 플레이어 통과(#16)·플레이어끼리 막기(플랫폼)·래그돌 밀기, 플랫폼 이동 속도 `freeMoveSpeed` (2026-10-02~03 PM @MoHoDu 승인)
- `Assets/Scripts/Network/PlayerSync/PlayerState.cs` — 상태 `Arrived`(도착 완료) 끝에 추가만 (2026-10-03 PM @MoHoDu, 기획 상세 규칙)
- `Assets/Resources/Prefabs/Characters/Players/Player.prefab` — 이동 0.5m/s·점프 0.8m·캡슐 높이 1.5m/지름 0.6m 값만 (2026-10-03 PM @MoHoDu, 기획 상세 규칙)
- `Assets/Scripts/Player/PlayerRagdoll.cs` — 래그돌이 다른 플레이어 몸통과 부딪혀 밀리게 (#16, 2026-10-02 PM @MoHoDu 요청)
- `Assets/Scripts/Player/PlayerControlScheme.cs`, `Assets/Scripts/Player/PlayerCondition.cs`(새 파일), `Assets/Scripts/Player/BodyPart.cs`(새 파일) — 신체 부위 손실(데미지) 준비만, 디메리트는 콘텐츠별 조작 규칙이 처리 (2026-10-02 PM @MoHoDu 요청)
- `Assets/Scripts/Network/Session/NetworkSessionManager.cs`, `Assets/Scripts/Network/Session/NetworkSessionTestUI.cs` — 최소 인원·호스트 게임 시작(`StartGame`)·시작 버튼, 자동 시작 제거 (2026-10-02 PM @MoHoDu 승인)
- `Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab` — 추적 방식을 월드 고정 방향으로(캐릭터 회전에 카메라가 따라 돌지 않게) (2026-10-02 PM @MoHoDu 승인)

## 수정 금지

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`
- `Assets/Settings/`
- `Assets/InputSystem_Actions.inputactions`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
