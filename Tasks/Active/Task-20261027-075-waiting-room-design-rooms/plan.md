# Task-20261027-075

- **Title:** 대기실 방 구조·배치 및 디자인
- **Type:** feat
- **Assignee:** @gyumin9048-ctrl
- **Branch:** feat/waiting-room/design-rooms
- **Base:** dev

## Context
@gyumin9048-ctrl(표시 이름 mingyu)이 기존 `feat/waiting-room/design-rooms` 브랜치에서 대기실 방 구조·배치 및 공간 디자인을 진행한다. 이번 요청은 작업 세팅이며 실제 씬 편집은 담당자가 이어서 진행한다. 방별 치수·구성·에셋·분위기는 현황 조사 후 구체화한다.

- 작업 씬: `Assets/Scenes/Levels/waiting_room.unity`.
- 범위: 방 구조와 연결 동선, 구조물·가구·소품 배치, 스케일·조명·분위기 조정, 배치에 필요한 기존 기능 연결 유지·보완.
- 관련 지도: [에셋 구조](../../../Docs/Domains/assets-structure.md). 과거 조사 경로는 현재 씬·프리팹과 대조한다.
- 선행 작업: [Task-060 대기실 아트](../Task-20261019-060-waiting-room-art/plan.md), [Task-065 대기실 물리 동선](../Task-20261020-065-waiting-room-physical-flow/plan.md). 과거 실패·충돌 기록은 현재 상태와 대조하며 기존 Task 기록을 덮어쓰지 않는다.

## 계획
1. [start-work](../../../Harness/Core/Skills/start-work.md)로 현재 브랜치에서 이어 간다. 씬 편집 전 [integrate-editor](../../../Harness/Engine/Unity/Skills/integrate-editor.md), [코드 위치](../../../Harness/Engine/Unity/Policies/code-folders.md), [에셋 협업](../../../Harness/Engine/Unity/Policies/asset-ownership.md)을 확인한다.
2. 현재 씬의 방·출입구·동선·기능 오브젝트와 수동 배치를 조사하고 방별 구조·배치 계획과 확인 기준을 정한다. 기존 생성 도구는 현재 배치를 덮어쓰는지 확인한 후 사용한다.
3. 구조물·가구·소품을 배치하고 방 크기·이동·카메라 시야·조명·분위기를 조정한다. 상점·보관·서비스 등 기존 상호작용 연결을 확인한다.
4. 수정 묶음 완료 시 `pwsh -File Scripts/check-work.ps1`을 한 번 실행하고 결과·환경 제약·다음 할 일을 log에 기록한다. 공간의 재미·방향·최종 플레이 경험은 사람이 확인한다.

## 미정·주의
- 세부 방 구성·치수·시각 방향은 아직 지정되지 않았다. 담당자가 조사하며 계획을 갱신한다. 미정 게임 의도는 human-decision을 따른다.
- 다른 작업과 수정이 겹치면 알리고 씬·프리팹 YAML 충돌은 담당자와 해결한다. 기존 `.meta`/GUID와 참조를 보존한다.
- 에셋 활용·배치·새 머티리얼 생성은 자유롭다. 외부 에셋은 비공개 `Assets/_ThirdParty/`에 두고 기존 에셋의 큰 수정·삭제는 PM·에셋 관리자와 조율한다.
- 별도 구역 배정·PM 인계 승인·Jira 등록은 착수 조건이 아니다. dev 반영은 PR 1회와 지정 팀원 검토를 거친다.
- 이번 요청은 작업 세팅과 현 브랜치의 origin 저장까지이며 실제 대기실 디자인 변경은 후속 작업이다.
