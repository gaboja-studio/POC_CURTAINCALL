# Plan

## Context

백스테이지 공간의 비주얼을 외부 에셋으로 구성해, 이후 통합 씬·기능 Task가 가져다 쓸 맵 배치 씬을 만든다. 이 Task에서는 배치만 하고 기능은 추가하지 않는다.
맵 배치 씬에는 둘러보기용 `PlaySetup`(BlockDoll Player·임시 바닥)과 1인칭 카메라가 준비되어 있다(dev #43, `Harness/Project/Decisions/space-workspaces.md`).

## Approach

1. `Assets/_ThirdParty/`의 에셋(현재 Synty 아포칼립스 로우폴리·Generic)을 맵 배치 씬에 가져다 배치한다.
2. 바닥·벽 콜라이더를 확인하며 Play(1인칭)로 수시로 둘러본다.
3. 필요한 외부 에셋은 PM에게 요청한다. 코드·다른 씬·공용 에셋은 고치지 않는다.
4. 검사 후 `integration/back-stage`로 제출.

## Files

- 수정 예정: `Assets/Scenes/Levels/backstage.unity`
- 읽기 전용 참고: `Assets/_ThirdParty/`, `Assets/Scripts/Systems/LevelPreview/`

## Risks

- 콜라이더 없는 에셋을 바닥으로 쓰면 Play 때 빠진다.
- 에셋을 많이 놓으면 씬 파일이 커진다. 씬 YAML 충돌은 AI가 해결하지 않는다(이 씬은 이 Task만 수정).

## Auto Verification

- 구역 검사: `verify-scope` (맵 배치 씬만 변경)
- 1 문서/규칙: `verify-fast`
- 2 컴파일: `verify-unity -Compile` (코드 변경 없음)
- 3 테스트: NO_PROJECT_TESTS (배치 작업)
- 4 실행 로그: 맵 배치 씬 Play 시 Console 오류 0건

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.

1. 맵 배치 씬을 Play하면 1인칭으로 공간 내부 전체를 걸어 다닐 수 있다(바닥으로 빠지거나 벽에 끼지 않음).
2. 공간 구성이 공간 기획(Confluence 공간 분리 문서)과 맞는다(PM 확인).
3. 변경 파일이 맵 배치 씬 하나뿐이다(구역 검사 PASS).
