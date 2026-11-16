# 미궁 복사 씬 플레이 검증

2026-11-16. 작업 씬은 `EscapeRouteLabyrinthPrototype.unity`, 현재 DEV 기준은 `0f95ba68`이다. **최신 3·4인 입구→성공 엔딩은 아직 미완료**다. 준비 위치 구간 검사, 이전 배치 실기, 실패한 연속 실행을 성공 완주로 합산하지 않는다.

이전 실행·어그로 원수치·구간 실패·복구 상세 원문은 [History/004-labyrinth-play-before-recovery.md](History/004-labyrinth-play-before-recovery.md)에 그대로 보존했다.

## 검증 기준

- 실제 Development LAN 클라이언트를 `PlayerInputFrame`의 걷기·달리기·균형·F·Space·장치 사용으로 조종한다. 자동 균형과 대포 타이밍 보조가 있으므로 사람의 난이도·체감과 구분한다.
- 연속 실행은 입구 시작, continuous=true/restarts=0, setup·damage·restart 명령 0개를 확인한다. 같은 라운드에서 진단 대기·입력 수정 후 재개한 경우 별도로 기록한다.
- 통과는 실제 좌표·지지 착지·점유·공통 비행 상태를 함께 확인한다. 명령 발행이나 함수 반환, 추격자의 routeProgress만으로 통과했다고 세지 않는다.
- 출구 배선 24개가 필수이고 보스 처치는 선택이다. 결과는 남은 참가자 처리 후 한 명이라도 Arrived인지 확인한다.
- 원자료는 `.harness/local/pursuer-campaign-20261115/<실행명>/`의 timeline/actions/shots/state다. actor ID와 slot 대응은 실행마다 확인한다.

## 현재 배치와 코드

- 공통 봉 신규 10개(검사동15~17, 열 회수18~21, 상부22~24), 등록 봉25/외줄20이다. 원본74개 추격 앵커·현재14개 등장 지점을 유지한다.
- 바닥 경로·승차·가장자리·작업장 표식 제거 후 조회 removedNames/horizontalPaint 모두 빈 배열. 생성 감사 failures0/warnings271/colliders952/ropes20이다. 실제 전체 가독성·조명 최종 조정은 남아 있다.
- 컨베이어 확장 관측장 내부에 남은 옛 Launch side guard를 제거했다. 직접 접근 캡슐 검사 hits[] 및 연속 실행의 정상 접근을 확인했다. 외측 낭떠러지 난간은 유지한다.
- 입구 ACK 전 추격 타이머 시작을 막았다. 입구 미출발자를 먼 서비스 경로에 투영해 실제 주자보다 우선하는 문제는 trustedProgress/입구 재분류로 수정했다.
- 입구 복귀는 기존 통로를 걸어 돌아와 실제 Entry에 도착하고 완전한 NavMesh 경로가 있을 때만 전환한다. 편집 검사에서 일반·목마 높이 8m 샘플 PathComplete. 전체 실기 복귀 조합은 미완료다.
- 기존 기물/공통 GameSettings 규칙을 유지한다. F 하차 문제 중 Review가 놓은 직후 같은 봉을 Tap으로 재잡던 부분은 개발 입력의 conveyorDisembarking으로 교정했다.

## 현재 빌드·자동 검사

- 전체 `labyrinth-final-parity-build-result.json`: Succeeded, errors0/warnings32, 264.730초. 입구 가드·AI 대상 선정·난간 수정 포함.
- `escape-tests-route-return.json`: EscapeRoute namespace 463/463 PASS, 실패/건너뜀0. 신규 대상·입구 복귀10개 포함. 이후 목마 Nav 샘플 반경 보완은 컴파일·편집 경로로 별도 확인했다.
- Review 진단 scripts 빌드 `labyrinth-hold-diagnostic-build-result.json`: errors0/warnings25,55.912초.
- Review 재잡기 교정 scripts 빌드 `labyrinth-drop-review-build-result.json`: errors0/warnings25,69.680초.
- 위 빌드는 `10633c71` 기준이다. 복구 뒤 `0f95ba68`로 FF했으며 새 빌드·검사를 진행한다. 첫 check-work는 기록 길이 HARD 초과로 문서/규칙 FAIL, 컴파일·테스트 SKIPPED였다. 기록을 History로 보존한 뒤 재시도한다.

## 최근 연속 실행

| 실행 | 관측 | 결과/제한 |
| --- | --- | --- |
| `labyrinth-parity-full-3-01` | host가 검사동·컨베이어·트램펄린·열 회수·상부 새 봉·반출교까지 진행. 입구 생존자의 잘못된 진행 투영 확인 | 긴 진단 대기. host는 대기 중 투척 누적으로 탈락, 후행자는 컨베이어에서 시험 Jump로 추락. 실패 |
| `labyrinth-parity-full-4-01` | 난간 수정 뒤 main/inspection 두 runner의 직접 컨베이어 접근. 같은 기준 첫 대포→컨베이어 약26.09/34.34초 | Review의 F 뒤 재잡기 때문에 하차 실패. 난이도 동등성 PASS가 아니며 이후 교정됨 |
| `labyrinth-parity-full-3-02` | host/참가자 모두 정상 F 하차·하차장 외줄. 트램펄린 운반/재사용, 열 회수·상부 새 봉, 반출교 통과 | 후행자 반동을 지상만 기다리던 pilot 때문에 대기 후 일반 입력으로 재개. setup/restart 없음. 선행자 포획 뒤 남은 actor1로 재추격. actor1은300.200초 작업장 도달,15/24 절단 뒤 포획.343.820초까지 전원탈락 기록 복구. 실패 |

3-02 작업장에서는 절단기의 carryBlocked와 무변위가 함께 관측됐다. 추격자가 옆 약1.56m에 있어 동적 충돌 가능성이 있으나 충돌체 ID 증거는 없다. 정적 새 중앙 설비 원인으로 확정하지 않는다. 회피 pilot은 추격자 반대 방향 이탈·실제 변위 확인으로 개선 중이다.

## 준비 위치 구간 시험

- 검사동 solo:15→16→17→릴레이 대포→컨베이어, 열 회수 solo:18→19→20→섬→4m외줄→21→합류 무부상 관측. 전체 완주 아님.
- 상부 solo의 제동 없는 이탈은 섬을 지나 외줄에 내려 추락한 실패다. Y만 본 이전 착지 라벨은 폐기했다.
- 상부 pair: 두 자리22→23→24, 상단 이탈 차단·하단 먼저 출발, 두 명 실제 섬 착지. 검사동 pair는 양쪽15→16까지이며 양쪽 세 봉 전환으로 확대하지 않는다.
- 열 회수 pair는 두 명18/19/20 점유와 섬 착지. 한 명이19 뒤 먼저 섬에 착지해20을 다시 잡았으므로 양쪽 연속 공중 전환 아님. 뒤 외줄/21 pair 미시험.
- `labyrinth-conveyor-guide-fixture-04`:39.754초 held=-1 비행→40.117초 실제 하차 발판 지지·무부상. 다만 중간 held&&Z>183 표본을 기다리던 스크립트가 이미 놓은 프레임을 놓쳐 timeout/exit1. 실제 하차와 자동 시험 전체 PASS를 구분한다.
- `labyrinth-exit-observed-fixture-01`: setup1/continuous=false,12발·24배선 절단·문 개방·EscapeSucceeded/Arrived 확인. 입구부터 완주·이미 전투 중인 보스 상대 통과 증거 아님.

## 강제 종료 복구

- 종료 확인 후 DEV를 `10633c71→0f95ba68` FF. 충돌0, 변경374개 해시 전부 보존. ZIP CRC 오류0, 변경파일0바이트0, 텍스트343개 NUL 손상0. 원본 PursuitRemodel SHA256도 기존과 일치했다.
- Unity backup을 별도 보존해 저장 씬과 비교했다. 복사 경로에 따른 GlobalObjectIdHash 한 곳 외 차이 없음. missing scripts0, 봉25/외줄20. Prototype을 다시 열었다.
- 마지막 state3개와 timeline 꼬리에 NUL 손상. 원본을 바꾸지 않고 `restart-recovery-20261116/review-log-salvage/`에 유효 host931/참가자918·917행을 보존했다. 마지막 유효 결과는3-02 전원탈락이며 성공으로 복구하지 않았다.
- Windows41/6008은 비정상 종료 기록이며 원인 확정은 아니다. RAM 여유가 적어 다음 다인 시험은 Editor 종료 후 실행한다. 상세는 log와 History에 보존했다.

## 남은 확인

- 최신 DEV 빌드의3·4인 입구→성공 엔딩, 중간 사망 뒤 생존자 실제 탈출, 협동 반출교12→14 교대.
- 분기별 같은 기준의 시간·대기·추격 압박·전환 수 비교, 새 봉 전체 두 자리/부상/목마/사망 조합.
- 첫 대포·봉 체류와 새 분기의 실제 등장·벽 파괴·공개 추격 압박, 입구 역주행 시 물리적 복귀.
- 작업장 운반 충돌·회피와 배선 작업 중 포획/생존, 모든 경로의 실제 가독성·조명·발판 구별.
- 최신 평균 어그로는 미확정. 이전 N=2 평균은 역사 기록이며 새 빌드/구간/실패 실행과 섞지 않는다. 대상 ID 갱신과 새 공격 재개를 별도로 측정한다.

- 복구 후 검사: 가이드 완료 전제를 보완한 문 EditMode 테스트만 수정, 런타임 규칙 유지. check-work-recovery-corrected.txt exit0/모든단계PASS/common712/712. 최신DEV scripts 빌드 errors0/warnings25/73.860초.
- 4인 `labyrinth-parity-full-4-02`는 체크포인트·준비·재시작 없이 전원탈락으로 끝났다. main/inspection 첫 대포→컨베이어는26.241/33.585초. 양쪽 정상 하차, 후행자 실제 트램펄린 재사용, 새 봉을 확인했다. 선행자들 포획 후 입구 actor3가 같은 세션에서 출발해414.325초 작업장 도착,15/24 절단했다. 중앙에 멈춘 보스 가까운 사격점을 pilot이 반복 거부하고 바닥 경계에서 회피 후보를 소진했다. 이 실행에는 carryBlocked가 없었으며 게임 진행불가 확정이 아니다. 엔딩은 EscapeAllRetired.
- 움직이는 추격자가 운반 중 절단기에 침입하면 작은 이탈 이동도 차단되는 코드 경로를 수정했다. FollowCarrier에 기존 공통 overlap 이탈 옵션을 연결하고 초기 정렬은 엄격하게 유지한다. 부분 이탈/깊어짐/새 벽/초기 정렬 회귀4개 포함 common716/716, check-work exit0. 읽기 전용 실제 camera/combat 관측 추가 뒤 컴파일과 scripts 빌드도 PASS(errors0/warnings25/62.757초). 게임 설정과 공통 helper는 변경하지 않았다.
- `labyrinth-parity-full-3-03`: 입구부터 host가 작업장 도착,2발/4배선 뒤234.327초 전원탈락. 첫 발은 보스 전투 전, 두 번째 발의2개 절단은 전투 진입 후다. pilot의 소수점 거리 정수 반올림으로 안전 후퇴를 전부 거부하는 문제를 찾아 교정했다. 첫 대포 동시 접근 시 탑승 재시도 누락도 local pilot에서 보완했다.
- `labyrinth-parity-full-4-03`: 세 runner 중 두 명이 반출교에서 포획된 뒤 actor3가 작업장까지 진행했다.1발/2배선 뒤295.309초 전원탈락. 회피 목표를 모서리에 잡아 다음 경고에서 탈출 여유가 없었으며 carryBlocked=false였다. 정수 반올림과 별개인 pilot 후보 선택 문제로 기록한다. 현재 경계3m 여유·8~12m 간격·측면 회피와 즉시 Walk로 재시험한다. 자세한 표본/타깃 전환은 [복구 후 실기 기록](History/005-restart-recovery.md)에 보존했다.
- `labyrinth-parity-full-3-04`: 첫 대포 두 runner 진행 뒤 inspection runner가 봉17 대신 실제 relay apron에 먼저 착지해 pilot이 중단됐다. 현 위치에서 정상 입력으로 재개했으며 setup/restart 없이 마지막 actor1이 작업장까지 진행,4/24 후405.726초 전원탈락. 긴 진단 대기로 시간·난이도 비교에서 제외한다. 마지막 회피의 불필요한 거리 증가 강제는 오프라인으로 교정했으나 실제 플레이 NOT RUN이다. 3·4인 성공 엔딩은 여전히 미완료다.
