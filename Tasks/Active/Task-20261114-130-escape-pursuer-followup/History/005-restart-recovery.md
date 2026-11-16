# 2026-11-16 강제 종료 복구와 후속 연속 실기

강제 종료 복구부터 `labyrinth-parity-full-3-03`·`labyrinth-parity-full-4-03`·`labyrinth-parity-full-3-04` 종료까지의 기록이다. 최신 실행들은 최종 작업장에 도달했으나 **성공 엔딩은 미완료**다. 자동 입력 pilot의 중단을 공통 게임 진행 불가로 확대하지 않는다. 이전 실기 원문은 [004-labyrinth-play-before-recovery.md](004-labyrinth-play-before-recovery.md)에 보존돼 있다.

## 복구와 보존 증거

- Unity·시험 플레이어 종료 확인 뒤 `origin/dev`를 fetch하고 `10633c71→0f95ba68` fast-forward했다. upstream 14개 파일과 작업 변경의 중복·병합 충돌은 없었다. Git connectivity 검사 exit 0이며 기존 dangling 객체 알림만 있었다.
- `.harness/local/pursuer-remake-planning/restart-recovery-20261116/`에 변경 파일 374개 ZIP, SHA256 목록, tracked patch를 보존했다. ZIP CRC 오류 0, 0바이트 변경 파일 0, 동기화 전후 374개 해시 일치, 텍스트 343개 NUL 손상 0이었다. 원본 PursuitRemodel SHA256 `9556701bdc3e8f88b246c38b41f8253c22e3804aa7e33fb5d3f113cb00190814`도 유지됐다.
- `Temp/__Backupscenes/0.backup`을 별도 보존하고 복구 씬을 `RecoveredLabyrinthReview.unity`로 비교했다. 저장된 Prototype과 복사 경로에 따른 `GlobalObjectIdHash` 1곳 외 차이가 없었다. 저장본을 덮어쓰지 않았으며 두 씬 모두 missing scripts 0, 공통 봉 배열 25, 외줄 배열 20이었다.
- Windows Kernel-Power 41/6008은 비정상 종료 기록이며 원인을 확정하지 않는다. 재시작 직후 RAM free 0.5GB/15.9GB였고 import worker 임시 조정 뒤 3.26GB가 됐다. 작업 후 worker 3으로 복원했으며 후속 다인 standalone 시험은 Editor 종료 후 진행했다.
- `labyrinth-parity-full-3-02`의 마지막 state 3개·임시 state와 timeline 꼬리에 NUL 손상이 있었다. 원본을 바꾸지 않고 복구 폴더의 `review-log-salvage/`에 유효 host 931행·참가자 918/917행을 보존했다. 보존본 파싱 실패 0, 마지막 유효 host 343.820초 결과는 `EscapeAllRetired`였다. 실패 결과를 성공으로 복구하지 않았다.
- 후속 빌드 뒤 보존 목록374개를 다시 비교했을 때 기존 파일 중 변경은 Review 소스·QA·log와 ProjectSettings뿐이었다(추가 수정 파일은 기존 목록에 없을 수 있다). ProjectSettings의 차이는 Unity Behavior 패키지 `Behavior App UI Settings` GUID `99f9c9493070a9d4c979a8fec7c5a8d3`의 preload 참조 한 줄 삭제다. `com.unity.dt.app-ui/Editor/AppUIBuildSetup.cs`의 OnPostprocessBuild→Revert가 AppUISettings를 preload에서 제거하는 정상 빌드 후처리이며 현재 ProjectSettings는 Git 기준과 일치한다. 사용자 게임 설정 손실로 판단하지 않는다.

## 수정과 검사·빌드

| 단계 | 결과와 한계 | 근거 |
| --- | --- | --- |
| 복구 직후 검사 | 첫 시도는 Task 기록 길이 HARD 초과로 문서/규칙 FAIL, 컴파일·테스트 SKIPPED. History 보존 뒤 재시도 | 기존 `log.md` 복구 기록 |
| 최신 DEV 공통 검사 | common 711/712. `CannonPracticeDoorUsesVisibleBarsWithoutAnInvisibleBarrier`가 첫 튜토리얼 잠금 중 해제를 기대하던 테스트 전제 문제였다. 가이드 완료 상태를 만드는 테스트만 수정하고 런타임 잠금은 유지 | `check-work-recovery-corrected.txt`: exit 0, 안전성·문서/규칙·컴파일·공통설정 PASS, common 712/712 |
| 복구 scripts 빌드 | Succeeded, errors 0, warnings 25, 73.860초 | 캠페인 폴더 `labyrinth-recovery-build-result.json` |
| 절단기 운반 수정 | `EscapeExitSaw.FollowCarrier`에서 기존 공통 `HumanCannonCarry.CanMove`의 `allowExistingOverlap: !align`을 사용. 동적 물체가 들어온 기존 겹침에서 작은 이탈을 허용하며 초기 정렬·새 벽 충돌 차단은 유지. 공통 helper·GameSettings 변경 없음 | `ExitSawTests`의 실제 FollowCarrier 회귀 4개: 2cm 부분 이탈, 겹침 깊어짐 차단, 다른 벽 통과 차단, 초기 정렬 엄격 유지 |
| 운반 수정 검사 | exit 0, 안전성·문서/규칙·컴파일·공통설정 PASS, common **716/716**, failed/skipped 0. 위 신규 4개 포함 | 복구 폴더 `check-work-saw-overlap.txt` |
| 후속 진단 | Review의 읽기 전용 camera/combat 관측 추가 뒤 컴파일 PASS. 이 추가 뒤 공통 716개를 다시 실행했다는 뜻은 아님 | root의 컴파일 실행 확인 |
| 후속 scripts 빌드 | **Succeeded, errors 0, warnings 25, 62.757초** | `.harness/local/pursuer-campaign-20261115/labyrinth-saw-overlap-build-result.json` |

검사는 구현 회귀의 근거이며 성공 엔딩·난이도 동등성의 근거는 아니다. 이 문서 작성 중 Unity·검사·빌드를 재실행하지 않았다.

## 복구 직후 실행에서 남은 문제

- `labyrinth-parity-full-3-02`: 정상 컨베이어 F 하차, 하차장 외줄, 트램펄린 운반/후행 재사용, 새 열 회수·상부 봉을 관측했다. 후행자가 이미 놓인 트램펄린에서 반동했는데 pilot이 지상 상태만 기다려 멈췄고 일반 입력으로 재개했다. 이후 pilot은 마지막 틈에서 실제 bounce counter 증가와 비행도 허용하도록 교정했다. setup/restart 없이 actor1이 300.200초 작업장에 도달해 15/24 절단 후 포획됐다.
- 같은 3-02 작업장에서 `carryBlocked`와 무변위를 관측했고 추격자는 약 1.56m 옆에 있었다. 충돌체 ID 증거는 없어 그 실기의 정확한 충돌 원인은 미확정이다. 별도 소스 점검으로 기존 겹침 이탈 누락을 확인해 위 FollowCarrier 수정·회귀를 추가했다.
- `labyrinth-parity-full-4-02`: main/inspection의 정상 F 하차, 트램펄린 재사용, 새 봉과 선행자 탈락 뒤 남은 입구 생존자 재추적을 관측했다. actor3가 414.325초 작업장에 도달해 15/24 절단했지만 pilot이 중앙 보스 근접 사격점과 경계 후퇴 후보를 처리하지 못했다. `carryBlocked`는 없었으며 결과는 전원 탈락이었다.

## 3-03: 입구부터 작업장까지 진행, 2발·4/24 뒤 전원 탈락

증거 폴더는 `.harness/local/pursuer-campaign-20261115/labyrinth-parity-full-3-03/`이다. 슬롯 매핑은 `0→actor0`, `1→actor2`, `2→actor1`이며 슬롯 번호를 actor ID로 대체하지 않는다.

- 첫 대포 Tap 명령의 최초 표본은 slot1 25.975초, host 26.350초였다. host는 탑승했으나 slot1은 `first cannon seated` 타임아웃 뒤 탑승 재시도 없이 중단했다. near/available만 확인한 pilot의 동시 접근·재시도 부족으로 구분하며 공통 대포 버그로 확정하지 않는다.
- host0는 입구부터 기존 봉 0→1→2, 컨베이어, 신규 열 회수 봉 18→19→20→21, 상부 봉 22→23→24, 반출 구간을 지나 **209.037초 작업장**, **211.912초 톱 운반**에 도달했다. 다른 두 actor는 이미 탈락한 상태였다.
- `shots-0.jsonl`: 첫 발 214.913초 FIRED→215.116초 CABLE_CUT 2건. 215.141초 timeline에서도 `combat=false`였다. 219.067초 `combat=true` 확인 후 둘째 발 219.967초 FIRED→220.167초 CABLE_CUT 2건. **전체는 2발·4절단, 전투 진입 뒤 확인한 절단은 둘째 발의 2개**다.
- 회피 로그의 `carryBlocked=false`와 pilot의 `No supported retreat on a different unblocked axis` 종료를 확인했다. local pilot의 정수 반올림 문제는 후속 실행 전에 교정했다. 이를 운반 겹침 수정의 실패나 공통 게임 진행 불가로 단정하지 않는다.
- actor0는 226.702초 포획, **234.327초 탈락과 동시에 `EscapeAllRetired`**. 마지막 기록은 265.325초이며 `cuts=4/24`, `exitOpen=false`다.

공격 중 유효한 `attackActor`가 있으면 우선하고 나머지는 `target`을 사용하는 effective focus를 표본 간격으로 누적했다. Normal은 경고 전 대상 선정도 포함하며, Captured 포함 열은 `Normal + EscapeCaptured` 상태만 더한다. 단일 실행 누적값으로 일반 평균이나 다른 실행과 합산하지 않는다.

| Actor | 첫 대상 지정 공격 | 포획 | 탈락 | Normal focus | Captured 포함 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 22.407 | 23.135 | 36.879 | 7.203초 | 20.947초 |
| 2 | 37.604 | 38.704 | 45.955 | 1.100초 | 8.351초 |
| 0 | 61.584 | 226.702 | 234.327 | 180.747초 | 188.372초 |

actor1 탈락 뒤 actor2의 다음 공격은 37.604초(0.725초 뒤), actor2 탈락 뒤 host0 첫 공격은 61.584초(15.629초 뒤)였다. host0의 다음 공격은 139.505초로 77.921초 공백이 있었지만, 그 사이 추격자는 gate3와 서비스 경로를 계속 이동했다. 입구·초반 구간 관측 범위에서 장기 위치 정체는 확인하지 않았다.

## 4-03: 마지막 회피가 모서리에서 중단, 1발·2/24 뒤 전원 탈락

증거 폴더는 `.harness/local/pursuer-campaign-20261115/labyrinth-parity-full-4-03/`이다. 이번 실행의 슬롯과 actor ID는 각각 일치했다.

- 마지막 runner actor3의 정상 사용 입력은 slot3 `shots-3.jsonl`, 권위 발사·절단은 host `shots-0.jsonl`에 기록됐다. **278.496초 FIRED 1건→278.721초 CABLE_CUT 2건**, 최종 모든 saw의 공유 진행은 `cuts=2/24`다.
- `exit-aware-aim-slot-3.jsonl`: 283.627초 DebrisWarning에서 바닥 모서리 목표 `(-572, 26.3)`을 선택했다. 285.424초 새 Warning 때 실제 발 위치는 `(-572.212, 25.080, 26.535)`이고 `carryBlocked=false`였다. 이후 다른 축의 지지된 후퇴 지점을 찾지 못해 pilot이 종료했다. **이미 교정한 정수 반올림과 별개인 모서리 목표 선택·후속 회피 후보 부족**으로 기록한다.
- host timeline은 285.475초 Warning, 286.907초 다음 Warning, **287.632초 actor3 포획→295.309초 탈락**을 기록했다. `EscapeAllRetired` 최초 표본도 **295.309초**이며 `exitOpen=false`다.

| 탈락 actor | 탈락 시각 | 이후 실제 focus | 다음 대상 지정 공격 | 탈락 후 지연 |
| --- | ---: | --- | --- | ---: |
| 1 | 41.157 | 같은 표본에서 actor2 | 42.567 DebrisWarning | 1.410초 |
| 0 | 205.615 | 같은 표본에서 actor2 | 205.990 DebrisWarning | 0.375초 |
| 2 | 232.540 | 같은 표본에서 actor3 | 254.273 DebrisWarning | 21.733초 |
| 3 | 295.309 | 대상 없음 | 없음 | — |

actor0·2 포획 시 raw `target`은 다음 주자로 먼저 바뀌지만 `attackActor`는 기존 포획 대상을 유지했다. 표의 실제 focus는 운반 종료를 포함해 구분한 값이다.

## 증거 범위와 남은 확인

### 3-04 추가 실행

- 두 runner를 입구에서 함께 출발시켰다. 첫 대포의 일반 Tap 재시도 후 양쪽 진행은 관측했으나 inspection runner는 봉16에서17을 잡지 않고 relay apron의 지지된 발판에 먼저 착지해 pilot의 Catch17 대기가 실패했다. 실제 착지 위치에서 정상 Walk/탑승 입력으로 재개했으며 체크포인트·준비 이동·재시작은 사용하지 않았다. 재개 중 대포 중심 좌표까지 걸어가려던 Go도 충돌로 목표에 못 닿아, 이미 boardable인 위치에서 정상 Tap으로 탔다. 이 진단 대기 때문에 경로 시간·난이도 비교에 사용하지 않는다.
- actor2는37.754초, 선행 actor0는209.365초 탈락했다. 남은 actor1은 기존 트램펄린을 재사용하고 열 회수·상부 봉·혼자 남았을 때의 반출교를 지나 최종 작업장까지 진행했다. 최종4/24 절단 후405.726초 전원탈락했다. host1339표본 전부continuous=true/restarts0이다. 기록은 `.harness/local/pursuer-campaign-20261115/labyrinth-parity-full-3-04/`에 보존한다.
- room pilot은 경계 여유3m와 즉시 Walk로 여러 공격을 피했지만396.829초 후퇴 후보를 소진했다. 당시 carryBlocked=false. 기존적거리7.4398m에서 끝점8.4398m 이상을 매번 강제한 것이 합법적인 측면 회피를 배제했다. 이 추가 거리 증가 강제만 제거한 오프라인 재계산은 목표(-575,50.3), 적 최소5.5214m, 경계3.5m를 선택했고 기존 몸통3.4m·공격점·바닥 검사는 유지됐다. **이 마지막 local pilot 수정의 실제 플레이는 NOT RUN**이다. 게임 코드/설정을 완화한 것이 아니며 성공 완주로 간주하지 않는다.

### 공통 제한

- 두 실행 모두 모든 클라이언트 전 표본에서 `continuous=true`, `restarts=0`, 클라이언트별 단일 PID였다. `actions-N.jsonl`에 setup·damage·checkpoint·restart 명령은 없다. Review의 `capture` 명령은 화면 캡처이며 플레이어 강제 포획이 아니다. 이 확인은 남은 기록 범위에 한정한다.
- 3-03 host 718행, 파싱 실패 0, 표본 중앙값 0.374초/15초 이후 최대 1.152초. 4-03 host 909행, 파싱 실패 0, 중앙값 0.358초/15초 이후 최대 1.292초다. 시작 구간 최대 간격은 각각 4.393/4.317초다. 표의 시각은 최초 관측값이며 동일 표본 전환을 정확히 동시 발생했다고 단정하지 않는다.
- 정상 입력에 자동 균형·발사 타이밍·회피 보조가 포함되므로 사람의 조작 난이도·분기 동등성 평가와 구분한다. 실패한 연속 실행과 준비 위치 fixture 성공을 합쳐 입구부터 성공 엔딩으로 표기하지 않는다.
- 최신 3·4인 성공 엔딩, 여러 주자의 첫 대포 재시도, 작업장 모서리에서의 연속 회피·배선 작업, 모든 선택 경로의 난이도·기믹 밀도 동등성은 남아 있다. FollowCarrier 4개 단위 회귀 PASS와 실제 작업장 생존 성공은 별도 검증이다.
