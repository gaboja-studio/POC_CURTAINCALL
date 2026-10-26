# Task-20261008-025

- **Title:** 공연장 레벨 디자인(에셋 배치)
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/main-stage-level-edit
- **Base:** integration/main-stage

## Context
- 공연장 맵 배치 씬(`Assets/Scenes/Levels/main_stage.unity`)을 Play하면 1인칭으로 공간 내부를 걸어 다니며, 외부 에셋(`Assets/_ThirdParty/`)으로 구성한 공간의 모습을 확인할 수 있다.
- 에셋 배치만 한다. 기능(코드) 추가는 하지 않는다.
공연장 공간의 비주얼을 외부 에셋으로 구성해, 이후 통합 씬·기능 Task가 가져다 쓸 맵 배치 씬을 만든다. 이 Task에서는 배치만 하고 기능은 추가하지 않는다.
맵 배치 씬에는 둘러보기용 `PlaySetup`(BlockDoll Player·임시 바닥)과 1인칭 카메라가 준비되어 있다(dev #43, `Harness/Project/Decisions/space-workspaces.md`).

## 현재 참고
- 단계: PM 준비, 기존 assigned 유지(기술 배정과 실제 전달은 별도)
- 담당: @RagSpirin (2026-10-15 PM 변경; 이전 준비 담당 @lakuase2)
- 동작하는 것: 기존 공연장 맵 배치 씬 준비; dev 동기화 #69 Merge commit 반영
- 막힌 것: 새 담당자 실제 전달 미확인, 맵 배치 구현·공동 테스트 미실행
- PR: 없음 (맵 배치 작업 PR); 부모 동기화 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/69

## 남은 확인
1. 기존 feat/main-stage-level-edit에 최신 통합과 담당 문서 수신 확인
2. PM 실제 전달 확인 후 맵 배치 씬 범위에서 착수
3. 구현·검사 후 integration/main-stage 제출; 인간 대포 기능은 별도 통합/Task

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
