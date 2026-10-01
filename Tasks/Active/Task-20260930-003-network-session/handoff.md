# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 3단계 4인 자동 시작·게임 상태 공유 완료(2·3단계 미커밋). 남은 것: 공개 진입점 정리·Domain Map
- 동작하는 것: `Assets/Scripts/Network/Session/` — `ServicesSessionConnector`(Relay 세션, 방 코드, 실행마다 익명 프로필 분리), `NetworkSessionManager`(공개 진입점), `ISessionConnector`(접속 방식 추상화), `LanSessionConnector`, `NetworkSessionTestUI`(IMGUI 임시). 배치(작업자 직접): `NetworkManager.prefab`(NetworkManager+UnityTransport+NetworkSessionManager), 씬 `SessionUI` 오브젝트
- 주의: 프리팹 NetworkConfig.NetworkTransport가 비어 있어 `EnsureTransportAssigned()`가 실행 시 연결한다. `.gitattributes`의 unity-yaml에 `-whitespace` 추가(작업자 승인)
- 막힌 것: 없음 / PR: 없음. 참고: `com.unity.pipeline` BasePipelineServer의 ObjectDisposedException 로그는 에디터 브리지 쪽(게임 무관, Packages 수정 금지라 무시)

## 다음 할 일

1. (완료) 2단계 플레이 테스트 — 작업자 확인: 방 코드 생성·참가·나가기·방 닫기 정상 (2026-10-01, MPPM)
2. (완료) 3단계 MPPM 4인 테스트 통과(2026-10-01): 자동 시작, 진행 중 이탈 유지·재참가 거절, EndGame, 호스트 종료. 규칙은 meta.md Decisions

## PM이 확인할 것

- 확인 요청: `.gitattributes` 변경(Unity YAML 줄 끝 공백 검사 제외) / 대기 결정: 없음 / 공용 파일 요청: 없음

## Verification

- 구역: OK / 문서·규칙: OK / 컴파일: PASS (2026-10-01) / 테스트: NO_PROJECT_TESTS (결정)
- 실행 로그: 미실행 / 공동 테스트: 미요청
- 플레이 테스트(작업자): 1단계 LAN·2단계 방 코드·3단계 4인 게임 상태 통과 (2026-10-01, MPPM)
