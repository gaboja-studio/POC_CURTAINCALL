# Setup

PM 준비 범위는 폴더·빈 테스트 씬뿐이다. 장치·네트워크 기능은 작업자가 구현한다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-126
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124

## 작업 구역

- 스크립트: `Assets/Scripts/MainStage/HumanCannon/`
- 테스트 씬·테스트 전용 프리팹: `Assets/Scenes/Tests/MainStage/HumanCannon/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음 — 후속 Task는 별도 예약 문서이며 이 Task의 구역을 공유하지 않는다.

## 배정된 공용 파일

- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 통합 소유 표의 단독 소유 Task-20261015-040.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외. main_stage.unity는 Task-025 소유.
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

기술 배정은 assign-task -UsePreparedBranch로 확인한다. 배정 문서를 정상 Merge commit으로 수신한 뒤에도 실제 전달 todo는 PM 확인 전 미완료다.

## 시작·반환 조건

- 최신 원격 Active Task 일치·assigned·배정 문서 수신·PM 실제 전달 확인 후 start-work.
- 다음 단계 전 이 Task integrated, 실제 4인 공동 검증 근거를 남기고 PM이 Allowed/공용 소유를 반환한다.
- 반환 이력은 Integration meta에 보존한다. 후속 예약 Task에 동시 소유권을 주지 않는다.

## 테스트 씬 처리

- 결정: 미정 (닫을 때 PM이 유지 / 삭제 결정).
- 결정자·날짜: 미정.
