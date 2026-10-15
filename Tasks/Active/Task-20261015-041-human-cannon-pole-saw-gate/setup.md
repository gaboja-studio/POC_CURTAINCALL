# Setup

041은 현재 Active Task이며 담당자는 @RagSpirin이다. 2026-10-15 현재 사용자 PM @MoHoDu가 040~044 인계 완료·순차 실행을 승인했다.
040 구현(#93/8293bb9)을 현재 브랜치 da4747c로 수신했고, 아래 구역/씬을 040에서 반환받아 단독 소유한다. 041 코드·씬 초안은 작성됐으나 단계별 사용자 확인 전이며, 현재 봉 조작부터 순서대로 확인한다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-127
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 2026-10-15 PM 제공 URL과 2단계 제목 확인, 실제 전달·순차 실행 승인 완료.

## 작업 구역

- 스크립트: `Assets/Scripts/MainStage/HumanCannon/`
- 테스트 씬·테스트 전용 프리팹: `Assets/Scenes/Tests/MainStage/HumanCannon/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음 — 041 구역은 현재 배정 완료. 042~044는 순번 도달 시 같은 구역을 이관받는다.

## 배정된 공용 파일

- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 041 단독 소유.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외.
- `Assets/Scripts/Systems/`
- `Assets/Resources/`
- `Assets/Scripts/MainStage/Tightrope/` — 원본 읽기/재사용만 허용, 수정 금지.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`
- main_stage.unity는 025 소유다. 공용 부상·관전·종료 코드 수정은 별도 소유 Task가 필요하다.

## 인계 전 체크 (PM)

- [x] 040 integrated 확인 — #93/8293bb9, PM 완료 확인; 4인 검증은 전체 기능 통합 후
- [x] 040 소유권 반납·041 단독 이관 — 2026-10-15 현재 PM 승인
- [x] 실제 JIRA URL 확인 — CC-127 (2단계 제목 조회)
- [x] 040 구현 코드·테스트 씬/폴더와 setup 확정 — 041에 이관
- [x] plan 공동 테스트 항목 유지 — 실행은 전체 기능 통합 후, 실제 결과 별도 기록
- [x] 다음 1개인 041에만 Active Task/Allowed 부여

## 배정 후 확인

- [x] 040 최신 구현 수신 — 041 브랜치 da4747c의 부모 8293bb9
- [ ] 이번 승인·배정 문서 원격 sync 확인 — 커밋/push 요청 시 수행
- [x] 담당자에게 실제 전달 — PM 확인

[순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)에 따라 추가 인계·착수 승인은 필요 없다. 041 통합 후 담당자가 이번 승인 범위에서 042로 단독 소유·Active Task·배정 문서를 갱신한다. 실제 최신 구현/문서 수신은 확인해 기록한다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
