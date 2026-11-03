> 이전 제작본 기록입니다. 현재 공연 리소스·입력 검증과 최신 캡처는 [재사용 수정 검증](reuse-verification.md)을 확인하세요.

# 리허설 기능·실제 이동 캡처

확인한 핵심 기능: 추락 복귀, 도착에 따른 단계 전환, 연속 그네 잡기, 트리거 연결/해제, 완료 후 길안내·이동·집결 전환. 새 철창 3곳은 8개 이동 시도에서 통과가 재현되지 않았고 나머지 한 지점은 기존 벽이 막았다.

발견·수정: 실제 집결 지점 z=-10.15가 기존 NavMesh 범위 밖이어서 길안내가 없었다. 출발점·목적지와 여유 구간을 포함하도록 수정한 뒤 출구 이동 스모크 PASS.

검사 범위: 단독 호스트 기능 확인이다. 각 리허설 동작의 시작 위치와 일부 단계는 테스트에서 배치하고 게임의 잡기/놓기·도착 판정을 확인했다. 2인 협동을 포함해 전 코스를 사람이 조작한 검사는 아니다. 완료 후 아래 이동 경유점은 순간이동 없이 보통 이동 입력으로 걸었다.

동선: 연습실 링 → 연습실 출구 → 로비 경유 → 무대 통로 입구 → 통로 끝 집결 → 첫 공연 단계 전환.

[문구·패널 편집 안내](</Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/quest-editing.md>). 원본은 `Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt`이며 `rehearsal.stage.*`, `rehearsal.quest.*`, `first-day.quest.*`, `first-day.panel.*`를 편집한다. 플레이 재시작 시 반영된다.

1단계: 외줄 기본 퀘스트

![1단계: 외줄 기본 퀘스트](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/01-stage1-start.png)

추락 상태와 안내

![추락 상태와 안내](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/02-fall.png)

출발대 복귀와 기본 퀘스트 복원

![출발대 복귀와 기본 퀘스트 복원](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/03-recovered.png)

도착 판정 뒤 2-1 코스·퀘스트

![도착 판정 뒤 2-1 코스·퀘스트](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/04-stage2-1.png)

2-2 연속 그네

![2-2 연속 그네](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/05-stage2-2-start.png)

반동·놓기 후 두 번째 그네 잡기

![반동·놓기 후 두 번째 그네 잡기](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/06-stage2-2-second-grab.png)

2-3 트리거 매달림·짝 줄 연결

![2-3 트리거 매달림·짝 줄 연결](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/07-stage2-3-trigger-held.png)

트리거 해제 뒤 줄 차단

![트리거 해제 뒤 줄 차단](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/08-stage2-3-released.png)

3단계 트리거·짝 줄 연결

![3단계 트리거·짝 줄 연결](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/09-stage3-trigger-held.png)

완료 퀘스트와 수정 후 바닥 길안내

![완료 퀘스트와 수정 후 바닥 길안내](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/10-complete-route.png)

실제 이동 경유점 01

![실제 이동 경유점 01](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-01.png)

실제 이동 경유점 02

![실제 이동 경유점 02](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-02.png)

실제 이동 경유점 03

![실제 이동 경유점 03](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-03.png)

실제 이동 경유점 04

![실제 이동 경유점 04](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-04.png)

실제 이동 경유점 05

![실제 이동 경유점 05](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-05.png)

실제 이동 경유점 06

![실제 이동 경유점 06](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-06.png)

실제 이동 경유점 07

![실제 이동 경유점 07](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-07.png)

실제 이동 경유점 08

![실제 이동 경유점 08](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/route-08.png)

집결 후 공연 단계 진입: 공간 전환 커튼(공연 씬 로딩 완료 캡처는 아님)

![집결 후 공연 단계 진입: 공간 전환 커튼(공연 씬 로딩 완료 캡처는 아님)](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/11-first-performance.png)

닫힌 철창 이동 검사: Dormitory

![닫힌 철창 이동 검사: Dormitory](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/gate-lockdown-dormitory.png)

닫힌 철창 이동 검사: PropRoom

![닫힌 철창 이동 검사: PropRoom](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/gate-lockdown-prop-room.png)

닫힌 철창 이동 검사: ShopHall

![닫힌 철창 이동 검사: ShopHall](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/gate-lockdown-shop-hall.png)

앞선 연결 검사: 일정 확정 후 취침 퀘스트

![앞선 연결 검사: 일정 확정 후 취침 퀘스트](/Users/seomingyu/Desktop/0. 부트캠프/02. 커튼 콜/POC_CURTAINCALL/Tasks/Active/Task-20261102-097-rehearsal-flow/Previews/Rehearsal/first-night-quest.png)
