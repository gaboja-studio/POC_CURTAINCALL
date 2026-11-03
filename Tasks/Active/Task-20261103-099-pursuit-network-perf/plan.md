# Task-20261103-099

- **Title:** 추격 대기실 실측과 추격 스냅샷 경량화
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/pursuit/network-perf (feat/pursuit/light-effects 위에서 분기)
- **Base:** dev

## Context
추격 대기실 렌더링·씬 로딩이 무겁고, 멀티플레이 중 일부 또는 전원이 타임아웃으로 튕긴다는 보고. 1차로 실측 도구와 추격 스냅샷 경량화를 진행한다.
알려진 원인(NetworkSessionManager 주석, 2026-11-02): Relay는 메인 스레드가 약 10초 멈추면 할당을 끊는다.

## 구현
- `PursuitSnapshotCodec`: JSON 문자열 → 바이너리(위치 cm short, 압축 쿼터니언, 플래그). 알림 문구는 수신 측 설정에서 조회. 메시지 이름 v2.
- `PursuitDirector`: 준비 알림(v2.ready)을 보낸 클라이언트에게만 전송, 1100B 이하면 UnreliableSequenced(전체 상태라 유실 자동 복구), 넘으면 ReliableFragmentedSequenced. 수신 준비 클라이언트가 없으면 스냅샷 생성 생략.
- `PursuitPerformanceProbe`(Tools/Curtain Call/Performance/Measure Pursuit Waiting Room): 플레이에서 씬 로드 → 최장 멈춤 프레임·상위 비용, 씬 규모, 3개 방 렌더 통계, 스냅샷 크기 → Logs/Performance/*.json(+프로파일러 .data), 기준값 비교.

진행 상황·남은 작업·다른 PC 재개 절차: [optimization-roadmap.md](optimization-roadmap.md)
이후 작업은 다른 PC의 `feat/waiting-room/pursuit-system`에 이 브랜치를 병합한 뒤 계속한다(2026-11-03 사용자 결정).

## 기준값(Probe)
최장 멈춤 5000ms(에디터, Relay 10초의 절반), 스냅샷 1100B, 방별 batches 3000, shadowCasters 2000.

## 미정·주의
- 다인 실기(Relay) 검증 필요: 스냅샷 수신·준비 알림·늦은 입장.
- 렌더(로비 batches 6110/그림자 4608)·씬 분할·정적 배칭(23k 중 180만 배칭)은 씬 담당자 협의 후 별도 Task.
