# Setup

PM 준비 범위는 폴더와 단계 테스트 씬(맵 배치 씬 복사본)뿐이다. 기능은 작업자가 구현한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-132
- 2026-10-15 PM 지시: 1~5단계 모두 CC-132에 연결한다(단계별 카드 없음).

## 작업 구역

- 스크립트: `Assets/Scripts/WaitingRoom/ShopStorage/`
- 테스트 씬·테스트 전용 프리팹: `Assets/Scenes/Tests/WaitingRoom/ShopStorage/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음 — 2026-10-15 PM이 2~5단계를 동시에 열었다. 단계마다 전용 폴더·씬을 쓴다.

## 배정된 공용 파일

- `Assets/Scenes/Tests/WaitingRoom/ShopStorage/WaitingRoomShopStorage.unity` — 3단계 테스트 씬. waiting_room.unity(통합 82405d7 기준) 복사본, 통합 소유 표의 단독 소유 Task-20261015-048.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외. 원본 waiting_room.unity·다른 단계 테스트 씬 수정 금지.
- `Assets/Scripts/WaitingRoom/Base/` — 1단계(Task-046) 구역. 읽기/재사용만, 수정은 046 담당 작업으로.
- `Assets/Scripts/Systems/` — 공용. 읽기/기존 인터페이스 재사용만.
- `Assets/Resources/`
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 참조·배치만, 수정·추가 금지.

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 — CC-132 (2026-10-15 PM 지시).
- [x] 테스트 씬을 만들고 저장했다 — waiting_room.unity 복사본, .meta 생성.
- [x] 스크립트·에셋 폴더를 만들었다 — .meta와 빈 폴더 .gitkeep.
- [x] `plan.md`에 공동 테스트 항목을 적었다 — CC-132 단계 기준 3개.
- [x] 담당자에게 실제 전달 — PM 확인
- [x] 동시 착수 허용 — 2026-10-15 PM 결정, 앞 단계 완료·PM 보고 대기 없음

기술 배정은 `assign-task.ps1 -Id Task-20261015-048 -UsePreparedBranch`.

## 시작 조건

- assigned·배정 문서 수신 후 바로 start-work. PM 보고·승인을 기다리지 않는다(2026-10-15 PM 결정).
- 앞 단계 기능이 필요하면 통합에 병합된 것을 작업 브랜치로 받아 읽기/재사용한다. 병합 순서·충돌 정리는 PM이 정한다.

## 테스트 씬 처리

- 결정: 미정 (닫을 때 PM이 유지 / 삭제 결정).
- 결정자·날짜: 미정.
