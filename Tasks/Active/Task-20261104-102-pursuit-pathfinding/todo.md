# Todo

## 할 일
- [x] 원인 재현: 추격 전환 시 pathPending 제자리 달리기(26체 중 대부분)
- [x] 동기 경로 계산(`PursuitNavigation`)·목표 근방 조정·부분 경로 최근접 도달 지점
- [x] 순회지 근방 조정(최대 5m)·막힌 순회지 건너뛰기·전부 막히면 대기 후 재탐색
- [x] 정지 감지(1초 재배치·3초 포기), 막힌 추격은 제자리 달리기 대신 대기
- [x] 최인접 추격 대상 경로 거리 선택, 순찰 복귀 지점 경로 거리 선택
- [x] 문 장애물 매 프레임 재깎기 제거, agent.autoRepath 끔
- [x] 수정 후 플레이 재측정(정지·pathPending)
- [x] NavMesh 재베이크 필요 여부 판단(불필요)
- [x] 함정 기록: Harness/Project/Pitfalls/navmesh-agent-path-pending.md
- [x] EditMode 테스트·check-work 1회, 결과 기록
- [ ] 사람 플레이 확인(실제 플레이어 추격·다인 원격 화면)
