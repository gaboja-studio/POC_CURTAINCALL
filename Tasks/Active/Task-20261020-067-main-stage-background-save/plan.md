# Plan

## Context

거대한 원형 폐공장 서커스를 공용 배경으로 제공한다. 묘기 기물과 게임 로직은 별도 씬에서 소유한다.

## Approach

1. 기존 제작 결과와 원본 에셋 변경을 분리하고 .meta와 참조를 검증한다.
2. 공연장 에셋을 private 저장소에 저장하고 원격 main에 반영한다.
3. 공개 작업 브랜치에 private gitlink와 Task 인계를 저장한다.

## Files

- 결과: Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity
- Task 등록과 integration/main-stage의 Task 행, private gitlink.

## Risks

- 활성 렌더러 약 1250개, 정점 약 525만. 대상 PC 성능 미측정.
- 기존 원본 머티리얼 변경 219개는 공연장 의존성에 포함되지 않으며 현 PC에 보존한다.

## Auto Verification

- save-work: scope, verify-fast, Unity 컴파일 및 저장 상태 확인.
- 에셋 .meta 누락 0, 씬 재귀 의존성 604개, 기존 변경 머티리얼 참조 0.

## 공동 테스트 항목

아래는 후속 시각/성능 검토 항목이며 이번 저장에 대한 공동 실행 완료를 의미하지 않는다.

1. 공연장 단독 씬에서 상하 시점의 규모감·조명·관중석을 검토한다.
2. 각 묘기와 Additive 로드하여 중앙 플레이 영역과 카메라 간섭을 확인한다.
3. 대상 PC에서 플레이 중 프레임 시간을 측정한다.
