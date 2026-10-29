---
summary: 방 코드·LAN 접속 중 OperationCanceledException은 상위 접속 취소의 결과일 수 있다.
status: active
updated: 2026-10-21
source: human
---

# 방 참가 중 취소 예외

## 언제 읽나

방 코드 또는 LAN 참가가 일정 시간 뒤 실패하거나 ServicesSessionConnector.StartAsync에서 OperationCanceledException이 날 때.

## 원인과 확인

- NetworkSessionManager.JoinAsync의 제한시간은 인증·SDK 요청·Relay·NGO 연결 및 이전 SDK 작업 정리 대기를 모두 포함한다. 기존 3초 제한이 정상 연결도 취소할 수 있었다.
- 2026-10-21 PM 긴급 요청으로 dev에서 공통 제한을 30초로 늘렸다. LAN도 같은 제한을 사용한다.
- 접속 취소 로그의 Timeout / UserCancelled / NetworkStopped / JoinFailed / HostFailed와 mode, elapsedMs, disconnectReason으로 시작 원인을 확인한다.
- JoinError는 취소 처리 전에 발생한 커넥터 예외다. SDK가 감싼 취소 예외는 이미 취소했거나 교체된 요청이면 중복 출력하지 않는다.
- NetworkStopped는 제한시간 전에도 발생할 수 있다. 호스트 거절·Transport 종료 등 실제 원인은 앞선 NGO/Transport 로그와 disconnectReason으로 확인한다. 제한시간 연장만으로 방화벽·주소·호스트 종료가 해결되지는 않는다.

## 검증

- StartConnectionTests: LAN 제한시간, 3초 이후 대기 유지, 사용자 취소, 네트워크 종료 사유, 이전 요청의 늦은 실패 격리.
- 이번 수정은 로컬 파일에 적용했으며 컴파일·테스트는 에디터 연결 상태에 따라 별도로 확인한다. 두 PC의 실제 Relay/LAN 접속은 공동 검증이 필요하다.
