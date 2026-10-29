---
summary: 대기실에서 주기적으로 멈출 때 텔레메트리 코루틴의 동기 파일 작업 확인
status: active
updated: 2026-10-29
source: Task-20261028-077, Task-20261029-078
---

# 업로드 코루틴의 메인스레드 정지

## 언제 읽나
- 대기실에서 주기적으로 턱턱 끊기거나 TelemetryRuntime.UploadLoop의 CPU 시간이 클 때.

## 증상
- 실제 2일차 Between 대기실에서 118.88/132.85ms 프레임의 UploadLoop가 각각 109.45/118.64ms였다. 시계 LateUpdate·Canvas.BuildBatch는 같은 측정에서 훨씬 작았다. 모든 끊김 원인이 같다는 뜻은 아니다.

## 원인
- Coroutine은 작업 스레드가 아니다. yield 전 후보 파일 탐색·읽기·JSON/UTF-8 생성과 응답 후 ACK 파일 재독출·역직렬화·해시·삭제가 메인스레드에서 실행됐다.
- SendWebRequest는 yield하므로 네트워크 응답을 동기 대기한 문제와 구별한다. 하위 개별 I/O 중 어느 것이 지배적인지는 미확정이다.

## 대처
- 참가자 입장 직후에만 끊기면 `NetworkLogAdapter.Tick`의 중계도 확인한다. UploadLoop를 비동기로 바꿔도 별개인 NextRelayPath 전체 파일 검색·JSON 검사와 ReadReceipt 재검증/삭제는 메인에서 돌고 있었다. ACK 뒤 nextRelay=0으로 대기열을 연속 처리해 초기 버스트가 생길 수 있다.
- 참가자 중계는 PrepareRelayAsync/AcknowledgeRelayAsync 작업 스레드로 처리한다. 준비 완료 전 프레임은 양보하고, 재전송은 준비된 원본 바이트를 쓴다. 정상 ACK 뒤 최소 1초 간격, 미확인 재시도 3초다. 연결 종료는 취소·참조 무효화하며 정확한 ID/개수/원본 해시와 내구 저장 ACK 전에는 파일을 지우지 않는다.
- DurableOutbox.PrepareUploadAsync/AcknowledgeUploadAsync에서 봉인 파일 작업을 Task.Run으로 처리한다. UnityWebRequest·Unity API·오류 표시·상태 판단은 메인스레드에 둔다. 완료 전에는 yield하며 메인에서 Task.Wait/미완료 Result를 사용하지 않는다.
- worker는 active를 건드리지 않는다. currentBatches는 ConcurrentDictionary, 봉인 파일 준비 읽기·ACK 삭제·AcceptRelay 쓰기는 동일 ID의 비차단 게이트로 보호한다. 경합 시 재시도하며 메인도 획득하는 lock 안에서 I/O하지 않는다.
- 용량·봉인 배치 수는 시작 시 파일 색인을 한 번 만들고 성공한 AtomicWrite/DeleteFile 때만 증분 갱신한다. 색인 잠금 안에서는 파일 I/O하지 않는다. ACK ID/개수/원본 해시 검증, 원본 바이트·현재 실행 우선순위·재시도를 유지한다. 종료/코루틴 중단은 취소하며 확인되지 않은 기록을 지우지 않는다. 실행 중 외부에서 outbox 파일을 편집하는 것은 지원하지 않으며 재시작 시 재색인한다.
- 첫 업로드 수정 뒤에도 사용자 방 입장·입력 끊김이 남았다. Lobby 입력 프레임에서 Append 114.78ms 중 PendingBytes 112.46ms를 재현했다. 키마다 누적 파일 전체의 FileInfo.Length를 디스크에서 읽던 경로이며 정지 대기실 표본으로는 놓친다. 색인으로 전체 재조회를 제거한다.
- Append/Seal의 즉시 영속화(Flush(true))는 별도 경로다. 측정 없이 비동기화해 반환 전 저장 계약을 약화하지 않는다. Telemetry.Outbox.Append/PendingBytes/AtomicWrite 마커로 입력 중 비용을 구분한다.

## 확인 방법
- 2026-10-29 로컬 대기열 67파일 읽기 전용 측정: 동기 후보 검색 283.47/30.71/30.79ms → 비동기 요청 메인 비용 0.165/0.007/0.008ms. 작업 전체 시간이 줄었다는 뜻이 아니며 실제 두 PC Relay 입장의 3~5초 전체 프레임 재현과 구분한다. `Telemetry.Relay.Tick`으로 메인 비용을 추적한다.
- DurableOutboxTests 20개(비동기 7개·색인 3개 포함), TransportPolicyTests 7개. 최종 27/27 통과. 색인은 UTF-8 덮어쓰기·봉인·ACK·복구·용량 초과·저장 실패 후 원본/집계 유지·비동기 경합을 디스크와 대조한다.
- 수정 후 동일 대기실에서 별도 600/900프레임 측정: 전체 최대 32.18/31.62ms, UploadLoop 최대 0.328/0.910ms. 실제 업로드 ACK 시각 전진·오류 없음 확인. 에디터·Profiler 포함 제한된 표본이며 빌드 전체 성능·사람 최종 플레이 검증은 아니다.
- 색인 수정 후 Lobby에서 게임 기능에 연결되지 않은 F13 press/release 80회 예약, 표본 내 79개 입력 저장 확인: 572프레임 중앙값 16.63ms/p95 18.77ms/최대 22.79ms. Append 최대 16.40ms, PendingBytes 0.0009ms, AtomicWrite 14.75ms. 입력·디스크 저장을 포함한 제한된 에디터 표본이며 2인 장시간 플레이와 같지 않다. 동기 저장 비용은 남는다.
- Unity ProfilerDriver.enabled와 Profiler.enabled를 함께 켜야 RawFrameData를 얻을 수 있었다. 조사가 끝나면 둘 다 원래 상태로 복원하고 직접 시작한 Play만 종료한다.
