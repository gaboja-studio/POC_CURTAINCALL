# Setup

이 Task는 예약 단계다. 현재 Active Task는 Task-20261015-046뿐이다. 실제 전달은 2026-10-15 PM 확인 완료, 기술 배정 전이다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.

## JIRA

- 관련 JIRA: 미발급 — 앞 단계 검증·PM 통합 뒤 이 단계 카드를 만들어 실제 URL로 교체(배정 필수 조건).
- 상위 개발 순서: https://hrjoo122770.atlassian.net/browse/CC-132

## 작업 구역

없음 — 선행 단계 검증과 PM 소유권 이관 전 수정 금지

## 예약 구역

- 스크립트 예정: `Assets/Scripts/WaitingRoom/Base/`
- 테스트 예정: `Assets/Scenes/Tests/WaitingRoom/Base/` — 테스트 전용 Prefabs 포함.
- 테스트 씬 WaitingRoomBase.unity·맵 배치 씬 waiting_room.unity는 현재 이 단계 권한 없음.
- 예약은 Allowed가 아니다. 선행 단계 소유권 반납 후 PM이 다음 1개에만 이관한다.

## 배정된 공용 파일

없음 — 선행 단계 검증과 PM 소유권 이관 전 수정 금지

## 수정 금지

- `Assets/Scenes/`
- `Assets/Scripts/Systems/`
- `Assets/Resources/`
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`

## 인계 전 체크 (PM)

- [ ] Task-20261015-046 integrated와 실제 4인 공동 테스트 근거 확인
- [ ] 선행 단계 소유권 반납·이관 확인
- [ ] 이 단계 실제 JIRA URL 연결
- [ ] 작업 구역·공용 파일 확정, plan 공동 테스트 항목 합의
- [ ] Active Task를 이 Task로 옮기고 배정 문서를 준비 브랜치로 Merge commit sync
- [x] 담당자에게 실제 전달 — PM 확인

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
