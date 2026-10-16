# Setup

040 구현은 PR #93/8293bb9로 통합 완료. 2026-10-15 현재 사용자 PM @MoHoDu의 완료·전체 인계·순차 실행 확인으로 구역/씬을 041에 반환했다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-126
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124

## 작업 구역

- 없음 — 041에 반환 완료. 040 문서/검증 기록만 보존한다.

## 예약 구역

- 없음 — 040 구현 단계 종료.

## 배정된 공용 파일

- 없음 — HumanCannon.unity는 041 단독 소유.

## 반환 이력

- 2026-10-15: `Assets/Scripts/MainStage/HumanCannon/`, `Assets/Scenes/Tests/MainStage/HumanCannon/`, `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`를 040에서 041로 이관.
- 근거: [PM 순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md), #93 통합·041 da4747c 수신 확인. 실제 4인 검증은 전체 기능 통합 후 수행.

## 수정 금지

- `Assets/Scenes/` — 반환 후 040 예외 없음. HumanCannon.unity는 041, main_stage.unity는 025 소유.
- `Assets/Scripts/Systems/` — 공용 부상·관전·공연 종료는 읽기/기존 인터페이스 재사용만.
- `Assets/Scripts/MainStage/Tightrope/` — 외줄 원본 수정 금지.
- `Assets/Resources/` — 최종 공연용 프리팹 배치는 별도 소유 배정 후.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 참조만, 수정·추가 금지.

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 — 1단계 CC-126, 상위 기준 CC-124.
- [x] Unity에서 테스트 씬을 만들고 저장했다 — .meta 생성, rootCount=0, 기존 활성 씬 유지.
- [x] 스크립트·에셋 폴더를 만들었다 — .meta와 빈 폴더 .gitkeep 준비.
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다 — PM 승인 계획과 CC-124의 3묶음.

위 체크는 040 초기 준비 이력이다. 현재는 040 완료·반환 및 5개 Task 인계·순차 실행 승인까지 PM 확인 완료다.

## 시작·반환 조건

- 040은 integrated이며 같은 작업 브랜치에서 구현을 계속하지 않는다. 다음 Active Task는 041이다.
- 4인 검증을 다음 단계 선행조건으로 요구하지 않는다. 미실행 기록·최종 공동검증 계획을 유지한다.
- 반환 이력은 Integration meta에도 보존한다. 후속은 기존 PM 승인으로 순차 전환하며 동시에 소유하지 않는다.

## 테스트 씬 처리

- 결정: 미정 (닫을 때 PM이 유지 / 삭제 결정).
- 결정자·날짜: 미정.
