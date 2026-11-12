# PR355 이후 행동 전환 추가 감사

- 기준: 2026-11-13, 작업본 9ed54d30(PR355 수정 포함), origin/dev 1764a21c. 시작 전 Unity 종료·DEV fetch/반영 완료, 추가 병합 변경 없음.
- 요청: 목마·운반·절단 상태의 구역 이동 및 리허설 시작/종료 추가 조사. 실제 플레이는 사용자 담당.
- 최초 감사: 새 결함 2건 확인. 아래 재현 기록은 수정 전 상태이며, 후속 사용자 요청에 따라 두 건 수정 완료(하단 전후 기록).
- 검사 방식: EditMode에서 임시 객체와 실제 상태 전환 함수를 호출. 실제 네트워크 패킷 지연·물리 프레임·다른 PC 화면을 검증한 것은 아님.

## 1. 대포 리허설 시작 배치 전에 트램펄린을 잡으면 배치와 놓기가 서로 대기

- 장소/조합: 대기실 → 인간대포 재연습, 시작 직전 잡기 요청과 코스 배치가 겹침.
- 전제: 새 리허설 트램펄린의 잡기 범위 안에 있는 참가자. 호스트가 아직 참가자를 시작대에 배치하지 않은 상태에서 Grab 요청을 처리.
- 확인: 대포 좌석은 IsPracticeReady(actor)를 검사하지만 트램펄린 CanGrab은 참가자의 배치 완료 여부를 검사하지 않음. PracticeGrabAllowed는 운반 복원 중인지만 확인.
- 증상: 잡기가 승인됨 → LocalTick은 Held가 없어질 때까지 배치를 보류 → TickUi는 배치 전 입력 잠금 → RequestDrop도 InputGate 때문에 거부. 시작대로 이동하지 못하고 F로 놓지도 못함.
- 재현: 정상 상태 3/3 반복. 추가로 2·3·4인 참가자 목록 × 사지 손실 0·1·2·3개 대표 조합 = 12/12 동일 결과.
- 관측값: couldGrabBeforePlacement=true, placementBlocked=true, heldAfterDrop=true, placedRound=-1. LocalTick/TickUi를 추가 3회 호출해도 유지.
- 한계: 임시 배치와 실제 공통 운반 충돌 Mesh를 사용. 작성된 씬의 특정 위치에서 지연 패킷이 도착하는 빈도는 미검증. 종료/스킵 투표 등 별도 탈출 경로까지 모두 막힌다는 뜻은 아님.
- 관련: HumanCannonTrampoline.CanGrab/HandleRequest, HumanCannonTrampoline.Network.SendRequest, WaitingRoomCannonRehearsal.EnsureCourse/LocalTick, WaitingRoomCannonRehearsal.Ui.TickUi.
- 수정 방향: 기존 공통 운반 요청 처리에서 리허설 참가자의 배치 상태를 검증. 공통 수치나 씬별 예외를 추가할 필요 없음.

## 2. 잠든 상태로 외줄 재연습 시작 시 침대 입력 잠금·시점·누운 외형 잔류

- 장소/조합: 밤의 대기실 침대 + 외줄 재연습 시작/이동.
- 확인: 수면 중 CanProposePractice=true. NightBed.CanSleep은 대포 리허설만 확인하여 외줄 연습 중에도 true.
- 증상: ActivateLocal과 Relocate로 몸의 이동 좌표는 코스로 옮겨도 localReady/RestingBed가 유지. BedRestView는 침대 시점과 UIInputBlocker를 유지하고 NetworkBedPose는 외형을 침대 위치에 다시 놓고 누운 자세를 유지.
- 재현: 실제 이동·BedRestView.Update·NetworkBedPose.LateUpdate 호출 3/3. 추가 제안 자격 검사에서도 수면 중 true 확인.
- 관측값: 플레이어 발 좌표 (9100,9000,9000), 외형 좌표 (0,0.58,-0.75), resting/lying/inputBlocked=true, RestingBed=0.
- 회복 가능성: 기존 침대의 F/Esc 기상 입력으로 풀릴 수 있음. 영구적으로 모든 입력이 막힌다고 표현하지 않음.
- 대조: 인간대포 EnsureCourse에는 밤의 SetReady(false)가 있어 같은 결함으로 집계하지 않음.
- 관련: WaitingRoomRehearsal.Votes.CanProposePractice, WaitingRoomRehearsal.Local.ActivateLocal/Relocate, NightBed.CanSleep, BedRestView.Update, NetworkBedPose.LateUpdate.
- 수정 방향: 기존 리허설 전환에서 수면 준비·침대 시점/외형 점유를 함께 정리하고 늦은 수면 요청도 확인. 새 게임 규칙을 만들 문제가 아님.

## 검사한 조합과 한계

| 범위 | 이번 결과 |
| --- | --- |
| 목마 2·3·4인 상태의 대포 연습 종료, 연결 해제/복귀 수신 순서, 사망과 종료 투표 중첩 | 기존 RehearsalLifecycleTests 70/70 통과 |
| 외줄 진입 방향·옆줄 이동·바닥 복귀·부상/추락 판정 | 기존 TightropeSurfaceControlTests 140/140 통과. 테스트 묶음의 기존 탈출로 사례도 실행됐지만 탈출로 조사/수정은 진행하지 않음 |
| 운반 중 점프·벽/천장·회전 충돌·사지 조각 | 기존 TrampolineCarryPoseTests 14/14 통과 |
| 수면 중 사망·소유자 교체·외형 교체 | 기존 BedRestOwnershipRecoveryTests 7/7 통과. 위 리허설 동시 전환은 기존 검사에 없음 |
| 튜토리얼 첫 배치·아침 복귀 순서·리허설 체크포인트와 로비 복귀 충돌 | 기존 WaitingRoomSpawnRecoveryTests 6/6 통과 |
| 치료/운반 상호 점유 | PR355의 기존 보호 처리 재검토. 이번 새 결함과 분리 |
| 기물 소유 복원·씬 종료·외줄 종료 | 호출 순서 및 해제 경로 검토. 추가 확정 결함으로 집계하지 않음. 실제 네트워크 순서/전체 지형은 미검증 |

- 위 관련 검사는 합계 237개. 새 결함 2건을 검사하는 임시 재현과 별도이며, 237개 통과는 새 결함이 없다는 뜻이 아님.
- 처음 리허설 검사 68/70: 임시 검사 후 파괴된 HUD singleton 참조를 기존 fixture가 object의 null 비교로 판별하지 못하여 PlayMode 전용 DontDestroyOnLoad를 호출. 파괴된 임시 참조만 정리한 뒤 소스 변경 없이 70/70 통과. 실제 게임 버그 2건과 구분.
- 임시 재현 준비 중 EditMode의 DontDestroyOnLoad·저장된 씬 이름 변경 제한 등을 보완했으며 게임 결함 수에 포함하지 않음.
- 모든 신체 부위 마스크·장비·지형·프레임/패킷 순서를 전수 검증하지 않음. 2·3·4인은 가상 참가자 목록이며 다른 PC 멀티플레이가 아님.
- 기존 전체 검사 실패18개/사용자 보류14개는 별도 유지. 이번 감사에서 전체 테스트를 재실행하거나 해당 실패를 수정하지 않음.

## 재현 자료

- `.harness/local/nov13-post355-probe.cs`: 임시 재현 코드.
- `nov13-post355-probe-results.json`: 두 결함 각각 3회 실제 함수 호출 결과.
- `nov13-post355-probe-matrix-results.json`: 참가자 수 × 절단 수 대표12조합 및 수면3회 결과.
- `nov13-post355-bed-eligibility-results.json`: 수면 중 제안 가능 여부·이동 후 침대 상태 잔류.
- `nov13-post355-{lifecycle-retest,rope,carry,bed,spawn}-results.json`: 기존 관련 검사 결과.

- 마무리 check-work: 안전성·문서/규칙·컴파일·공통 설정 연결 PASS. 자동 테스트는 위237개를 별도 실행했으므로 check-work에서는 선택하지 않음. 첫 문서 검사에서 todo83줄 한도 초과를 발견하여 원문을 History/018에 보존하고 재검사 통과. 기존 길이 경고70개/하드0개 유지.
- 종료 시 Start 씬만 열림, dirty0. 기존 Start.unity와 escape-labels.asset은 시작 전 백업과 SHA256 일치. Assets/Scripts·공통 설정·ProjectSettings diff 없음.

## 수정 후 — 2026-11-13

- 기준: PR355가 반영된 DEV 4bf66bab에서 `fix/rehearsal-entry-handoffs` 생성. 기존 사용자 씬·폰트·다른 Task 변경 보존.
1. 대포 리허설 시작/종료와 트램펄린 잡기
   - 전: 배치 전 Grab 승인 → 운반 해제를 기다리는 배치와 입력 잠금으로 막힌 Drop이 서로 대기.
   - 후: 대포 좌석과 같은 IsPracticeReady(actor)를 트램펄린 CanGrab에도 적용. 호스트의 실제 Grab 요청 및 지연 상호작용 재검사에 공통 적용.
   - 후: 배치 완료 후 정상 잡기·놓기 가능. 리허설 종료/복귀 중 늦은 Grab도 거부. 기존 운반 복원 전용 경로는 유지.
2. 수면과 리허설 이동
   - 전: 외줄 연습 시작 후 침대 준비·시점·입력·누운 외형 잔류.
   - 후: 외줄/대포 코스 시작 전에 수면 준비를 해제하고 침대 시점·입력 잠금·선택한 침대를 정리. 기존 사지 손실은 그대로 유지.
   - 후: 리허설 상태를 먼저 받은 원격 화면도 침대 외형을 복원. 침대 상태 정리가 늦어도 누운 모션을 유지하지 않음.
   - 후: 침대와 호스트 수면 요청이 같은 자격 검사를 사용. 투표/시작 대기·진행/종료 중 늦은 수면 요청 거부, 밤 전환 대기. 종료/취소 후 정상 수면 가능.
   - 후: 정상 수면에서 마지막 사람이 준비하여 즉시 Dawn으로 전환되어도 침대 시점 생성 유지.
3. 변경 범위·검사
   - 기존 런타임7파일과 기존 테스트2파일 수정. 신규 런타임 스크립트·GameSettings 수치/에셋·씬/프리팹 배치 변경 없음.
   - 새 검사68/68: 2·3·4인 × 생존 가능한 사지 마스크15종의 늦은 잡기/정상 배치/놓기/종료45, 수면 이동/정리 순서×외줄/대포×절단 수16, 원격 침대 정리 수신 순서2, 늦은 호스트 수면/밤 전환/종료 복구4, 마지막 수면자의 Dawn 시점1.
   - 첫 준비 오류: 테스트 특성 Range/BodyPart 이름 충돌을 한정명으로 정리. 숨김 임시 객체가 Unity 검색/물리에서 제외되어 발생한53실패를 실제 씬 조건의 임시 객체로 수정한 뒤68/68 통과. 도메인 재로드 중 빈 컴파일 실패 응답은 완료 상태 재확인 후 통과.
   - 실제 플레이·다른 PC 멀티플레이·연출 체감은 사용자 확인. 기존 보류 실패14개는 이번 수정에 포함하지 않음.
- 최종 check-work -CommonRegression: 안전성·문서/규칙·컴파일·GameSettings 연결·공통686/686 PASS, 실패/생략0. 신규68개는 공통686개에 포함. 결과: `.harness/local/nov13-entry-fix-final-check.log`, `nov13-entry-fix-common-results.json`, `nov13-entry-fix-focused-results.json`.
