# Setup

043은 예약 단계다. 현재 Active Task는 040뿐이며 기술 배정 전이다. 실제 전달은 2026-10-15 PM 사용자 확인 완료이며 구현 개방은 아니다.
준비 사실은 폴더·빈 씬(root 0)만이다. 인간 대포 기능·공동 테스트는 미실행이다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-129
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 2026-10-15 PM 제공 URL과 4단계 제목 확인. 실제 전달은 PM 사용자 확인 완료; 구역 이관은 별도 대기.

## 작업 구역

없음 — 선행 단계 검증과 PM 소유권 이관 전 수정 금지

## 예약 구역

- 스크립트 예정: `Assets/Scripts/MainStage/HumanCannon/`
- 테스트 예정: `Assets/Scenes/Tests/MainStage/HumanCannon/` — 테스트 전용 Prefabs 포함.
- 테스트 씬: `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 현재 040 단독 소유, 후속 단계 권한 없음.
- 예약은 Allowed가 아니다. 선행 단계 소유권 반납 후 PM이 다음 1개에만 이관한다.

## 배정된 공용 파일

없음 — 선행 단계 검증과 PM 소유권 이관 전 수정 금지

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

## 인계 전 체크 (PM)

- [ ] 042 integrated와 실제 4인 공동 테스트 근거 확인
- [ ] 선행 단계 소유권 반납·이관 확인
- [x] 실제 JIRA URL 확인 — CC-129 (4단계 제목 조회)
- [ ] 테스트 씬·폴더 준비와 setup 확정 — 현재 빈 씬은 040 소유
- [ ] plan 공동 테스트 항목 합의·개별 검사 확인
- [ ] 공용 종료 연결 Task 식별자·소유자·계약·의존성/검증 계획 확인
- [ ] 다음 1개인 043에만 Active Task/Allowed 부여

## 배정 후 확인

- [ ] 승인된 준비 브랜치에 배정 문서 Merge commit sync 확인
- [x] 담당자에게 실제 전달 — PM 확인

기존 feat 브랜치도 앞 단계 완료 후 최신 부모 이력을 받아야 한다. 승인된 준비 브랜치 배정·문서 sync·실제 전달 확인 이후만 start-work하며 scaffolded 착수는 금지한다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
