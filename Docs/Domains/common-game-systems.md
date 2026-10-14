# 공통 게임 시스템

> Task-20261013-029(CC-107) 기존 기능 조사 결과. 코드 기준 `feat/feature-reuse-audit`(2026-10-13), 기획 기준 Confluence 최신판(게임 흐름 15564804·부상 14254330·장소 10454669·조작 11633076, 2026-10-13).
> "근거"는 코드에서 확인한 사실, "기획"은 [게임 흐름](../../Harness/Project/Facts/game-flow.md)·[런 상태](../../Harness/Project/Facts/run-state.md)·[부상 규칙](../../Harness/Project/Decisions/injury-rules.md)·[조작키](../../Harness/Project/Decisions/control-keys.md) 요약이다.

## 목적

런 진행·부상/생존·공연 결과·공동 자금·장소 전환처럼 여러 공간이 함께 쓰는 게임 시스템의 지도. 기획상 담당은 시스템(@MoHoDu): 런 상태 관리, 장소 전환, 공간 간 규칙, 잃은 팔다리 수 관리.

## 재사용 목록

분류: 재사용 / 수정 후 재사용 / 신규 / 범위 밖(공간 담당 또는 이후)

### 런 상태·세션 (CC-108)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 방 접속·나가기·정원·진행 중 참가 거절 | 재사용 | `Assets/Scripts/Systems/Network/Session/NetworkSessionManager.cs` | 유일한 공개 진입점, 접속 승인 콜백 소유. 정원 4·최소 1(테스트). 기획 2~4인 → 최소 인원은 세팅값으로 조정 |
| 방 상태 `Waiting/Playing/Ended` | 재사용(의미 한정) | `GameSessionState.cs`, `GameSessionStateSync.cs` | 호스트만 변경. 로비 → 런 진행 → 런 종료 단위로만 쓰고, 공연 종료 신호로 쓰지 않는다(하루 공연 2회) |
| `RestartEndedGame()` | 수정 후 재사용 | `NetworkSessionManager.cs:291` | `internal`, 호출처는 `TightropeRun.cs:117`뿐. 기획은 공연 후 재도전이 없으므로 세대 교체(런 재시작) 경계로만 남길지 정리 필요. 재접속 비허용(PM 결정 #7)은 지금의 진행 중 참가 거절과 일치 |
| 런 진행 관리자(일차·단계·쉬는 시간 타이머·밤 준비 완료) | 신규 | 없음 (planned `Assets/Scripts/Systems/Campaign/`, Task-028) | Day·쉬는 시간·밤 코드 없음. 기획 9-2 런 범위 데이터 전부 |
| 세션 범위 데이터(세대 번호·물려줄 선반·흔적) | 신규 | 없음 | 세대 코드 없음. 같은 게임 세션 안에서만 유지(기획 6-2) |
| 조정값 시작점 | 수정 후 재사용 | `Assets/Scripts/Systems/Settings/GameSettings.cs`, `BaseGameSettings.cs` | `Base`·`Tightrope`만 있음. 5묶음(공통·묘기·대기실·탐색·탈출로, PM 결정 #3)으로 확장. 런 수치(일수 3, 공연 2회/일, 쉬는 시간 5분, 소집 경보 1분, 가이드 길이, 타이머 A/B) 칸 추가 |

### 부상·생존 (CC-109)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 몸 상태(잃은 팔다리 비트) | 재사용 | `Assets/Scripts/Systems/Player/PlayerCondition.cs`, `BodyPart.cs` | 비트 플래그. 절단 순서가 고정이면 `LostCount`가 곧 기획의 "잃은 팔다리 수(0~4)" — 그대로 둬도 수와 1:1 |
| 고정 절단 순서 | 수정 후 재사용 | `PlayerCondition.NextLegThenArmCut(lost, leftFirst: true)` | `leftFirst=true`면 왼다리→오른다리→왼팔→오른팔로 기획과 일치. 그러나 `ResolveCut`(요청 부위), 톱날 호출(`TightropeSaws.cs:497`: 가로 = 닿은 쪽, 수직 = 닿은 위치), 디버그 요청(무작위 좌우)은 순서를 어김 |
| 부위 손실 동기화·사망 확정 | 수정 후 재사용 | `Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs` | `ServerCutPart`·`ServerCutLegThenArm`. 4곳 손실 → `Fallen`. 없는 것: N곳 한 번에 절단(3축·추격자), 외팔 상태로 설정(부활·잡힘), 역순 복구 |
| 플레이어 상태 `Normal/Fallen/Arrived` | 수정 후 재사용 | `PlayerState.cs` | `Fallen` = 기획의 사망(추락 포함)과 같은 뜻이라 유지 가능. 없는 것: 리타이어(탈출로), 추격 중·끌려옴. `Arrived`는 외줄 도착 전용 의미. 값은 끝에만 추가 |
| 상태 변경 허용 규칙 | 수정 후 재사용 | `NetworkPlayer.ServerCanChangeState`(`NetworkPlayer.cs:51`) | `static Func` 1개를 `TightropeRun`이 씀. 장소마다 규칙이 다르므로(공연/쉬는 시간/탈출로) 현재 장소의 규칙만 끼우는 방식 필요 |
| 재시작 시 몸 복구 | 수정 후 재사용(대체) | `NetworkPlayer.ServerRestartAll()`, `BaseGameSettings.BodyRestartRecovery` | 사망자를 온전한 몸으로 복구. 기획: 묘기 재시작 없음, 사망자는 다음 시간대에 **외팔 상태**로 부활 → 부활 함수로 대체, 복구 설정은 폐기 대상 |
| 사망 후 관전 | 재사용 | `Assets/Scripts/Systems/Camera/CameraSpectator.cs` | 내 캐릭터 사망 시 살아 있는 사람을 따라감(A/D 전환). 기획 "공연 끝까지 관전"과 일치. 추격당하는 사람 시점 관전·리타이어 후 엔딩까지 관전은 대상 지정 기능 추가 필요 |
| 손상 효과 적용기(기어가기 + 정책) | 수정 후 재사용 | `Assets/Scripts/Systems/Damage/BodyDamageEffects.cs`, `BodyPenaltyPolicy.cs` | 033에서 공통 폴더로 이전(동작 동일). 정책 교체 구조는 기획의 "탐색 중 / 공연 중 부상 패널티" 전환과 맞음. 점프 높이 배율 없음(`PlayerMover.JumpSpeed`가 `Rope.JumpHeight` 고정) |
| 탐색 중 부상 패널티 정책 | 신규(대기실 담당 수치) | 없음 | 정책 틀(`BodyPenaltyPolicy`)에 대기실·백스테이지·탈출로 묘기 밖 공통 정책 1개 추가 |
| 외줄 공연 중 부상 패널티 | 재사용(공연장 소유) | `MainStage/Tightrope/Damage/TightropeBodyPenalty.cs` | 기획 예시표(×0.8, 기어가기 ×0.5·착지 ×1.5, 외팔 목마 거리 −25%)와 같은 임시값 |
| 잘린 팔다리 연출 | 재사용 | `Systems/Damage/LimbDismemberment.cs`, `ModelLimbs.cs` (033 이전) | 모두에게 보임, 판정과 무관 |
| 상호작용 창구 | 재사용 | `Assets/Scripts/Systems/Player/PlayerInteraction.cs` | F 입력 = 상호작용, F 홀드 = 해제로 조작키 기획과 일치. 외팔 "시간 걸리는 상호작용 느려짐" 배율은 없음 |
| 신체 복구(역순·개인별·비용) | 신규 | 없음 | `PlayerCondition`에 역순 1개 되돌리기 추가 + 대기실 의료 전문가 화면(대기실 담당) |
| 절단 보너스·절단 기록 | 신규 | 없음 | 누가·언제·원인 기록 없음(텔레메트리는 판정 미사용) |

### 공연 결과·보상·공동 자금 (CC-110)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 확정 결과·참가자 명부 | 수정 후 재사용 | `Assets/Scripts/MainStage/Tightrope/Results/TightropeResults.cs`, `TightropeResult.cs` | 호스트가 시도마다 명부·결과(성공, 종료 이유 `Completed/AllFallen/TimeExpired`, 참가자별 시작/종료 손실 부위·결과) 고정. 기획 9-3 "공연장 → 대기실" 데이터의 성공·사망자·부상 변화를 이미 담음. 없는 것: 플레이어별 정산액, 전원 생존 여부(생존 보너스), 묘기 ID·난이도 단계. 외줄 이름·공연장 폴더 |
| 실패 후 묘기 재시작 | 수정(공연장 소유) | `MainStage/Tightrope/Rope/TightropeRun.cs:315`(`RestartDelay`) | 기획: 재시작 없이 다음 시간대로. 외줄 기획서 갱신(공연장 담당) 후 공연 종료 신호로 교체 |
| 보상 규칙 틀 | 수정 후 재사용 | `Assets/Scripts/Systems/Settings/RewardTable.cs`, `TrickSettings.cs` | 빈 abstract 클래스. 난이도 단계별 최대 보상·환호/야유 수치를 담을 자리로 쓸 수 있음 |
| 팀 호응 게이지(플레이어별 구간) | 신규 | 없음 | 게이지 코드 없음. 환호/야유 이벤트·수치는 공연장 담당 + 보상 시스템 문서 |
| 공동 자금·장부 | 신규 | 없음 | 크레딧 코드 없음. 호스트 확정·공유 값(기존 "이동은 클라·판정은 호스트" 결정) |
| 묘기 반복 횟수·난이도 단계·세트리스트 | 신규 | 없음 | 런 범위 데이터(CC-108과 공유) |
| 개인 플레이 기록 | 범위 밖 | `Assets/Scripts/Systems/Telemetry/` | 주석상 "게임 판정과 외부 전송은 하지 않는다". 장부·결과 저장에 쓰지 않는다 |

### 장소 전환·연결 (CC-111)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 공간 씬 4개 | 수정 후 재사용(각 담당) | `Assets/Scenes/Levels/*.unity` | 둘러보기용 `PlaySetup` 수준. Build Settings 미등록. 공연장은 한 씬에서 묘기 프리팹 교체(장소 기획 3-2-1) |
| 씬 전환 | 신규 | 없음 | `LoadScene`·NGO 씬 전환 호출 없음. 기획 9-1 전환 8종 |
| 출발 위치 | 재사용 | `PlayerSync/PlayerSpawnPoints.cs` | 씬의 자리 번호 순 위치. 대기실 로비·무대 입구 등 공간마다 하나 |
| 안전지대·무대 입구 통로·탈출 입구 집결 판정 | 신규 | 없음 | 구역 진입 판정 코드 없음. 쉬는 시간 종료·조기 시작·탈출 개시 계기 |
| 기물 소지(팔 개수별 슬롯) | 신규 | 없음 | 기물·슬롯 코드 없음, 입력 에셋에 1/2/3 역할 없음. 플레이어 범위 데이터(기획 9-2). 개수는 공통 세팅(PM 결정 #2) |
| 공통 조작 역할 추가 | 수정 후 재사용 | `Assets/Resources/Input/PlayerControls.inputactions`, `PlayerInputReader.cs` | 역할은 Move·Posture·Action·DirectionLeft/Right·Interact뿐. 달리기·앉기·기물 슬롯·카메라 휠·LM+RM 교차 없음(조작키 기획) |

## 결정 매칭 (1차 조사 Open → 최신 기획 답)

| 1차 Open | 기획 답 | 출처 |
|---|---|---|
| 부활 방식·패널티 | 비용 없이 다음 시간대(쉬는 시간·밤 정비) 시작 때 대기실에서 외팔 상태(잃은 팔다리 3)로 부활, 그 공연 본인 보상 박탈 | 부상 4-3, 7-3 / 흐름 5-4 |
| 부활 장소 | 대기실 (공연장 → 대기실 로비 자동 복귀 시) | 흐름 9-1 |
| "공연 완료" 상태를 공통으로 둘지 | 공연 결과는 성공/실패 + 사망자 목록으로 넘기고 개인 판정은 생존/사망. 외줄 `Arrived` 같은 공연 안 상태는 공연장 내부 | 흐름 3-3, 9-3 |
| 보상 공식 | 성공 시 남은 팀 호응 게이지 → 공동 자금, 사망자 구간 제거, 실패 시 0, 전원 생존 보너스. 수치는 보상 시스템 문서 | 흐름 3-3, 3-4, 10-2 |
| 공동 자금 소유 주체 | 팀 공용 잔액 하나(누가 써도 같은 잔액), 장부 기록. 런 범위, 세대 교체 시 초기화 | 흐름 5-2, 9-2 |
| 공통으로 옮길 대상 | 잃은 팔다리 수·몸 상태 전달·공통 부상 규칙은 시스템 담당 → 손상 효과 적용기·정책 틀·결과 명부의 공통 부분은 `Systems/`로 | 부상 9-4, 흐름 9장 |
| `ServerCanChangeState` 구조 | 기획에 직접 답 없음. 장소별 사망/리타이어 규칙이 달라(공연 사망·백스테이지 외팔/사망·탈출로 리타이어) 장소별 규칙이 필요하다는 근거만 있음 | 부상 2-2, 4-4 |
| 최신 부상 기획과 대조 | 위 부상 표에 반영: 절단 순서 고정, 재시작 대신 외팔 부활, 신체 복구, 점프 높이 감소, 탐색/공연 패널티 구분 | 부상 3~7장 |

## PM 결정 (2026-10-14) — 후속 Task 반영 사항

| # | 결정 | 기록 | 후속 |
|---|---|---|---|
| 1 | 쉬는 시간 타이머 **B안 기본**, A안도 구현해 세팅에서 전환(A/B 테스트용) | [run-rules.md](../../Harness/Project/Decisions/run-rules.md) #1 | CC-108 |
| 2 | 기물 소지는 **부상 기획 기본값**(팔 2개 = 2, 팔 1개 = 1, 배낭 +2), 세 숫자 모두 세팅에서 변경 | [injury-rules.md](../../Harness/Project/Decisions/injury-rules.md) #8 | CC-109·111 |
| 3 | 세팅을 **공통 / 각 묘기 / 대기실 / 탐색 / 탈출로** 5묶음으로 분리 | [game-settings.md](../../Harness/Project/Decisions/game-settings.md) | CC-108 |
| 4 | 공통 폴더 이전은 **별도 Task**(refactor, 1단계) | [통합 계획](../../Integrations/Active/Integration-common-game-systems/plan.md) | 신규 Task |
| 5 | 공통 게임 시스템은 묘기·탐색·대기실·탈출로를 **더미 로직·씬**으로 먼저 구성해 시스템과 설정 반영을 검증, 실제 공간 로직·씬은 나중에 교체·연결 | run-rules.md #4 | CC-108~111 |
| 6 | **생존 2명 미만 = 묘기 공통 실패** → 대기실 복귀, 생존자 부상 유지·사망자 외팔 부활 후 다음 쉬는 시간 | run-rules.md #2 | CC-109·110 |
| 7 | **게임 시작 후 재접속 비허용**(방을 새로 만들어야 함), 로비 재입장은 허용 — 현재 구현과 같음 | run-rules.md #3 | CC-108 |
| 8 | **최소 인원 2명**, 1인 테스트는 개발자 모드, 협력 테스트 AI는 검토 후 별도 Task | run-rules.md #5 | 신규 Task |

- 위 결정으로 1차 판단 항목 중 3(상태 규칙 구조)·5(외줄 정리 시점)는 더미 우선(#5) 방침에 따라 공통 시스템 쪽 구현 Task에서 정한다. 외줄 실제 코드는 교체·연결 단계까지 그대로 둔다.
- 작업 순서·병렬 구성·공용 파일 배정은 [통합 계획](../../Integrations/Active/Integration-common-game-systems/plan.md).

## 공통 폴더 이전 (4-4 설명)

- **공통 폴더** = `Assets/Scripts/Systems/`. [space-workspaces.md](../../Harness/Project/Decisions/space-workspaces.md)에서 "여러 공간이 함께 쓰는 코드"를 두기로 한 곳이다(네트워크·플레이어·카메라·세팅·기록이 이미 여기에 있다). 공간 폴더(`MainStage/`·`WaitingRoom/`·`BackStage/`·`EscapeRoute/`)는 각 담당 소유다.
- **옮기는 작업** = 지금 공연장 폴더(`Assets/Scripts/MainStage/Tightrope/`)에 있지만 모든 공간이 써야 하는 코드를 `Systems/`로 옮기는 정리 작업이다. 후보:
  - `Damage/BodyDamageEffects.cs`·`BodyPenaltyPolicy.cs` — 기어가기·패널티 정책 교체. 대기실·탈출로의 탐색 중 부상 패널티도 이 틀을 쓴다. 정책이 비면 외줄 정책을 쓰는 기본값도 바꿔야 한다.
  - `Results/TightropeResult(s).cs`의 명부·확정 결과 부분 — 모든 묘기가 같은 공연 결과(성공/실패·사망자·부상 변화)를 대기실로 넘긴다.
  - `Damage/LimbDismemberment.cs`·`ModelLimbs.cs` — 절단 연출은 탈출로 묘기 구간·추격자에서도 쓴다.
- 왜 필요한가: asmdef가 없어 옮기지 않아도 컴파일은 되지만, 대기실·탈출로 코드가 공연장 폴더에 의존하면 공연장 담당이 고칠 때 다른 공간이 깨지고 소유권이 불분명해진다.
- 할 때 주의: `.meta`째 옮겨 GUID를 유지해야 프리팹 참조가 끊기지 않는다. 공연장(@lakuase2) 파일 수정이므로 PM 배정이 필요하다. 더미 우선(#5) 단계에서는 더미가 `Systems/` 쪽 새 틀을 쓰고, 이전은 실제 공간 연결 때 해도 된다.
- **결과 (Task-033, 2026-10-14)**: Damage 4개(`BodyDamageEffects`·`BodyPenaltyPolicy`·`LimbDismemberment`·`ModelLimbs`)를 `Assets/Scripts/Systems/Damage/`로 이전(GUID·namespace 유지). 외줄 기본 정책은 `TightropeBodyPenalty`가 `BodyDamageEffects.DefaultPolicyFactory`에 등록. 결과 명부 분리는 031로 넘김(경계안: 033 handoff).

## 진입점

- 현재 공통 진입점: `NetworkSessionManager`(방·방 상태), `NetworkPlayer`(상태·부위 손실·재시작), `PlayerCondition`(몸 상태), `CameraSpectator`(관전), `GameSettings.Base`.
- 공연 결과는 아직 `TightropeResults`(외줄 전용)가 유일하다.
- 부상: `BodyDamageEffects.SetPolicy(policy)`(null = 콘텐츠 불이익 없음), `IsCrawling`, `Current`. 시작 정책이 비면 `BodyDamageEffects.DefaultPolicyFactory`(지금은 외줄).

## 공간에서 쓰는 법 (부상)

- 공간 Task는 `Systems/Damage/`를 **읽고 부르기만** 한다. 고칠 일이 있으면 PM에게 요청한다(`space-workspaces.md`).
- Player 프리팹에 이미 붙어 있어 어느 공간이든 Player만 두면 절단 연출·기어가기가 동작한다. 절단 요청은 `NetworkPlayer.RequestCutPart`(소유자)·`ServerCutPart`(호스트), 상태는 `PlayerCondition.LostParts`·`Changed`.
- 공간 전용 불이익은 내 공간 스크립트 폴더에 `BodyPenaltyPolicy`를 상속해 만들고, 공간에 들어올 때 `GetComponent<BodyDamageEffects>().SetPolicy(내 정책)`, 불이익이 없으면 `SetPolicy(null)`.
- ⚠ 지금은 어느 공간이든 기본 정책이 외줄 불이익이다(옮기기 전과 같음). 공간별 기본값은 030(부상·생존)에서 정한다.
- 새 모델은 `ModelLimbs`로 팔·다리 위치를 지정(휴머노이드는 생략 가능).

## 의존 도메인

- [network-session.md](network-session.md) — 방 상태는 런 단계와 별개
- [player-sync.md](player-sync.md) — 플레이어 상태·부위 손실 동기화
- [tightrope-course.md](tightrope-course.md) — 현재 결과·재시작 경계의 유일한 사용처
- [player-control.md](player-control.md) — 입력 역할·조작 규칙·이동 부품

## 수정 주의점

- 공간 내부 기능은 각 담당 소유([space-workspaces.md](../../Harness/Project/Decisions/space-workspaces.md)). 런 상태·장소 전환·공간 간 규칙은 시스템 담당.
- 보상·가격·패널티 수치는 미확정. 세팅 파일 값으로 둔다.
- `PlayerState`·`BodyPart`는 숫자로 전송한다. 기존 값을 바꾸지 말고 끝에만 추가한다.
- 접속 승인 콜백은 `NetworkSessionManager`만 등록한다.
- `Systems/Damage/`는 공연장 코드(`MainStage/`)를 부르지 않는다. 묘기 전용 정책은 묘기 쪽이 `SetPolicy` 또는 `DefaultPolicyFactory`로 끼운다.
