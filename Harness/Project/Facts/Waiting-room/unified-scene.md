---
summary: 통합 WaitingRoom 씬, Base/Pursuit 선택과 추격 배치 편집 대상을 확인할 때
status: active
updated: 2026-11-04
source: Task-20261104-100, 사용자 통합 요청 및 Unity 씬 비교
---

# 통합 대기실

- 기본 대기실은 `Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity`다. `Tools/Dev Mode/대기실 씬 설정`에서 Base / Pursuit / Integrated를 선택한다. 실제 플레이·Dev 시작·탐색 경로가 같은 선택을 쓴다. 변경 후 런을 다시 시작한다.
- Base/Pursuit enum 값 0/1은 유지하고 Integrated=2를 추가했다. GUID 경로를 사용하므로 에셋 이동 시 `.meta`를 보존한다. 세 씬 모두 빌드 목록에 등록한다.
- Pursuit 원본의 26개 방·26개 개별 배치·순찰 경로·22개 문 장애물·내비게이션·행동 그래프·포획 효과를 복제했다. Base의 PracticeMonitor 및 자식 3개 Transform은 최신 값으로 가져왔다. 리허설 코스·단장 모니터·봉쇄는 기존 `WaitingRoomRehearsal` 런타임 생성 경로를 쓴다.
- 리허설은 Rehearsal 단계, 추격은 RestTime/Pursuit 타이머에서 작동한다. 통합 씬의 카메라·UI·음성·로그 분류는 `WaitingRoomScenes`에서 인식한다.
- `추격자 배치`는 현재 활성 씬의 Director를 편집하고 그 씬에 저장한다. 통합 씬을 열어 편집하면 통합 씬 배치에 적용된다. Base/Pursuit/Integrated 사이의 배치를 자동으로 덮어쓰지 않는다. Director가 없는 씬에서 도구의 열기 버튼은 설정한 추격 지원 씬(Base 설정이면 Integrated)을 연다.
- 통합 씬을 원본으로부터 다시 복제하면 이후 개별 배치 편집을 덮어쓸 수 있으므로 자동 재생성하지 않는다. 후속 변경은 통합 씬에 직접 적용한다.
- 실제 다인 접속/Relay 검증 및 성능 비교는 Task-099의 별도 미완료 항목이다.
