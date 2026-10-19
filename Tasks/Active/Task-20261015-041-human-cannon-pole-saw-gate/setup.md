# Setup

041은 integrated이며 담당자는 @RagSpirin이다. 2026-10-16 PR #108 MERGED(292667c)와 042 착수 기준 dae56af 수신을 확인했다.
기존 PM 순차 실행 승인에 따라 041 Allowed·공용 씬 소유를 반환하고 042에 단독 이관했다. 봉 조작·Space·톱 변화는 사용자 확인 완료, 관문·사망 인계 실제 4인 검증은 전체 통합 후에 남긴다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-127
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 2026-10-15 PM 제공 URL과 2단계 제목 확인, 실제 전달·순차 실행 승인 완료.

## 작업 구역

없음 — 2026-10-16 코드·테스트 구역을 042에 반환했다. 이 Task의 검증 기록은 보존한다.

## 예약 구역

- 없음 — HumanCannon 스크립트·테스트 구역은 현재 042 단독 소유다.

## 배정된 공용 파일

없음 — HumanCannon.unity는 042에 단독 이관했다.

## 수정 금지

- `Assets/Scenes/` — 기존 HumanCannon 테스트 구역도 042 이관 후 수정 금지.
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
- [x] 당시 다음 1개인 041에만 Active Task/Allowed 부여 — 2026-10-16 반환

## 배정 후 확인

- [x] 040 최신 구현 수신 — 041 브랜치 da4747c의 부모 8293bb9
- [x] 041 승인·배정 문서 수신 — 사용자 포함 승인으로 제출한 #108/292667c를 042 dae56af가 수신
- [x] 담당자에게 실제 전달 — PM 확인
- [x] 041 integrated·Allowed 반환·042 단독 소유 이관 — 2026-10-16 기존 PM 위임
- [x] 이번 041→042 전환 문서 저장 — 2026-10-16 다른 PC 재개용042 저장 묶음에 포함, 상위 통합 반영은 별도

[순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)에 따라 추가 인계·착수 승인 없이 042로 단독 소유·Active Task·배정 문서를 갱신했다. 실제4인 미검증과 전환 문서의 상위 통합 반영 대기는041 구현 통합 사실과 구분한다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
