---
summary: 씬 전환 중 "[TransportShutdown] NetworkConnectionManager was shutdown"으로 튕기면 메인 스레드 10초 정지로 인한 Relay 할당 만료를 의심한다.
status: active
updated: 2026-11-02
source: human, Task-20261102-093
---

# 씬 전환 중 Relay 할당 만료로 튕김

## 언제 읽나

클라이언트 화면에 `[Disconnect Event][Client-N][TransportClientId-<0이 아닌 값>][TransportShutdown] NetworkConnectionManager was shutdown. The transport was shutdown.`가 뜨고 시작 화면으로 돌아갈 때. 대기실 → 탈출로 전환에서 자주 보고됐다(2026-11-02).

## 원인

- 이 문구는 서버가 끊은 것이 아니다. 해당 클라이언트의 NGO가 스스로 `Shutdown`했다는 뜻이다. TransportClientId가 0이 아니면 그 전에 전송 계층의 끊김 이벤트가 없었다는 뜻이다.
- 우리 코드의 나가기(`NetworkSessionManager.LeaveAsync`)였다면 "방에서 나갔습니다."가 뜬다. 연결 취소 경로는 `HandleConnectionEvent`가 무시한다.
- 남는 경로는 UTP의 `RelayConnectionStatus.AllocationInvalid`다. `UnityTransport`가 이를 `TransportFailure`로 올리고, NGO `TransportFailureEventHandler`가 `Shutdown(true)`한다. 그다음 `DisconnectLocalClient`의 로컬 끊김 이벤트가 위 문구를 `DisconnectReason`에 남긴다.
- Relay 서버는 약 10초 동안 요청이 없으면 그 클라이언트 할당을 만료시킨다(ErrTimeout=1). 전송 계층 문서에는 `ScheduleUpdate`가 충분히 자주 호출되지 않을 때도 이렇게 된다고 적혀 있다. 즉 메인 스레드가 한 번에 10초 가까이 멈추면 복구할 수 없게 끊긴다. 재연결에는 새 할당과 NGO 재시작이 필요하다.
- Multiplayer Services 세션 SDK도 로비 멤버에서 빠지면 `INetworkHandler.StopAsync`로 같은 문구를 만들 수 있다. 다만 Relay 끊김에 따른 로비 제거는 기본 120초 뒤라 씬 전환 직후의 튕김과는 맞지 않는다.

## 측정

- 2026-11-02 이 PC 에디터 배치(렌더링 없음)에서 측정했다. 조건은 대기실(WaitingRoomBase) 위에 escape_route를 출발 상태로 추가 로드하고, 활성화한 뒤 대기실을 언로드하는 것이다.
  - escape 로드 3.1초 중 최대 프레임 0.4초, 활성화와 단계 공개 1.0초, 대기실 언로드 0.2초. 10초에 가까운 단일 프레임은 없었다.
  - 메모리는 대기실만 있을 때 1.7GB, 함께 있을 때 2.2GB.
- 실제 두 PC Relay 입장에서는 3~5초 프레임이 있었고(Telemetry 함정), 4인 MPPM 첫 동기화·64MB 글꼴 텍스처 업로드에서는 9~13초 정지가 있었다([session-join-cancelled](session-join-cancelled.md)). 렌더링, GPU 업로드, 메모리가 부족한 PC가 원인 후보로 남아 있다.

## 대처

- `NetworkSessionManager`가 `OnTransportFailure`를 받아 종료 문구를 `TransportFailureMessage`로 바꾸고 다음 로그를 남긴다.
  - `[Session] 전송 계층 실패로 접속 종료 [TransportFailure] mode=… longestStallMs=… scene=…`
- 접속 중 3초 이상 멈춘 프레임은 `[Session] 메인 스레드 정지 …ms scene=…` 경고로 남긴다. 튕긴 PC의 `%USERPROFILE%\AppData\LocalLow\gaboja-studio\POC_CURTAINCALL\Player.log`에서 정지 시각과 직전 로그를 보고 원인 작업을 찾는다.
- 전송 제한 시간을 늘려도 Relay 서버의 10초는 바뀌지 않는다. 한 프레임 정지를 10초보다 충분히 짧게 나누는 것이 근본 해결이다.

## 검증

- `StartConnectionTests.TransportFailureReportsStallReasonInsteadOfTransportShutdown`
