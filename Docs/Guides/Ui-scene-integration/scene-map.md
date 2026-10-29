# 실제 씬과 UI 대응

2026-10-20 사용자가 지정한5개 씬만 적용한다. 백스테이지·탈출로 씬은 현재 없으므로 생성하지 않는다.
참고: 사용자 제공 커튼콜 게임 UI 화면 목록.html(3판, 2026-10-19). 문서의 제안 구성·과거 구현 상태와 현재 Assets 정본/실제 코드를 구분한다. HTML 예시 색/글꼴로 정본 그래픽을 덮어쓰지 않는다.

| 씬 | 대응 화면 | 현재 단계 |
|---|---|---|
| Assets/Scenes/Start_Lobby/Start.unity | T1/T2/T3 타이틀·방 생성/참가 | 후속, 정본 확인 먼저 |
| Assets/Scenes/Start_Lobby/Lobby.unity | L1 방 정보·준비·로비 입력 | 후속, 정본 확인 먼저 |
| Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity | W1/C4/C5/W6·상호작용·구매·스케줄 | 후속, 정본 확인 먼저 |
| Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity | C1/C2/C3/P1 | 적용, 1인 Play 확인; 다인 검증 미실행 |
| Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity | C1/C2/C3, P1 항상 숨김 | 적용, 1인 Play 확인; 다인 검증 미실행 |

## 데이터 차이
- 사용자 확정: C2는 불이 안전 지대에 도달할 때까지의 표시 시간이다. 외줄의 TimeRemaining(공연 제한시간)은 사용하지 않는다. 기존 성공/실패 로직은 그대로 유지한다.
- 인간대포는 RunSeconds(경과 시간)와 구간별 FireWall.EndTime만 존재한다. 10분 목표를 제한시간으로 바꾸거나 마지막 구간 불을 전체 공연 타이머로 추정하지 않는다.
- HTML에서 미구현으로 적힌 C1은 현재 정본과 보상 binding이 있다. 최신 프리팹과 API를 우선한다.
- 독립 UI 검증 씬10개는 제작·회귀 참고용이며 실제 게임 씬을 대체하지 않는다.
