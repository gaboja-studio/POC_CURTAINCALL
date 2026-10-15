# Setup

PM 준비 범위는 폴더·빈 테스트 씬뿐이다. 공간 배치·전환·동기화 기능은 작업자가 구현한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-132
- 연관: CC-111(공간 연결), CC-119(더미 공간), CC-109(부상·생존) — 읽기/재사용 대상.

## 작업 구역

- 스크립트: `Assets/Scripts/WaitingRoom/Base/`
- 테스트 씬·테스트 전용 프리팹: `Assets/Scenes/Tests/WaitingRoom/Base/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 이관 대기

Allowed가 아니다. 아래 조건이 모두 충족되면 PM이 소유 표와 이 setup의 `배정된 공용 파일`로 옮긴다.

- 맵 배치 씬 Assets/Scenes/Levels/waiting_room.unity — 현재 부모 integration/waiting-room에서 Task-024 소유.
  - 조건: Task-024 PR 제출·통합(작업 구역 밖 변경 정리 포함) → 부모 소유 표 반환 → 이 통합이 최신 부모를 Merge commit으로 수신.

## 예약 구역

- 없음 — 후속 Task-047~050은 별도 예약 문서이며 이 Task의 구역을 공유하지 않는다.

## 배정된 공용 파일

- `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity` — 통합 소유 표의 단독 소유 Task-20261015-046.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외. waiting_room.unity는 이관 전까지 Task-024 소유.
- `Assets/Scripts/Systems/` — 네트워크·런 상태·더미 공간·부상은 읽기/기존 인터페이스 재사용만.
- `Assets/Resources/`
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 참조·배치만, 수정·추가 금지.

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 — CC-132.
- [x] 테스트 씬을 만들고 저장했다 — 빈 씬(root 0)·.meta 생성.
- [x] 스크립트·에셋 폴더를 만들었다 — .meta와 빈 폴더 .gitkeep.
- [x] `plan.md`에 공동 테스트 항목을 적었다 — CC-132 공동 플레이 확인 3개.
- [ ] ⑥·⑦ 위치(대기실/백스테이지) PM 확인
- [ ] waiting_room.unity 이관 조건 충족

기술 배정은 `assign-task.ps1 -Id Task-20261015-046 -UsePreparedBranch`. 실제 전달 todo는 PM이 별도 확인한다.

## 시작·반환 조건

- 최신 원격 Active Task 일치·assigned·배정 문서 수신·PM 실제 전달 확인 후 start-work.
- 다음 단계 전 이 Task integrated, 실제 4인 공동 검증 근거를 남기고 PM이 Allowed/공용 소유를 반환한다.

## 테스트 씬 처리

- 결정: 미정 (닫을 때 PM이 유지 / 삭제 결정).
- 결정자·날짜: 미정.
