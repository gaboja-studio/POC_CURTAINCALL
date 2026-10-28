# Task-20261027-074

- **Title:** 외줄타기 공연 고도화
- **Type:** feat
- **Assignee:** @lakuase2
- **Branch:** feat/tight-rope/enhancement
- **Base:** dev

## Context
@lakuase2가 현 브랜치에서 외줄타기 공연을 고도화한다. 이번 요청은 작업 세팅이며 실제 기능·씬 수정은 담당자가 이어서 진행한다. 세부 개선 항목과 수용 기준은 현황 조사 후 이 plan과 todo에 구체화한다.

- 관련 지도: [외줄 묘기](../../../Docs/Domains/tightrope-course.md). 과거 조사 날짜의 구현·계획 표기는 현재 코드와 대조한다.
- 조사 진입점: `Assets/Scripts/MainStage/Tightrope/`, `Assets/Resources/Settings/GameSettings/Tricks/TightropeSettings.asset`. 실제 씬·프리팹과 공용 시스템 연결은 담당자가 확인한다.

## 계획
1. [start-work](../../../Harness/Core/Skills/start-work.md)를 따라 현재 브랜치에서 이어 간다.
2. 외줄 공연의 코스·조작·장애물·진행·결과와 관련 씬을 조사해 개선 항목과 확인 방법을 정한다. 미정 게임 의도는 [human-decision](../../../Harness/Core/Policies/human-decision.md)을 따른다.
3. Unity 변경 전 [코드 위치](../../../Harness/Engine/Unity/Policies/code-folders.md)와 [에셋 협업](../../../Harness/Engine/Unity/Policies/asset-ownership.md)을 확인한다. 씬·프리팹 편집은 integrate-editor, 코드 구현은 implement-code를 따른다.
4. 수정 묶음 완료 시 `pwsh -File Scripts/check-work.ps1`을 한 번 실행하고 결과·환경 제약·다음 할 일을 log에 기록한다. 재미·방향·최종 플레이 경험은 사람이 확인한다.

## 미정·주의
- 구체적인 기능 추가·밸런스·연출 방향은 아직 지정되지 않았다. 이번 세팅에서 확정하지 않는다.
- 다른 작업과 수정이 겹치면 알리고, 씬·프리팹 YAML 충돌은 담당자와 해결한다. 기존 에셋의 큰 수정·삭제는 PM·에셋 관리자와 조율한다.
- 별도 구역 배정·PM 인계 승인·Jira 등록은 착수 조건이 아니다.
- dev 반영은 PR 1회와 지정 팀원 검토를 거친다. commit/push는 사용자 요청 시에만 한다.
