# Setup

042는 integrated다. #111/75862ef 병합·사용자 전달 PM 확인 후 043 HEAD0128623 수신 확인. 기존 [순차 인계 위임](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)에 따라 2026-10-16 구역·씬 소유를 043으로 반환했다.
이력: 041 #108/292667c를 042 dae56af가 수신한 뒤 단독 구현했고, 사용자 전체 확인·제출 요청으로 #111에 반영했다. 현재 042의 수정 권한은 없다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-128
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 2026-10-15 PM 제공 URL과 3단계 제목 확인. 인계·순차 실행 승인 완료; 구역 이관은 선행 구현 통합 후 기술 순번에 따라 수행한다.

## 작업 구역

없음 — 2026-10-16 코드·테스트 구역을 043으로 반환. 이 Task 문서의 검증 이력만 보존한다.

## 예약 구역

- 없음 — 043에 이관 완료.

## 배정된 공용 파일

없음 — HumanCannon.unity의 단독 소유를 043으로 반환했다.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외.
- `Assets/Scripts/Systems/`
- `Assets/Resources/`
- `Assets/Scripts/MainStage/Tightrope/` — 원본 읽기/재사용만 허용, 수정 금지.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`
- main_stage.unity는 025 소유다. 공용 부상·관전·종료 코드 수정은 별도 소유 Task가 필요하다.

## 승인 및 단계 전환

- [x] @RagSpirin 담당·040~044 실제 인계·순차 실행 승인 — 2026-10-15 PM @MoHoDu
- [x] 실제 JIRA URL 확인 — CC-128 (3단계 제목 조회)
- [x] 041 구현 통합·최신 부모 수신 — #108/292667c를 착수 기준042 dae56af가 포함, 당시 origin/feat 동일·부모 tracked tree 일치 확인
- [x] 042에만 Active Task/Allowed/공용 소유권 이관·setup 갱신 — 2026-10-16 기존 PM 위임
- [x] 사용자 요청으로 implement-code 착수 — 최신 지시로 042 전체 구현, 중간 확인 대기 해제
- [x] 이번 전환 문서 저장 — 2026-10-16 다른 PC 재개용 042 저장 묶음에 포함, 상위 통합 반영은 별도

순서 040→041→042→043→044. 단계 전환·소유권 이관은 이번 PM 승인 범위이며 추가 착수 승인을 기다리지 않는다.
앞 단계 실제 4인 PASS는 다음 단계 조건이 아니다. 공동 테스트는 전체 통합 후 실행하며 미실행을 PASS로 기록하지 않는다.
start-work 스크립트는 미실행이다. 이미 042 브랜치·최신 부모 수신 상태이며 기존 폰트·MPPM 미커밋 변경을 보존한다. 옛 원격 gate·dirty 제약을 PASS로 기록하지 않는다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
