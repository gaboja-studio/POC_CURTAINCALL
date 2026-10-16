# Handoff

## 지금 상태
- 단계: working. 1단계 기능 구현 완료, 호스트 1인 왕복 확인. 4인 공동 테스트 전.
- 동작하는 것:
  - `WaitingRoomGreybox`(0,0,70): 조감도 비율 ①~⑧ 그레이박스. 메뉴 `Tools/CurtainCall/대기실/그레이박스 다시 만들기`로 재생성. ③ 상점 = 로비 기둥(수화기·배송구). 표지는 `Materials/GreyboxText.shader`.
  - `WaitingRoomSpawn`: 씬 진입·복귀 시 ① 로비 스폰(자리 번호) + 줄 균형 끔(켜 두면 서 있어도 추락).
  - `StageEntranceGather`: 쉬는 시간 살아 있는 전원이 ⑥ 영역 안 → `NotifyAllAtStageEntrance()` 1회.
  - 네트워크 준비: 맵 확인용 `PlaySetup` 끔, Main Camera에 CinemachineBrain, `PlayerFollowCamera` 배치(LevelPreviewCamera 끔).
  - PM 구두 허용(2026-10-16, meta 기록): 빌드 목록에 WaitingRoomBase 등록, 장소 전환 `waitingRoomScene` = WaitingRoomBase. 로컬 변경만, 미커밋.
- 막힌 것: 저장 불가 — verify-scope가 위 2파일을 막음(`BaseGameSettings.asset` 수정 금지, `EditorBuildSettings.asset`은 Task-051 구역과 겹침). setup.md는 PM 전용.
- PR: 없음
## 다음 할 일
1. 4인 공동 테스트 3개(plan): SpaceLink 씬 → 방 만들기·참가 → 런 시작 → 공연 종료(더미 화면) → 대기실.
2. "검사해줘" → "제출해줘".
## PM이 확인할 것
- 확인 요청: 원본 waiting_room.unity 반영 시점, 최종 테스트 씬 처리, 참고 프로토타입 2개(`WaitingRoom`·`대기실`) 처리.
- 확인 요청: 조감도에 맞춰 ⑦을 표시물로 넣음(setup은 "⑦ 만들지 않음").
- 확인 요청: `waitingRoomScene` = WaitingRoomBase는 다른 SpaceLink 테스트도 이 씬으로 보낸다. 통합 반영 시 유지/복원 결정.
- 공용 파일 요청: setup.md "배정된 공용 파일"에 `ProjectSettings/EditorBuildSettings.asset`(WaitingRoomBase 등록만)·`Assets/Resources/Settings/GameSettings/BaseGameSettings.asset`(`waitingRoomScene`만) 추가, Task-051 구역과 겹침 정리.
## Verification
- 구역 검사: OK (2026-10-16)
- 2 컴파일: 에디터 재컴파일 성공
- 3 테스트: NO_PROJECT_TESTS
- 4 실행 로그: LAN 호스트 1인(개발자 모드 임시) — 공연1 사망 → 쉬는 시간 대기실 복귀: 스폰1·외팔(잃은 3)·Normal·균형 꺼짐, ⑥ 0/1 → 진입 1/1 → 공연2 전환 → 밤 정비 복귀 스폰1·0/1 재설정. 새 오류 없음.
- 5 공동 테스트: 미실행
## 공동 테스트 결과
- 미실행 (예정: 호스트1·참가자3, plan 3개)
