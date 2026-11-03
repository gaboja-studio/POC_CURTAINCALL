# Task-20261103-099

- **Title:** 추격 대기실 실측과 추격 스냅샷 경량화
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/pursuit/network-perf
- **Base:** dev

## Context
추격 대기실 렌더링·씬 로딩이 무겁고, 멀티플레이 중 일부 또는 전원이 타임아웃으로 튕긴다는 보고. 1차로 실측 도구와 추격 스냅샷 경량화를 진행한다.
알려진 원인(NetworkSessionManager 주석, 2026-11-02): Relay는 메인 스레드가 약 10초 멈추면 할당을 끊는다.

## 초기 브랜치 구현 (통합 전 이력)
- `PursuitSnapshotCodec`: JSON 문자열 → 바이너리(위치 cm short, 압축 쿼터니언, 플래그). 알림 문구는 수신 측 설정에서 조회. 메시지 이름 v2.
- `PursuitDirector`: 준비 알림(v2.ready)을 보낸 클라이언트에게만 전송, 1100B 이하면 UnreliableSequenced(전체 상태라 유실 자동 복구), 넘으면 ReliableFragmentedSequenced. 수신 준비 클라이언트가 없으면 스냅샷 생성 생략.
- `PursuitPerformanceProbe`(Tools/Curtain Call/Performance/Measure Pursuit Waiting Room): 플레이에서 씬 로드 → 최장 멈춤 프레임·상위 비용, 씬 규모, 3개 방 렌더 통계, 스냅샷 크기 → Logs/Performance/*.json(+프로파일러 .data), 기준값 비교.

진행 상황·남은 작업·다른 PC 재개 절차: [optimization-roadmap.md](optimization-roadmap.md)
이후 작업은 다른 PC의 `feat/waiting-room/pursuit-system`에 이 브랜치를 병합한 뒤 계속한다(2026-11-03 사용자 결정).

## 기준값(Probe)
최장 멈춤 5000ms(에디터, Relay 10초의 절반), 스냅샷 1100B, 방별 batches 3000, shadowCasters 2000.

## 통합 후 P1 구현 묶음
- 통합 제출 작업 브랜치: feat/waiting-room/pursuit-system (주 Task-094). Task-099의 원래 Branch는 중복 작업 매핑을 피하기 위해 유지하며, PR 제출 뒤 후속 전용 브랜치로 갱신한다.
- 이 PC 통합 기준값을 보관하고, Awake의 전체 마네킹 생성/행동 그래프 초기화를 프레임당2ms 예산·최대4체로 나눈다. 전체 생성 전 판정·스냅샷 적용·준비 알림을 막고 커튼은 초기화 완료를 기다린다. 씬 배치 순서/동기화 인덱스 보존.
- 초기화 총시간·가장 긴 개별 생성·분산 프레임 수를 측정한다. Probe는 씬을 처음 연 에디터 세션과 반복 로드를 구분하고, 첫 로드 전용 메뉴는 이미 씬을 열었으면 거부한다. OS/GPU 캐시까지 지운 콜드 로드와는 구분한다.
- 실제 상태4Hz/위치20Hz 전송량을 보고한다. 조명은 계산/깜빡임을 유지하며 값이 동일한 Light/환경광 쓰기를 생략한다.
- 의미 있는 초기화/중단/상태 수신 회귀 및 기존 추격 회귀, 전후 측정·check-work 후 기록한다. 빌드/Relay 다인 실기는 환경 밖 검증으로 남긴다.

## 미정·주의
- 다인 실기(Relay) 검증 필요: 스냅샷 수신·준비 알림·늦은 입장.
- 렌더(로비 batches 6110/그림자 4608)·씬 분할·정적 배칭(23k 중 180만 배칭)은 씬 담당자 협의 후 별도 Task.
