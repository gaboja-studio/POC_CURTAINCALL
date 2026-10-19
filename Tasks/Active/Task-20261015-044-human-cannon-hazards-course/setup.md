# Setup

044는 working/Active Task, 담당자 @RagSpirin. [PM 순차 위임](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)과 2026-10-16 사용자 착수 요청에 따라 043 구역·씬을 반환받았다.
043 PR #121/70e242a 통합·044 HEAD9a0d078 수신, 부모 이력 포함·tracked tree 동일·로컬/원격 일치 확인. 043의 공용 실패 규칙 예외 권한은 이관하지 않는다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-130
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 실제 URL·5단계 제목·인계 확인 완료.

## 작업 구역

- `Assets/Scripts/MainStage/HumanCannon/`
- `Assets/Scenes/Tests/MainStage/HumanCannon/` — 테스트 전용 Prefabs 포함.

## 예약 구역

없음 — 044 배정 완료.

## 배정된 공용 파일

- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 044 단독 소유.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외.
- `Assets/Scripts/Systems/`
- `Assets/Resources/`
- `Assets/Scripts/MainStage/Tightrope/` — 원본 읽기/공개 API 재사용만.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`
- main_stage.unity는 025 소유. 공용 부상·관전·종료 수정 권한 없음.

## 승인 및 단계 전환

- [x] @RagSpirin 담당·040~044 인계·순차 실행 승인 — 2026-10-15 PM @MoHoDu
- [x] 실제 JIRA CC-130 URL·제목 확인
- [x] 043 #121/70e242a 통합 및 044 HEAD9a0d078 최신 부모 수신 확인
- [x] 이전 Allowed/씬 반환 → 044 단독 Active Task/Allowed/씬 소유 이관 — 2026-10-16 기존 PM 위임
- [x] 현재 브랜치·로컬/원격·부모 tree 확인 후 implement-code 착수

옛 원격 scaffolded gate를 사용하는 start-work 스크립트 대신 승인된 전환을 문서에 기록한다. 실제4인 공동 검증은 전체 통합 후 실행하며 미실행을 PASS로 적지 않는다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
