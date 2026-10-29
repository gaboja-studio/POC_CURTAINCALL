# Task-20261027-073

- **Title:** 탈출로 공간 디자인
- **Type:** feat
- **Assignee:** @RagSpirin
- **Branch:** feat/exit-route/design-map
- **Base:** dev

## Context
@RagSpirin이 기존 에셋을 활용해 탈출로 맵을 배치하고 공간을 디자인한다. 주 작업은 씬 안의 구조물·소품 배치, 동선·스케일·분위기 조정이다. 이번 요청은 작업 세팅이며 실제 맵 편집은 담당자가 이어서 진행한다.

- 작업 씬: `Assets/Scenes/Levels/escape_route.unity` (기존 `PlaySetup`·`TempFloor` 확인).
- 공간 의도: 백스테이지에서 이어지는 구 공연장·하역장 복합 코스. 상세 구성·치수·에셋 선택은 작업하며 정한다.
- 에셋: `Assets/_ThirdParty/`의 기존 에셋을 직접 참조·배치하고 원본·`.meta`/GUID를 보존한다.
- 작업 기준: [하네스 간소화](../../../Harness/Project/Decisions/harness-simplification.md), [에셋 지도](../../../Docs/Domains/assets-structure.md).

## 계획
1. 이 plan과 todo를 읽고 [start-work](../../../Harness/Core/Skills/start-work.md)로 현재 브랜치에서 이어 간다. 씬 편집 전 [integrate-editor](../../../Harness/Engine/Unity/Skills/integrate-editor.md), [코드 위치](../../../Harness/Engine/Unity/Policies/code-folders.md), [에셋 협업](../../../Harness/Engine/Unity/Policies/asset-ownership.md)을 확인한다.
2. 기존 씬과 사용할 에셋을 살펴보고 구 공연장 → 하역장 동선과 구역을 잡는다. 세부 계획은 담당자가 갱신한다.
3. 에셋을 배치하고 충돌·바닥·카메라 시야·이동 가능 여부를 둘러보며 조정한다. 실제 바닥을 만들면 임시 `TempFloor`는 제거할 수 있다.
4. 수정 묶음 완료 시 `pwsh -File Scripts/check-work.ps1`을 한 번 실행하고 결과·미검증 사항을 log에 기록한다. 공간의 재미·방향·최종 플레이 경험은 사람이 확인한다.

## 미정·주의
- 별도 setup/handoff 문서·구역 배정·PM 인계 승인·Jira 등록·중간 통합 브랜치 경유는 착수 조건이 아니다. 목적에 필요한 연결은 직접 진행한다.
- 새 에셋 활용·배치·새 머티리얼 생성은 자유롭다. 외부 에셋 원문은 비공개 `_ThirdParty`에 두며 기존 에셋의 큰 수정·삭제는 PM·에셋 관리자와 조율한다.
- 다른 작업과 씬·프리팹 수정이 겹치면 알릴 내용을 log에 남긴다. YAML 충돌은 담당자와 해결한다.
- 탈출 판정·추격·런 상태 등 게임 시스템 개발은 현재 주 작업이 아니다. 공간 배치에 필요한 연결과 미정 게임 의도는 구분해 기록한다.
- dev 반영은 PR 1회와 지정 팀원 검토를 거친다. commit/push는 사용자가 요청할 때만 한다.
