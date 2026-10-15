# Setup

043은 scaffolded 기술 순번 대기다. 2026-10-15 PM @MoHoDu가 @RagSpirin에게 040~044 인계·순차 실행을 승인했다. 추가 PM 인계·착수 승인 불필요: [순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md).
040은 PR #93으로 integrated. 현재 Active Task/Allowed/테스트 씬 단독 소유는 041이다. 이 단계 구현·기능 검증·공동 테스트는 미실행이다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-129
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 2026-10-15 PM 제공 URL과 4단계 제목 확인. 인계·순차 실행 승인 완료; 구역 이관은 선행 구현 통합 후 기술 순번에 따라 수행한다.

## 작업 구역

없음 — 선행 구현 통합 후 이 단계로 Active Task/Allowed/소유권을 이관하기 전 동시 수정 금지

## 예약 구역

- 스크립트 예정: `Assets/Scripts/MainStage/HumanCannon/`
- 테스트 예정: `Assets/Scenes/Tests/MainStage/HumanCannon/` — 테스트 전용 Prefabs 포함.
- 테스트 씬: `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 현재 041 단독 소유, 이 단계는 이관 전 권한 없음.
- 예약은 현재 Allowed가 아니다. 이번 PM 승인에 따라 선행 구현 통합 후 다음 1개에만 소유권을 이관하고 문서를 갱신한다.

## 배정된 공용 파일

없음 — 선행 구현 통합 후 이 단계로 Active Task/Allowed/소유권을 이관하기 전 동시 수정 금지

## 수정 금지

- `Assets/Scenes/` — 예약 테스트 예외는 활성화·소유권 이관 후에만 허용.
- `Assets/Scripts/Systems/`
- `Assets/Resources/`
- `Assets/Scripts/MainStage/Tightrope/` — 원본 읽기/재사용만 허용, 수정 금지.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`
- main_stage.unity는 025 소유다. 공용 부상·관전·종료 코드 수정은 별도 소유 Task가 필요하다.
- 공용 종료 계약은 별도 소유 연결 Task 의존이다. 이번 Allowed로 Systems 변경 불가; 최소 생존 2명 전역 삭제·임시 우회 금지. 미연결이면 통합 종료 검증 BLOCKED.

## 승인 및 단계 전환

- [x] @RagSpirin 담당·040~044 실제 인계·순차 실행 승인 — 2026-10-15 PM @MoHoDu
- [x] 실제 JIRA URL 확인 — CC-129 (4단계 제목 조회)
- [ ] 042 구현 통합 및 최신 부모 이력 수신 확인
- [ ] 다음 1개인 043에만 Active Task/Allowed/공용 소유권 이관·setup 문서 갱신
- [ ] 해당 단계의 브랜치·문서·최신 구현 동기화 확인 후 start-work
- [ ] 공용 종료 연결 Task 식별자·소유자·계약·의존성/검증 계획 확인

순서 040→041→042→043→044. 단계 전환·소유권 이관은 이번 PM 승인 범위이며 추가 착수 승인을 기다리지 않는다.
앞 단계 실제 4인 PASS는 다음 단계 조건이 아니다. 공동 테스트는 전체 통합 후 실행하며 미실행을 PASS로 기록하지 않는다.
최신 부모 수신·브랜치 동기화는 아직 이 단계에서 확인하지 않았으며 실제 전환 때 결과를 기록한다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
