---
summary: 통합 WaitingRoom 씬, Base/Pursuit 선택과 추격 배치 편집 대상을 확인할 때
status: active
updated: 2026-11-05
source: Task-20261104-100, 사용자 통합 요청 및 Unity 씬 비교; Task-20261105-107 dev 동기화 비교
---

# 통합 대기실

- 기본 대기실은 `Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity`다. `Tools/Dev Mode/대기실 씬 설정`에서 Base / Pursuit / Integrated를 선택한다. 실제 플레이·Dev 시작·탐색 경로가 같은 선택을 쓴다. 변경 후 런을 다시 시작한다.
- Base/Pursuit enum 값 0/1은 유지하고 Integrated=2를 추가했다. GUID 경로를 사용하므로 에셋 이동 시 `.meta`를 보존한다. 세 씬 모두 빌드 목록에 등록한다.
- Pursuit 원본의 26개 방·26개 개별 배치·순찰 경로·22개 문 장애물·내비게이션·행동 그래프·포획 효과를 복제했다. Base의 PracticeMonitor 및 자식 3개 Transform은 최신 값으로 가져왔다. 리허설 코스·단장 모니터·봉쇄는 기존 `WaitingRoomRehearsal` 런타임 생성 경로를 쓴다.
- 리허설은 Rehearsal 단계, 추격은 RestTime/Pursuit 타이머에서 작동한다. 통합 씬의 카메라·UI·음성·로그 분류는 `WaitingRoomScenes`에서 인식한다.
- `추격자 배치`는 현재 활성 씬의 Director를 편집하고 그 씬에 저장한다. 통합 씬을 열어 편집하면 통합 씬 배치에 적용된다. Base/Pursuit/Integrated 사이의 배치를 자동으로 덮어쓰지 않는다. Director가 없는 씬에서 도구의 열기 버튼은 설정한 추격 지원 씬(Base 설정이면 Integrated)을 연다.
- 통합 씬을 원본으로부터 다시 복제하면 이후 개별 배치 편집을 덮어쓸 수 있으므로 자동 재생성하지 않는다. 후속 변경은 통합 씬에 직접 적용한다.
- 실제 다인 접속/Relay 검증 및 성능 비교는 Task-099의 별도 미완료 항목이다.
- 2026-11-05 dev 기준 `WaitingRoomBase.unity`는 통합 WaitingRoom(9b71be5d 이전)의 바이트 동일 복사본이다(Task-097이 리허설 작업 씬으로 복사). 리허설 기획 반영은 스크립트·`RehearsalCourseFromPerformance.prefab`이라 씬 사이를 옮길 내용이 없다. Base에 있다고 믿는 기능은 먼저 git show로 두 씬을 fileID 단위 비교해 확인한다.
- 알려진 EditMode 실패: `UnifiedWaitingRoomSceneTests`가 WaitingRoomPursuit과 WaitingRoom의 PursuitPlacement size(index 6, 0/1) 불일치로 FAIL. dev 861ea6f7이 두 씬을 다르게 저장한 결과이며 담당 @MoHoDu 확인 전까지 배치 편집 브랜치에서 고치지 않는다.
