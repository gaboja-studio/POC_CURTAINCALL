# Log

## 2026-11-04
- feat/rehearsal-flow(c2f689bb+문서) 위에서 feat/cannon-rehearsal 분기. Unity 에디터가 열려 있어 origin/dev(c04997b2, PR 287 병합 포함) 반영은 에디터 종료 후로 미룸. 로컬 전용 변경(폰트 2·escape-labels·BaseGameSettings·ThirdParty) 보존.
- **사용자 지시로 구현 보류(2026-11-04).** 코드·씬·프리팹 변경 없음. 아래는 재개 시 쓸 조사 결과.
- 사용자 정정: 착지 지점은 연습실 링 바닥이다. 링 아래 지하 맵을 파지 않는다. 시작대·1단계 도착 장소·2단계 도착 장소는 링 위에 높인 발판, 트램은 1단계 도착 발판에서 링 바닥으로 내려간다.
- 차용 가능: HumanCannonSystem/Trampoline/RouteSystem/Transport/LandingZone은 NetworkBehaviour 없는 이름 붙은 메시지 구조로 탈출로 씬에서도 재사용 중. 씬당 하나(Current) 제약. Prepared 프리팹: 대포 발사 전방 7.2·상방 12m/s, 원형 트램펄린 반동 12m/s·requireCarrierForBounce=false, 운송 발판 6×8m·overloadTrap=true(공연 출발 운송 0번과 같음. RouteSystem 1번 칸은 쉼터 대기 조건이 있어 0번 칸 사용).
- 범위 관리: HumanCannonSystem을 켜면 점프 높이 덮어쓰기·부상 정책이 등록되므로 리허설 중에만 켠다. 직렬화 필드 setter가 없어 런타임 생성 대신 에디터 빌더로 리허설 코스 프리팹을 만드는 방식이 공연 코드 무수정에 맞다.
- 무부상 즉사: 공연은 비착지 착지 시 2곳 절단(HumanCannonSystem.Landing), 억제는 EscapeDevelopmentSession.FallInjuriesSuppressed뿐. 공연 코드 무수정안: 리허설 중 링 바닥에 착지 구역을 깔아 부상을 막고, LandingConfirmed 위치로 리허설이 Fallen(래그돌=사망 모션) → 1.5초 뒤 ServerRestoreAll → 체크포인트 이동(외줄 리허설 추락 복구와 같은 방식).
- 시간 정지: 연습실(Area_S09)은 탐색 타이머 정지 구역이고 밤 정비 시간은 무제한이라 새 코드 불필요.
- 진입: ScheduleBoard.Set에서 인간대포·미이수면 확인 패널(RehearsalVotePanel 재사용). 스케줄표 창은 순간이동하면 자동으로 닫힌다. 재진입은 연습실 PracticeLanes/HumanCannon(해금 시 보이는 기물)에 RehearsalPracticeStation 같은 상호작용을 붙인다.
- 충돌 주의: 밤에는 PracticeGate(-17.2,0,86.5)를 포함한 모든 NightDoorLock 철창이 잠긴다. 종료 후 "연습실 입구"는 철창 바깥(로비 쪽)에 두어야 갇히지 않는다. 연습실 대포 재진입은 밤에는 연습실이 잠겨 불가(쉬는 시간만 가능).
- 링 실측: 링 바닥 DeckCollider 중심(-48.5, 0.6 윗면, 112) 40×40m, 천장 충돌체 없음, 연습실 둥근 벽 높이 12m.
- origin/dev 미반영(Unity 에디터 열림).
