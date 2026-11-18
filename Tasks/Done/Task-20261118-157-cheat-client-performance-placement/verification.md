# 검증 기록

2026-11-18 / Unity 6000.3.25f1 / MPPM 2.0.2 / dev.

## 원인 재현
- 실제 MPPM 메인 에디터와 별도 Player 2로 LAN17934 연결. 로비를 건너뛰어 대기실→외줄 공연 이동.
- 수정 전 P2는 공연 상태를 수신해도 Director/Entry가 없어 출발점 오차75.42m, 이후 낙하. 기존 Prepare 1회 호출만으로 오차0.08m/Normal로 복구.
- 추가 새 세션 직행에서는 최초 캠페인 상태가 sceneLoaded 이후에 도착하는 경로 확인. 최종 수정본의 읽기 전용 observer 기록: 씬=Tightrope, 세션=Playing, 캠페인 존재/Phase=None/IsRunning=false, Entry=true/initialized=false. 수신 이후 자동 초기화/배치 완료.

## 최종 실제 2인 MPPM
Host PID35824, Client PID27496(Library/VP/mppm476bfb2f), 서로 다른 Pipeline 서버 사용. 배치 진단에는 Instance만 읽었으며 클라이언트 Director 생성이나 수동 Prepare/좌표 보정을 하지 않았다.

| 경로 | 호스트/참가자 결과 |
|---|---|
| 새 세션 Start→외줄 공연 직행 | 모두 Normal, LocalPlaced=true, 출발점 오차0.08m |
| 같은 외줄 공연 다시 이동 | Entry 각1개, 조작 가능, 외줄 Running, 모두 Normal/오차0.08m |
| 외줄→인간대포 공연 | Entry 각1개, 조작 가능, 모두 Normal/오차0m |
| 인간대포→대기실 | 공연 Entry0개, 조작 가능, 모두 Normal/오차0.02m |
| 대기실→외줄 공연(원래 실패 경로) | Entry 각1개, 조작 가능, 외줄 Running, 모두 Normal/오차0.08m |

| 15초 안정성 관찰 | 프레임 | 최소 인원 | Normal/배치 유지 | 최대 출발점 오차 | 오류/예외 |
|---|---:|---:|---|---:|---:|
| 외줄 직행 Host | 738 | 2 | 유지 | 0.08m | 0 |
| 외줄 직행 Client | 795 | 2 | 유지 | 0.08m | 0 |
| 인간대포 Host | 584 | 2 | 유지 | 0m | 0 |
| 인간대포 Client | 635 | 2 | 유지 | 0m | 0 |

동일 PC의 실제 2프로세스 LAN 검증이며 Relay/지연·손실 주입·실제 두 대 PC 검증은 아니다. 공연 완주/발사 조작 전체가 아니라 요청된 치트 이동/배치/입력 잠금 해제와 상태 동기화를 검사했다.

## 자동 검사
- PerformanceEntryJudgeTests: 7/7 PASS(31.75초). 관리자 없는 NGO 씬 로드, 늦은 첫 스냅샷, 중복 초기화 방지, 배치 전 사망 보호, 부활 후 재배치, 밤 전환 보호, 독립 대포 사망 판정.
- Standalone_ActualSceneBridgeSettlesOnce_WithoutSessionEndOrTrickRetry: PASS(11초). 독립 공연의 기존 Bridge 실행/정산 정책 유지.
- 기존 테스트의 오래된 선택 인자 리플렉션 호출(StartSession/ServerSetState/ServerKill)만 현재 API에 맞춤. 게임 규칙 변경 없음.
- check-work 최초 문서 길이 초과는 내용 압축으로 해결. 후속 verify-fast OK(hard0), verify-unity Compile PASS 및 Common settings PASS. 검사는 56c05863 기반 수정본에서 수행했으며, 제출 전 합류한 26c4cdb9는 별도 탈출로 에셋/연출 변경이다.
