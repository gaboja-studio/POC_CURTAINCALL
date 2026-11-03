---
summary: MPPM 4개 Editor를 실제 UTP 지연으로 비교할 때 최신 Simulator 파라미터와 정상 이동 조건을 검증한다
status: active
updated: 2026-11-04
source: Task-20261103-099 4인 LAN A/B 및 불꽃 접촉 검증
---

# MPPM 실제 지연 측정

## 언제 읽나

4인 위치 동기화·패킷 유실·지연을 같은 PC에서 재현하거나, 시뮬레이터를 켰는데 RTT가 바뀌지 않을 때.

- 현재 UTP 패키지에서 오래된 UnityTransport.SetDebugSimulatorParameters는 실제 적용 경로가 아니다. GetNetworkDriver().ModifyNetworkSimulatorParameters(NetworkSimulatorParameter)의 SendDelayMS/SendJitterMS/SendPacketLossPercent를 사용하고 수신측 RTT로 적용 여부를 확인한다.
- 이 프로젝트의 Unity 6000.3.25f1에서는 공식 Multiplayer Play Mode 창의 Player 2/3/4 체크박스로 4개 독립 Editor 실행에 성공했다. Scenario 창 실행 실패를 모든 MPPM 기능 불가로 일반화하지 않는다.
- 각 복제 프로젝트 경로를 명시한다. Pipeline 서버 포트는 도메인 리로드 때 바뀐다. 다른 작업 트리 Editor를 대신 조작하지 않는다.
- Play를 먼저 정상 종료하고 가상 플레이어 체크를 해제한다. 프로세스를 먼저 강제 종료하지 않는다.
- 성능 측정 전 4인 접속·정상 신체·접지·이동을 확인한다. 이전 실패 후 추락하는 플레이어 데이터는 위치 지연 비교에서 제외한다. 외줄은 시작선 통과 후 IsPerformanceStarted가 켜진다. Running만 보고 접촉 이력이 이미 쌓였다고 가정하지 않는다.
- 같은 PC의 공통 시각으로 소유자 위치와 복제 위치를 비교할 수 있다. 4대 PC는 먼저 시계 정렬이 필요하다. 4개 Editor의 프레임 시간을 출시 빌드/Relay 결과로 부르지 않는다.

## 검증

Task099의 FourPlayerNetworkProbe와 four-player-lan-report-20261104.md에 100ms/30ms/3%, A→B→B→A 원본과 분석 절차를 보관했다.
