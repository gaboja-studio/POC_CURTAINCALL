# PR356 이후 행동 전환 버그 검사

- 기준: 2026-11-13, 최신 DEV `9a5baaa7`(PR356·357·358 반영), `fix/common-action-audit`.
- 원래 `feat/exit-route/pursuer`의 저장된 작업 `27a7a1f8` 보존. Unity 정상 종료 후 DEV fetch, 최신 DEV에서 검사 브랜치 생성.
- 최초 요청은 검사였으며 아래 1·2절은 수정 전 재현 기록. 후속 사용자 요청으로 하단 수정 반영. 탈출로 제외, 실제 플레이는 사용자 담당.
- 방법: 기존 EditMode 검사와 임시 객체에서 실제 상태 전환 함수 호출. 실제 맵 플레이·패킷 지연·다른 PC 화면 재현과 구분한다.

## 1. 리허설 종료의 트램펄린 자동 반환이 치료 입장을 막음

- 장소/조합: 대기실, 트램펄린을 들고 리허설 시작 → 종료 후 원래 자리로 복귀 → 자동 반환보다 치료 입장이 먼저 승인됨.
- 전제: 원래 운반 위치가 치료 입장 범위에 있고, 호스트가 아직 참가자의 실내 이동 좌표를 받지 않은 순서. 자동 반환의 원래 위치 1m 조건을 충족해야 한다.
- 기대: 치료가 이동을 점유한 동안 자동 반환도 일반 잡기와 같이 거부하거나 보류.
- 실제: 일반 잡기는 거부되지만 자동 반환은 승인. 치료 중인 플레이어에게 트램펄린이 다시 붙어 치료실 안으로 이동하지 못함.
- 원인: `HumanCannonTrampoline.ServerRestoreRehearsalCarrier`는 `HasOccupiedHands`/치료 상태를 확인하지 않음. `ExpertRoomView.ReconcileLocalOccupancy`는 운반 소유가 남아 있으면 입장을 기다림.
- 재현: 실제 `ReserveCarrierForRehearsal → ExpertService.Enter → RestoreReservedCarrier → ReconcileLocalOccupancy` 순서 3/3.
- 관측: entered=true, normalGrab=false, restored=true, treating=true, localInside=false. 입장 처리를 3회 더 호출해도 그대로 유지.
- 회복 대조: 운반 소유를 해제한 뒤 입장 처리 시 localInside=true. 영구 입력 잠금/완전한 진행 불가로 단정하지 않음. 실제 F 입력의 회복까지 플레이한 것은 아님.
- 관련 코드: `Assets/Scripts/WaitingRoom/PassageGimmicks/WaitingRoomStageDevices.cs:96`, `Assets/Scripts/MainStage/HumanCannon/HumanCannonTrampoline.cs:413`, `Assets/Scripts/WaitingRoom/ExpertService/ExpertRoomView.cs:161`.
- 수정 검토: 자동 반환에도 기존 공통 운반 자격을 확인하고 실패 시 예약을 안전하게 유지/해제. 치료 종료 뒤 정상 반환도 회귀 검사할 것. 이번에는 미수정.

## 2. 대기실 포획 직후 재연습 시작 시 포획 자세·조작 잠금 잔류

- 장소/조합: 밤의 대기실에서 추격자에게 포획 → 아직 은퇴 처리 전인 상태에서 외줄/인간대포 재연습 시작.
- 외줄 경로: 이미 전원이 찬성한 투표를 호스트가 처리하기 전에 포획됨. 포획 중에도 PlayerState는 Normal이므로 투표 인원에서 빠지지 않고 재연습 시작이 승인됨.
- 인간대포 경로: 다른 참가자가 재연습을 시작할 때 포획된 참가자도 Normal 상태로 시작 위치 배치 대상에 남음.
- 기대: 포획과 재연습이 동시에 이동/외형/조작을 소유하지 않아야 함. 포획 중 시작 거부인지 전환 시 포획 해제인지는 후속 수정 시 정할 부분.
- 실제: 몸의 이동 좌표는 리허설 위치로 변경되지만 외형은 포획된 위치에 붙잡혀 있고 이동 입력이 잠김. 포획 모션이 리허설 이동 이후에도 계속 덮어씀.
- 원인: 시작 자격·외줄 투표 확정·배치 경로에 포획 상태 검사가 없고, `PursuitVictimPose.LateUpdate`는 기존 위치/입력 잠금을 유지함.
- 재현: 임시 포획 상태 + 실제 캐릭터 모델에서 외줄 Relocate/대포 PlaceLocalAtCheckpoint, 이후 LateUpdate 3회. 각 경로 3/3 동일. 외줄은 2인 찬성 목록을 실제 HostTickVotes에 전달하여 practice=true 확인.
- 관측: ropeStart=true, cannonStart=true, captured=true, state=Normal, control=false, inputBlocked=true. 외줄 예시에서 이동 좌표를 20m 옮겨도 외형은 원래 포획 위치 유지.
- 한계: 추격자의 실제 NavMesh 접촉부터 플레이한 것이 아니라 유효한 포획 상태를 주입한 조합 검사. 포획 종료 이후 자동 복구 여부와 실제 발생 빈도는 이번에 검증하지 않음.
- 관련 코드: `Assets/Scripts/WaitingRoom/Rehearsal/WaitingRoomRehearsal.Votes.cs:88`, `Assets/Scripts/WaitingRoom/Rehearsal/WaitingRoomCannonRehearsal.cs:69`, 같은 파일 `:375`, `Assets/Scripts/WaitingRoom/Rehearsal/WaitingRoomRehearsal.Local.cs:504`, `Assets/Scripts/WaitingRoom/Pursuit/PursuitVictimPose.cs:76`.
- 최신 DEV에 들어온 대기실 추격과 기존 재연습의 연결 문제. 통일화만의 원인이라고 단정하지 않음. 이번에는 미수정.

## 검사 결과와 남은 범위

| 검사 | 결과 |
| --- | --- |
| 기존 CommonProgressionRegression | 686/686 통과, 실패/생략 0 |
| 치료 입장 + 트램펄린 자동 반환 | 새 결함 3/3 재현, 운반 해제 뒤 입장 회복 3/3 |
| 포획 + 외줄/대포 재연습 배치 | 새 결함 각각 3/3 재현 |
| 목마 위 탑승자 + 트램펄린 자동 반환 | 이번 임시 배치에서는 3/3 반환 거부. 결함으로 집계하지 않음, 모든 배치 정상 보장 아님 |
| 실제 다른 PC 2~4인 플레이/패킷 순서 | 미실시, 사용자 확인 |
| 기존 보류 14개·탈출로 | 이번 조사 제외 |

- 686개 통과는 위 두 조합의 정상 동작을 보장하지 않는다. 두 결함은 기존 검사에 포함되지 않은 경로에서 확인했다.
- 원본 결과: `.harness/local/nov13-post356-common-results.json`, `.harness/local/nov13-post356-combined-results.json`.
- 임시 재현 코드: `.harness/local/nov13-post356-probe.cs`, `Post356Probe.Run`. Assets에 검사 스크립트 추가 없음.
- 도구 준비 중 EditMode의 DontDestroyOnLoad 제약 발생. 임시 HUD 인스턴스를 준비하여 해소 후 재현 완료했으며 게임 버그로 집계하지 않음.
- 최종 check-work: 안전성·지식 검사 통과. 기존 DEV의 Task115 log154줄/제한130줄 때문에 문서 검사 실패, 해당 단계 컴파일 생략. 별도 실행한 공통686검사 결과와 구분한다. 원래 열린 씬 dirty=false, Assets/Packages/ProjectSettings 변경 없음.

## 후속 수정 — 사용자 승인 후

| 장소·기능 | 변경 전 | 변경 후 |
| --- | --- | --- |
| 대기실·트램펄린 자동 반환 | 치료 중 일반 잡기 거부 / 자동 반환 승인 | 자동 반환과 일반 잡기의 자격 판정 공유 / 치료 중·퇴실 중 반환 보류 |
| 대기실·자동 반환 재개 | 다른 조작의 점유를 놓칠 수 있음 | 목마 위·포획·기물 점유·연습 배치 조건 확인 / 조건 해제 시 기존 예약 시간·위치 안에서 반환 / 만료 후 재부착 없음 |
| 외줄·인간대포 재연습 시작 | 포획 직후 Normal 상태를 일반 참가자로 처리 | 아직 은퇴하지 않은 포획 참가자가 있으면 시작 보류 / 은퇴·연결 해제 후 남은 팀 진행 가능 |
| 리허설 배치·복귀점 저장 | 포획 위치·모션과 리허설 이동이 중복 적용 | 포획 중 배치와 복귀점 저장 차단 / 기존 포획 처리는 유지 |
| 외줄 재연습 투표 | 확정 다음 프레임의 점유 변화 누락 / 전원 은퇴 후 빈 투표 잔류 가능 | 활성화 직전 재검사 / 투표 인원이 0이면 정리하여 밤 진행 차단 방지 |

- 신규22/22 및 이를 포함한 공통708/708 통과, 실패/생략0. 컴파일·공통 GameSettings 연결 검사 통과. 2/3/4인 합성 참가자 목록을 사용했으며 실제 멀티플레이는 미실시.
- 기존 스크립트 수정, 새 런타임 스크립트·세팅 에셋·씬별 규칙 없음. 기존 반환 유예시간/거리, 포획·부상 수치 유지.
- 제출 검사에 걸린 Task115 과거 기록은 원문 그대로 History/004로 이동하고 링크 유지. 게임 내용 변경 없음.
