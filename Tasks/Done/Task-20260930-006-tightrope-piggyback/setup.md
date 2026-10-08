# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-47

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/Piggyback/` (Piggyback.unity, 조작 캐릭터·더미는 Prefabs 하위)
- 스크립트 폴더: `Assets/Scripts/Tightrope/Piggyback/`
- 카메라 스크립트 폴더: `Assets/Scripts/Camera/` (2026-10-04 PM @MoHoDu: 카메라 가림 처리를 이 Task에서, `Integrations/Active/Integration-tightrope-prototype/camera-review-20261004.md`)
- 에셋 폴더: 테스트 씬 폴더에 포함
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Settings/TightropeSettings.cs` — 목마 묶음(`PiggybackSettings`)에 006 칸 추가만 (2026-10-04 PM @MoHoDu)
- `Assets/Resources/GameSettings/Tricks/TightropeSettings.asset` — 위 칸 값 저장 (2026-10-04 PM @MoHoDu)
- `Assets/Scripts/Tightrope/Rope/CourseControlSwitcher.cs` — 목마 위층(이동 계산 꺼짐) 동안 조작·균형 전환을 건너뛰는 줄만 (2026-10-04 PM @MoHoDu)
- `Assets/Scripts/Player/PlayerMover.cs` — 땅에 없어도 점프를 시작하는 공개 함수 추가(목마 맨 위 앞·뒤 분리 점프, 2026-10-04 PM @MoHoDu). 2026-10-05 PM 승인: 기존 허용·착지 규칙을 재사용하는 머리 위 옆줄 점프 진입점 추가.
- `Assets/Resources/Prefabs/Controllers/Camera/` — `PlayerFollowCamera.prefab`에 가림 처리·구도 컴포넌트 추가, 투명 템플릿 머티리얼 (2026-10-04 PM @MoHoDu)
- 위 TightropeSettings 두 파일에 카메라 묶음 추가 (2026-10-04 PM @MoHoDu, BaseGameSettings는 012가 수정 중이라 피함)

- `Assets/Scripts/Tightrope/Rider/RopeLaneJumpRules.cs` — 옆줄 착지 자동 목마 합체 우선 연결·손잡기 착지 알림 (2026-10-04 PM @MoHoDu 승인)
- `Assets/Plugins/`·`Assets/Plugins.meta`·`Assets/Resources/DOTweenSettings.asset`·`Assets/Resources/DOTweenSettings.asset.meta` — PM이 임포트한 DOTween Pro 포함. 원본·예제는 수정하지 않음 (2026-10-04)

중간 저장 한정 예외 (2026-10-05 PM @MoHoDu 승인):

- `Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset` — 현재 폰트 아틀라스·글리프 변경을 보존해 저장하는 것만 허용.
- `Assets/Resources/GameSettings/BaseGameSettings.asset` — 테스트 임시값(movingSway=0, idleSway=0, tiltAcceleration=0) 보존·저장만 허용. 초기화는 Task 모든 작업 종료 및 사용자 전체 통과 후.
- `ProjectSettings/ProjectSettings.asset` — DOTween 임포트에 따른 플랫폼별 DOTWEEN 심볼 추가를 저장하는 것만 허용. 그 외 ProjectSettings 수정 금지 유지.

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

- 결정: 유지 (공간별 재배치로 Assets/Scenes/Tests/MainStage/Tightrope/·Tests/Systems/에 보존)
- 결정자·날짜: PM @MoHoDu, 2026-10-08
