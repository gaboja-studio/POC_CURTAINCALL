# Task-20261020-064

- **Title:** 인간대포 종료 후 대기실 부활 관전 잔류 수정
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-room-revival-guard
- **Base:** dev

## Context
- 첫째날 인간대포 사망자가 밤 정비에서 부활한 뒤 이전 씬의 피해 판정으로 다시 사망하지 않고 대기실에서 조작·시점을 복구한다.
사용자 제보: 첫째날 인간대포 사망 후 밤 정비 부활 로그와 달리 대기실에서도 관전이 지속된다. Editor.log 6673·6689·6715행에서 밤 정비 → 사망자 1명 부활 → 대기실 전환을 확인했다. 실제 재사망 상태 전이는 기존 로그에 없다.

## 현재 참고
- Task-064, fix/waiting-room-revival-guard. PM 직접 로컬 수정, 커밋·push·PR 없음.
- 확인한 결함: 밤 정비 부활은 공연 씬 해제보다 빠르며, 기존 가드는 출발점 확인 이후 공연 종료 여부를 검사하지 않아 옛 씬 피해가 부활자를 다시 죽일 수 있었다.
- 최소 수정: CampaignPerformanceEntry의 기존 Fallen 가드에 IsCurrentPerformance 결합. 카메라·NetworkPlayer RPC·부활 위치·게임 규칙은 변경하지 않음.
- 기존 Vivox Settings.json 변경 보존. Task-062 문서·미완료 공동 검증 기록 보존. 필요한 세 구현/테스트 파일만 현재 Task에 재배정.
- Task 발급용 로컬 integration/waiting-room-revival 및 최초 fix/waiting-room-revival 브랜치는 생성만 되었고 원격·커밋 없음. 실제 작업 브랜치는 meta 참조.

## 남은 확인
1. 실제 호스트/참가자 공동 플레이 확인(plan.md의 3항목). 자동 검증은 완료.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
