# Task-20261021-068

- **Title:** 모든 묘기 씬 공용 공연장 배경 자동 로드 연결
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/performance-background-load
- **Base:** dev

## Context
- #196의 배경을 dev로 가져오고 모든 실제 묘기 진입 시 additive 배경 하나를 자동 표시한다.
- 기존 코스·판정·카메라를 보존하고 묘기 종료 시 배경을 정리한다.
#196은 integration/main-stage에만 반영되어 있다. dev 반영 PR #197 및 역방향 동기화 PR #198에 gitlink·통합 문서 충돌이 있어, dev 기반 전용 fix PR로 포인터 가져오기와 자동 로드를 함께 준비한다. dev나 다른 통합 브랜치에 직접 push하지 않는다.

## 현재 참고
- fix/performance-background-load: dev 기반 같은 프로젝트 폴더에서 구현. 별도 worktree/에디터 없음.
- #196 private b6cc29c7 포인터와 배경 빌드 등록, 실제 묘기 자동 additive 로드·종료 해제 완료.
- 대상: HumanCannon/Tightrope/proto_0.0.2. 원본 1/3, root Z54.4583/55.8333, 무대 Y0. 기존 코스·씬 저장본 수정 없음.
- 원본 조명 범위/세기 배율과 안개·ambient·카메라 배경색을 런타임 적용, 이탈 시 복원. 수동으로 연 배경은 소유·변형·해제하지 않음.
- NGO 공개 검증 콜백으로 배경을 네트워크 동기화에서 제외. 게임의 동기화 모드·공용 전환 코드는 유지.

## 남은 확인
1. 승인된 제출·CI·병합 및 대체 PR 정리.
2. 실제 두 화면/늦은 참가/프레임 시간은 plan.md 3개 공동 확인 항목으로 검증.

## 기록
- 현재 할 일은 [todo.md](todo.md), 특별한 문제·결과·다음 행동은 [log.md](log.md).
- [이전 계획](History/Harness-v1/plan.md) · [현재 상태·미검증 원문](History/Harness-v1/handoff.md) · [목표·결정 원문](History/Harness-v1/meta.md).
- [기존 경로·참조 정보](History/Harness-v1/setup.md)는 참고용이다. 작업 구역·인계·Status·순차 공동 테스트 제한은 적용하지 않는다.
