# Handoff

## 지금 상태
- 단계: working (2026-10-16 시작).
- 동작하는 것:
  - 테스트 씬 `WaitingRoomGreybox`(0,0,70): 2차 기획서 조감도 비율(약 0.06m/px)의 ①~⑧ 그레이박스·대기실 문·⑧ 닫힌 문·⑥ 대체 통로. 캡슐 검사로 출입구 통과·⑧ 차단 확인.
    - 메뉴 `Tools/CurtainCall/대기실/그레이박스 다시 만들기`(`Base/Editor/WaitingRoomGreyboxBuilder.cs`)로 재생성. 기획서 주요 오브젝트만 단순 도형+표지, 장식 없음. ③ 상점은 로비 안 기둥(수화기·배송구), 옆 벽 없음.
    - 표지 글자는 벽 뒤가 비치지 않게 테스트 셰이더 `Materials/GreyboxText.shader` 사용.
  - 참고 프로토타입: `WaitingRoom`(waiting_room.unity 복사), `대기실`(Levels/SampleScene에서 복사, 원본 미수정). 에셋 적용은 기능 뒤 후순위.
  - `WaitingRoomSpawn`: 씬 진입·복귀 시 내 캐릭터를 ① 로비 스폰(`PlayerSpawnPoints` 4개)으로.
  - `StageEntranceGather`: 판정 화면이 쉬는 시간에 살아 있는 전원이 ⑥ 영역 안이면 `CampaignRun.NotifyAllAtStageEntrance()` 1회. 오프라인 런으로 바깥 0/1 전환 없음 → 진입 1/1 공연 전환 확인.
- 막힌 것: 4인 공연 왕복은 장소 전환이 이 씬을 대기실로 열어야 가능(아래 공용 파일 요청).
- PR: 없음
## 다음 할 일
1. PM: 아래 공용 파일 요청 처리.
2. 작업자: 처리 뒤 SpaceLink 시작 씬으로 4인 왕복·외팔 부활·반복 확인, 검사·PR.
## PM이 확인할 것
- 확인 요청: 원본 waiting_room.unity 반영 시점, 최종 테스트 씬 처리.
- 확인 요청: 조감도에 맞춰 ⑦ 공연 스테이지 입구를 테스트 씬에 표시물로 넣음(setup은 "⑦ 만들지 않음"). 백스테이지 포함 SampleScene 대기실도 참고용으로 복사함.
- 대기 중인 결정: 없음 (2~5단계 미정 항목은 각 Task meta).
- 공용 파일 요청:
  - `ProjectSettings/EditorBuildSettings.asset`에 `WaitingRoomBase.unity` 등록(NGO 씬 전환은 빌드 목록 씬만 연다).
  - `Assets/Resources/Settings/GameSettings` 장소 전환 `waitingRoomScene`을 테스트 동안 `WaitingRoomBase`로(또는 테스트용 교체 방법 제공). 지금은 `DummyWaitingRoom`.
  - 연결 시 테스트 씬의 맵 확인용 `PlaySetup/Player`(씬 배치)는 접속 플레이어와 겹치므로 작업자가 끈다.
## Verification
- 구역 검사: OK (verify-scope, 2026-10-16)
- 1 문서/규칙: 미실행
- 2 컴파일: 에디터 재컴파일 성공 (check-work 미실행)
- 3 테스트: NO_PROJECT_TESTS (이 Task 자동 테스트 없음)
- 4 실행 로그: 오프라인 런 PlayMode 수동 확인 — ⑥ 밖 0/1 전환 없음, 진입 1/1 → `[WaitingRoom] ⑥ 전원 집결 1/1 → 공연 전환`, 오류 없음. 네트워크·장소 전환 미확인.
- 5 공동 테스트: 미요청
## 공동 테스트 결과
- 일시: 미실행
- 참여자: 미실행 (예정: 호스트1·참가자3)
- 재현: 미실행 (plan 3개)
- 예상: plan 참조
- 실제: 미실행
