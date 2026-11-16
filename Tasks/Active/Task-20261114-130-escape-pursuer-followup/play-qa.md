# 탈출 구간 실제 플레이 재검사

2026-11-14 사용자 직접 요청으로 Codex의 PlayMode 실행을 허용했다. 첫 대포 정지·버벅임에서 탈출 구간 전체 검사로 범위를 확대했다. 개발 체크포인트로 시작한 재현은 입구부터 연속 완주와 구분한다.

## 수정 전 실제 재현

- 복사 씬의 원본 호스트/플레이어로 시작했다. 입력 없이 출발점에 머무르면 추격자의 포획·절단·투척/자동 재시작이 진행됐다.
- 첫 대포 개발 체크포인트에서 실제 Board/Launch 요청을 보내 비행했다. 첫 시도는 착지 외줄 이후 걷기·균형 입력을 주지 않아 낙하했다. 이 코스는 대포→착지 외줄→봉 순서이므로 대포 사거리 결함으로 판정하지 않는다. 이후 W 전진/A·D 균형 입력과 실제 가까운 봉 상호작용으로 첫 봉 탑승에 성공했다.
- 외줄 출발 개발 체크포인트에 플레이어만 배치하고 추격자 상태를 조작하지 않았다. 추격자가 첫 대포 뒤 서비스 경로0.25877887, world(-220.441,25,55.071)에서20초 이상 영구 정지했다. gate0.started=-1, 실제 벽 거리2.82639551m, 공격/투척/도약 모두 비활성이었다.
- 원인: PrepareTravel의 확장 AABB+1.1m 정지 예측은 벽 타격 가능 거리2.8m보다 먼저 이동을 막는다. 스킨 충돌이나 NavMesh 연결 검사는 이 갱신 조건 교착을 발견하지 못했다.
- 첫 발판 실제 위치에서100회 반복 측정: 손 위치1회30.20ms/3회96.94ms, Navigation.Step79.42ms, 공격 위치 탐색95.05ms. 런타임 자체 시간과 전체 FPS는 별개이며 GC 측정값0의 지원 여부는 불확실하다.

근거: `.harness/local/pursuer-followup/cannon-*-before.jsonl`, `pursuer-perf-before.json`.

## 수정 후

- 첫 대포→착지 외줄→첫 봉 실제 입력 재현에서 벽 타격203.983초/파손204.613초를 확인했다. 기존2.826m 정지는 해소됐다.
- 이어진 첫 봉 대기에서 별도 정지 재현: 추격자 world(-226,25,65.3), cursor0.999999642/desired1로15.54초 고정. 월드 위치가 경로점과 같아 이동 루프가 끝나지만 다음 등장 조건은 cursor>=1을 요구했다. 실제 도달한 지상 앵커의 cursor 정규화로 수정했다. 벽·경계 회귀19/19 통과.
- 두 수정 후 같은 대포→외줄 걷기/균형→첫 봉 탑승을 다시 실행했다. 추격자는 첫 등장 발판(-227,25,45.3)에 도달했고28.85초 DebrisWarning→29.89초 DebrisFlight→31.70초 Impact와 동시에 봉 이탈→34.08초 Retired→자동 재시작으로 이어졌다. AI 위치·상태를 강제 변경하지 않았다. 근거 `cannon-rope-pole-anchor-after.jsonl`, `escape-live-after.jsonl`의 `first-cannon-anchor-fixed`.
- 외줄 출발 체크포인트105초 실행에서 추격자가 첫 벽을 지나 경로22/등장4까지 이동, 파편을 던지고 다음 등장 위치로 재배치했다. 선택한 발판에서 사거리 안에 있는 목표를 경로 index 범위 밖으로 간주해 조기 철수하고 먼 발판까지 왕복하는 비효율도 관찰했다. 선택·유지 판정을 실제 공격 사거리로 통일하고, 공격 가능한 발판을 재방문 쿨다운 때문에 사거리 밖 발판보다 뒤로 미루지 않도록 수정했다. 관련 회귀는 아래 최종417개 검사에 포함한다.
- 동일 첫 발판100회 Navigation.Step 측정은79.42→26.33ms. 단일 루틴 측정이며 전체 FPS 개선 수치가 아니다. 손 위치 getter 자체는 바꾸지 않았고, 투척 준비 중 중복 호출을 제거했다.
- 임시 입력 도구의 Keyboard bitfield/extension 호출 오류가 발생했다. aggregate KeyboardState 전송으로 도구를 수정했고, 해당 오류는 게임 결함과 분리했다.
- 최종 코드의 탈출로 EditMode417/417 통과(32.04초). 비영 거리1mm 잔차에서도 실제 이동 예산을 소비해 정확한 목표점까지 도달하도록 진행률 deadband를 제거했다.
- 최종 첫 봉 재실행(`cannon-final-after.jsonl`)에서도 타격 거리2.8m→cursor1→등장0→실제 파편 발사가 이어졌다. 첫 봉 공격은 코드 변경 전후를 구분해 두 번 확인했다.
- 컨베이어: 대포3 체크포인트에서 접근·탑승, 실제 이동 봉 도착 시각에 맞춰 발사, 가까운 봉 잡기, 운반 중 대기, 하차 구간 F 길게 누르기로 정상 바닥에 착지했다. 종료 world(-238,5.880,186.334), Normal/봉 미점유/비행 종료. 근거 `conveyor-play.jsonl`.
- 화염복도 연결 계단: 실제 계단 입구 한 번 배치 후8개 꺾임/착지 지점을 W 이동으로 통과했다. 별도 재시작 뒤 W+Shift와 계단마다 Space 점프를 조합해 다시 통과했다. 두 경우 모두 Normal/LostParts=None, 끝 world(-322.8,17.08,68.32). 근거 `stair-walk.jsonl`, `stair-running-jump.jsonl`. 계단 입구부터의 검증이며 앞선 협동 외줄 완주를 의미하지 않는다.
- 화염복도: 입구에 한 번 배치한 뒤 장비 없이 실제 이동과 화염 주기 대기로 6쌍/12개 화염을 통과했다. 안전 지점0~5를 순서대로 지나17.75694초에 완료, 종료 world(-322.729,17.080,29.322), Normal/LostParts=None/gear=none. 근거 `flame-play-actions.jsonl`. 포커스가 없던 앞선 도구 시도는 `flame-unfocused-attempt.jsonl`로 분리했다.
- 최종 AI: 플레이어만 최종 전투 체크포인트에 배치하고 추격자는 입구에서 자력으로 전체 서비스 경로를 이동했다. 공유 epoch419.22704 실행에서235.84초 Ignition→237.33초 Chasing→238.61초 route70/등장9 도착→247.11초 LeapWarning→247.95초 Leaping→249.40초 Landing→250.64초 Battle→252.11초 Grab/EscapeCaptured→256.54초 Throw→259.48초 Retired→자동 재시작을 확인했다. 10초 넘게 위치가 고정된 최종 착지 이후 구간은 포획·던지기·사망 상태가 진행됐으며 경로 교착이 아니었다. 근거 `finale-chase-after.jsonl`, `escape-live-after.jsonl`의 `finale-ai-approach`. 이는 AI의 입구부터 최종 접근 검증이며 플레이어의 입구부터 연속 완주와 구분한다.
- 출구 첫 시도는 임시 QA 드라이버가 콘텐츠 축을 잘못 가정해 바닥 밖 사격 위치로 걸어갔다. 입력 경로를 실제 지지 바닥 위로 수정한 재실행에서 원본 장치의 잡기·사용 요청으로 필수 케이블24/24개를 절단했고, 4개 톱 장치 모두 DoorOpen=true가 됐다. 이후 실제 이동으로 도착 영역 안 world(-603.815,25.080,40.303)에 도달했으며 플레이어는 Normal이었다. 근거 `exit-supported-play-actions.jsonl`, `exit-result.json`.
- 출구 검증 범위: 위 실행은 독립 개발 씬이며 `campaign=false`, `contentCampaign=false`, `practice=true`였다. 도착 영역 안/ExitOpen=true까지 확인했지만 정상 캠페인 도착 판정과 엔딩 전환은 실행되지 않았다. QA 드라이버의 최종 `completed=false`/도착 판정 대기 오류를 남겼으며, 엔딩 검증 완료로 기록하지 않는다.

## 실제 2인 이동 조합

MPPM 호스트(slot0/id0)와 클라이언트(slot1/id1)에서 마지막 재시작 epoch249.9015503의 결과를 확인했다. 각 프로세스의 `local=true` 소유자 스냅샷과 위치 변화를 판정 근거로 사용했다. 시작 위치만 최종 발판에 배치하고 이후 정상 입력 파이프라인으로 다음 동작을 실행했다.

| 단계 | 실제 소유자 상태 — 양 플레이어에서 확인 |
| --- | --- |
| 0 걷기 | moving=true, crouched/sprinting=false, 지상 속도3.0m/s와 실제 위치 이동 |
| 1 달리기 | sprinting=true, 지상 속도6.0m/s와 실제 위치 이동 |
| 2 앉아 걷기 | crouched=true, sprinting=false, 지상 속도1.5m/s |
| 3 달리기+앉기 | crouched=true, sprinting=false, 지상 속도1.5m/s로 앉기 우선 처리 |
| 4 이동+점프 | InPlace/airborne=true, 발높이25.08m에서 최고 관측25.866m/25.865m까지 상승 후 하강, grounded=true/Jump=None 복구 |
| 5 착지 후 걷기 | 지상 속도3.0m/s, Normal 상태로 재이동 |
| 6 입력 종료 | 실제 속도0, moving=false, 동일 위치에 정지 |

모든 단계에서 두 소유자 모두 Normal·simulated=true·control=true였다. 입력 요청 기록만으로 성공 처리하지 않았다. 근거 폴더는 `.harness/local/pursuer-followup/pair-qa-20261114-141604425/`이며 `locomotion-0/1.jsonl`과 `pair-observer-slot0-pid38384-20261114-141955275.jsonl`, `pair-observer-slot1-pid8092-20261114-141957383.jsonl`을 대조했다. 첫 임시 입력 도구의 `c.target` Vector3 직렬화 오류는 수정 전 시도로 분리했다. 최종 단계0~6은 양쪽 모두 완료했으며 관찰자 자체는 위치·속도를 문자열로 기록한다.

## 실제 2인 트램폴린·운반자 사망

공유 epoch469.35124에서 호스트0을 하부(-289.8,20.08,90), 클라이언트1을(-289.8,20.08,96.5)에 준비 배치했다. 클라이언트가 약0.21초 후 Normal 상태로(-285,25.05,94.3)에 이동한 원인은 `EscapeCooperativeRopes`의 하부 작업자1인 규칙이다. 두 준비 위치 모두 `SingleWorkerBounds` 안이며, 먼저 점유한 host0 이외의 하부 지상 플레이어는 `ReturnOverflow→AuthorizeOverflow→ReturnLocalOverflow` 승인 후 `overflowReturnPoint`로 복귀한다. 씬의 반환점 frame(85,.05,24)이 관측된 월드 좌표와 정확히 일치한다. 이 체크포인트 배치로 발동한 기존 협동 구역 동작을 추격자나 트램폴린 고장으로 판정하지 않았다.

- 운반: host0의 실제 소유자 기록에서 `carryType=HumanCannonTrampoline`과 장치의 동반 이동을 확인했다. side=.12 걷기는0.540m/s, side=-.12 달리기는 sprinting=true/1.080m/s, move=.1 앉기 및 반대 방향 앉기+달리기는 crouched=true/sprinting=false/0.225m/s였다. 이 값은 작은 아날로그 입력에서의 실측이며 전속력 수치가 아니다. 각 구간에서 Normal·control/simulated=true를 유지했다. 정상 내려놓기와 다시 잡기로 carrierNobody→0 전환도 확인했다.
- 운반자 포획: `timeline-0.jsonl`의 공유 clock537.652에서 host0이 트램폴린 운반 상태에서 EscapeCaptured로 바뀌며 carry가 해제됐고, clock545.152에 Retired가 됐다. 이후 관찰 스냅샷에서 장치는 carrierNobody, world(-290.902,20.000,92.658)의 지상 위치였다.
- 다른 플레이어의 반동: `actions-1.jsonl`의 추가 명령78440381은 jump=true/sprint=true를 포함해 상부에서 트램폴린 쪽으로 접근했다. 소유자 `timeline-1.jsonl`에서 clock539.492에 y20.714까지 하강한 뒤539.868부터 flying=true로 상승, 최고 관측 y27.877,541.757에 y25.08/flying=false로 상부 착지를 확인했다. 재설치된 클라이언트 관찰자에서 `LocalBounceCount=1`도 확인했다. 새 관찰자가 반동 뒤 시작했으므로 이 구간의 비행 근거는 timeline이며, 발판 이탈 직후 별도 점프가 실제 발동했는지는 확정하지 않는다.
- 후속 추격: client1은 반동 후에도 Normal을 유지하다 clock546.284에 EscapeCaptured,553.784에 Retired가 됐다. 관찰자에는 공격 대상1/Grab→Throw가 기록됐다. 두 명이 모두 탈락한 뒤 새 epoch554.565로 자동 재시작해 두 플레이어 Normal과 트램폴린의 원래 위치/점유 해제를 확인했다.

순서는 운반자 포획·장치 해제→클라이언트 반동·착지→운반자 사망→클라이언트 포획·사망이다. 반동은 host0의 Retired 이전에 발생했다. 관찰자180초 종료와 재설치 사이에는 공백이 있어, 운반자 최초 공격 단계와 장치가 땅에 닿는 순간은 상세 관찰자로 연속 기록하지 못했다. 해당 구간의 소유자 상태·운반 해제는 계속 기록된 timeline으로 교차 확인했다.

근거: 위 2인 증거 폴더의 `trampoline-0/1.jsonl`, `actions-0/1.jsonl`, `timeline-0/1.jsonl`, 호스트 관찰자 `142215614`/`142537177`, 클라이언트 관찰자 `142225116`/`142538213` 파일. 게임 코드·구역 규칙·에셋을 변경하지 않고 조사했다.

## 실제 2인 목마·사망 후 대상 전환과 별도 회귀

같은 증거 폴더의 호스트 관찰자 `142215614`와 클라이언트 관찰자 `142225116`에서 F 탭으로 client1이 host0 위에 탑승한 상태를 확인했다. 아래 시각은 프로세스별 Time.time이 아닌 공유 clock이다.

- 431.217 파편 Impact에서 두 사람 모두 Normal인 채 목마가 해제됐다. 431.409 클라이언트 소유자 기록에서 위 사람1은 바닥(-576.702,25.080,40.950)에 내려와 simulated/control=true였다.
- 최초 포획은 위 사람이었던 client1이다: 432.414 Grab/1→436.982 Throw/1→439.899 Retired. host0은 그동안 Normal이었다. 이후 440.108 Warning/0→440.733 Grab/0→445.316 Throw/0→448.233 Retired로 이어졌다. 첫 피해자 사망 뒤 약0.21초 안에 생존자 공격 경고로 전환했다.
- 따라서 이 실제 목마 실행은 아래 사람 포획 때문에 위 생존자가 이동하는 경계의 재현이 아니다. host0 포획 시 이미 사망한 client1에 보낸 이동 명령도 생존자 이동 검증으로 세지 않았다.
- 별도 EditMode 6개로 포획/사망 상태와 목마 해제의 같은 프레임 순서를 재현했다. 수정 전5실패/1통과: 아래 사람이 EscapeCaptured/Retired/Fallen이면 위 생존자를 (9000,9001.5,9000)에서 이동한 아래 사람 옆 (9005,9007,9003.65)으로 옮겼고, 위 사람 자신이 포획되면 목마 해제가 Simulated=true로 덮었다. 정상 하차 대조군은 통과했다.
- `PiggybackFollow`는 아래 사람이 Normal일 때만 일반 하차 위치를 적용하고, EscapeCaptured인 소유자의 이동 잠금을 보존하도록 두 조건만 수정했다. 수정 후 새6개를 포함한 전체 `TightropeSurfaceControlTests`150/150 통과(10.64초). 근거 `piggyback-capture-before.json`, `piggyback-surface-after.json`; 수정 후 실제 목마 포획 플레이를 다시 실행한 것으로 기록하지 않는다.

캠페인 엔딩 전환 및 입구부터 2인 연속 완주는 별도 미검증 항목이다.
