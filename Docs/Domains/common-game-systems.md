# 공통 게임 시스템

> Task-20261013-029(CC-107) 기존 기능 조사 결과. 코드 기준 `feat/feature-reuse-audit`(2026-10-13), 기획 기준 Confluence 최신판(게임 흐름 15564804·부상 14254330·장소 10454669·조작 11633076, 2026-10-13), 기획 갱신 Confluence 27721807 M2 변경점·27459631 보상 확정 규칙(2026-10-27, Task-20261027-076).
> "근거"는 코드에서 확인한 사실, "기획"은 [게임 흐름](../../Harness/Project/Facts/game-flow.md)·[런 상태](../../Harness/Project/Facts/run-state.md)·[부상 규칙](../../Harness/Project/Decisions/injury-rules.md)·[조작키](../../Harness/Project/Decisions/control-keys.md) 요약이다.

## 목적
런 진행·부상/생존·공연 결과·공동 자금·장소 전환처럼 여러 공간이 함께 쓰는 게임 시스템의 지도. 기획상 담당은 시스템(@MoHoDu): 런 상태 관리, 장소 전환, 공간 간 규칙, 잃은 팔다리 수 관리.

## 현재 기획 안내 (2026-10-27 M2)
- 위 조사 날짜·과거 근거는 보존. 공용 PR #64·패널 PR #65 dev 병합은 전체 공간 연결/4인 검증 완료가 아니다. 현재 공연장·탈출로 @RagSpirin, 대기실 @gyumin9048-ctrl([팀](../../Harness/Project/Facts/team.md)), 과거 @lakuase2 기록은 당시 담당.
- 최신 [게임 흐름](../../Harness/Project/Facts/game-flow.md)(M2): 대기실·백스테이지를 대기실로 통합(코드 폴더 `BackStage/`·씬 이름은 그대로), 쉬는 시간 A/B안 삭제(안전지대 없음, 무대 입구 통로 진입으로 복귀), 전문가 스케줄 삭제(서비스는 신체 복구 하나, 시설 서비스 삭제, 공연 중 외 언제든), 기물 구매는 밤 정비만, 신체 복구 1~3부위·부위당 100토큰/+25(보상 규칙 27459631이 M2 10/+5 대체), 난이도 단계 선택, 인간 대포 추락 2곳 절단·외줄 매달림(7초, 동료가 F 2초로 끌어올림). 추가 결정: 방은 런마다 랜덤, 밤 정비는 전력 차단·철창과 침대 수면으로 종료, 물려줄 선반 → 가판대. 기물은 2026-10-28 사용자 결정으로 팔 개수 무관 기본 2칸, 배낭·외팔 슬롯 감소 삭제. 코드와 다른 점은 아래 "M2 미반영 구현" 절.
- 생존 규칙(2026-10-16): 인간 대포 v0.5의 **살아 있는 1명 도착 성공**을 모든 묘기 공통으로 올림(run-rules.md #10). 공용 기준 `PerformanceRewards.OfficialMinSurvivors = 1`.

## 재사용 목록

분류: 재사용 / 수정 후 재사용 / 신규 / 범위 밖(공간 담당 또는 이후)

### 런 상태·세션 (CC-108)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 방 접속·나가기·정원·진행 중 참가 거절 | 재사용 | `Assets/Scripts/Systems/Network/Session/NetworkSessionManager.cs` | 유일한 공개 진입점, 접속 승인 콜백 소유. 정원 4·최소 1(테스트). 기획 2~4인 → 최소 인원은 세팅값으로 조정 |
| 방 상태 `Waiting/Playing/Ended` | 재사용(의미 한정) | `GameSessionState.cs`, `GameSessionStateSync.cs` | 호스트만 변경. 로비 → 런 진행 → 런 종료 단위로만 쓰고, 공연 종료 신호로 쓰지 않는다(하루 공연 2회) |
| `RestartEndedGame()` | 수정 후 재사용 | `NetworkSessionManager.cs:291` | `internal`, 호출처는 `TightropeRun.cs:117`뿐. 기획은 공연 후 재도전이 없으므로 세대 교체(런 재시작) 경계로만 남길지 정리 필요. 재접속 비허용(PM 결정 #7)은 지금의 진행 중 참가 거절과 일치 |
| 런 진행 관리자(일차·단계·탐색 제한 시간·밤 준비 완료) | 신규(구현) | `Assets/Scripts/Systems/Campaign/` (Task-028) | `CampaignRun`(진입점, 호스트 판정·모두에게 공유)·`RunState`(판정). 세대·일차·시간대 전환, 쉬는 시간 타이머 A/B·소집 경보, 밤 전원 준비 완료, 기한 초과 → 세대 교체, 탈출 신호. 세트리스트는 자리만(묘기 이름 임시 문자열) |
| 세션 범위 데이터(세대 번호·가판대(옛 이름: 물려줄 선반)·흔적) | 신규 | 없음 | 세대 코드 없음. 같은 게임 세션 안에서만 유지(기획 6-2) |
| 조정값 시작점 | 수정 후 재사용(5묶음 완료) | `Assets/Scripts/Systems/Settings/GameSettings.cs`, `BaseGameSettings.cs`, `WaitingRoomSettings.cs`, `ExplorationSettings.cs`, `EscapeRouteSettings.cs` | 공통 = `BaseGameSettings`(런 진행·기물 소지 칸 추가, Task-028), 묘기 = `Tricks/*`, 대기실·탐색·탈출로는 빈 틀(칸은 CC-119가 채움) |

### 부상·생존 (CC-109)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 몸 상태(잃은 팔다리 비트) | 재사용 | `Assets/Scripts/Systems/Player/PlayerCondition.cs`, `BodyPart.cs` | 비트 플래그. 절단 순서가 고정이면 `LostCount`가 곧 기획의 "잃은 팔다리 수(0~4)" — 그대로 둬도 수와 1:1 |
| 고정 절단 순서 | 수정 후 재사용 | `PlayerCondition.NextLegThenArmCut(lost, leftFirst: true)` | `leftFirst=true`면 왼다리→오른다리→왼팔→오른팔로 기획과 일치. 그러나 `ResolveCut`(요청 부위), 톱날 호출(`TightropeSaws.cs:497`: 가로 = 닿은 쪽, 수직 = 닿은 위치), 디버그 요청(무작위 좌우)은 순서를 어김 |
| 부위 손실 동기화·사망 확정 | 수정 후 재사용 | `Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs` | 조사 당시 `ServerCutPart`·`ServerCutLegThenArm`만 있었고, Task-030에서 `ServerCut(개수)`(공통 순서로 N곳, 4번째 손실에서 사망하고 중단)·외팔 설정·역순 복구를 추가(아래 진입점). 4곳 손실 → `Fallen`. M2 인간 대포 추락(2곳)은 `ServerCut(2)`로 표현 가능 |
| 플레이어 상태 `Normal/Fallen/Arrived` | 수정 후 재사용 | `PlayerState.cs` | `Fallen` = 기획의 사망(추락 포함)과 같은 뜻이라 유지 가능. 없는 것: 리타이어(탈출로), 추격 중·끌려옴. `Arrived`는 외줄 도착 전용 의미. 값은 끝에만 추가 |
| 상태 변경 허용 규칙 | 수정 후 재사용 | `NetworkPlayer.ServerCanChangeState`(`NetworkPlayer.cs:51`) | `static Func` 1개를 `TightropeRun`이 씀. 장소마다 규칙이 다르므로(공연/쉬는 시간/탈출로) 현재 장소의 규칙만 끼우는 방식 필요 |
| 재시작 시 몸 복구 | 수정 후 재사용(대체) | `NetworkPlayer.ServerRestartAll()`, `BaseGameSettings.BodyRestartRecovery` | 사망자를 온전한 몸으로 복구. 기획: 묘기 재시작 없음, 사망자는 다음 시간대에 **외팔 상태**로 부활 → 부활 함수로 대체, 복구 설정은 폐기 대상 |
| 사망 후 관전 | 재사용 | `Assets/Scripts/Systems/Camera/CameraSpectator.cs` | 내 캐릭터 사망 시 살아 있는 사람을 따라감(A/D 전환). 기획 "공연 끝까지 관전"과 일치. 추격당하는 사람 시점 관전·리타이어 후 엔딩까지 관전은 대상 지정 기능 추가 필요 |
| 손상 효과 적용기(기어가기 + 정책) | 수정 후 재사용 | `Assets/Scripts/Systems/Damage/BodyDamageEffects.cs`, `BodyPenaltyPolicy.cs` | 033에서 공통 폴더로 이전(동작 동일). 정책 교체 구조는 기획의 "탐색 중 / 공연 중 부상 패널티" 전환과 맞음. 점프 높이 배율 없음(`PlayerMover.JumpSpeed`가 `Rope.JumpHeight` 고정) |
| 탐색 중 부상 패널티 정책 | 신규(대기실 담당 수치) | 없음 | 정책 틀(`BodyPenaltyPolicy`)에 대기실·탈출로 묘기 밖 공통 정책 1개 추가 |
| 외줄 공연 중 부상 패널티 | 재사용(공연장 소유) | `MainStage/Tightrope/Damage/TightropeBodyPenalty.cs` | 기획 예시표(×0.8, 기어가기 ×0.5·착지 ×1.5, 외팔 목마 거리 −25%)와 같은 임시값 |
| 잘린 팔다리 연출 | 재사용 | `Systems/Damage/LimbDismemberment.cs`, `ModelLimbs.cs` (033 이전) | 모두에게 보임, 판정과 무관 |
| 상호작용 창구 | 재사용 | `Assets/Scripts/Systems/Player/PlayerInteraction.cs` | F 입력 = 상호작용, F 홀드 = 해제로 조작키 기획과 일치. 외팔 "시간 걸리는 상호작용 느려짐" 배율은 없음 |
| 신체 복구(역순·비용) | 구현(규칙은 M2 전) | `NetworkPlayer.ServerRestorePart`, `Assets/Scripts/WaitingRoom/ExpertService/` | 기획(보상 27459631): 1~3부위 선택, 공동 자금에서 부위당 100토큰·팀 이용 1회마다 다음 가격 +25(여러 부위도 1회), 런마다 초기화, 쉬는 시간·밤 언제든 |
| 절단 보너스·절단 기록 | 신규 | 없음 | 누가·언제·원인 기록 없음(텔레메트리는 판정 미사용). 기획: 절단 개당 호응 +10 고정, 복구 비용 연동·비용보다 낮아야 한다는 제한 삭제([보상 규칙](../../Harness/Project/Decisions/reward-rules.md)) |

### 공연 결과·보상·공동 자금 (CC-110)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 확정 결과·참가자 명부 | 수정 후 재사용 | `Assets/Scripts/MainStage/Tightrope/Results/TightropeResults.cs`, `TightropeResult.cs` | 호스트가 시도마다 명부·결과(성공, 종료 이유 `Completed/AllFallen/TimeExpired`, 참가자별 시작/종료 손실 부위·결과) 고정. 기획 9-3 "공연장 → 대기실" 데이터의 성공·사망자·부상 변화를 이미 담음. 당시 기록상 없는 것: 플레이어별 정산액·묘기 ID·난이도 단계. 새 완주 보너스용 실제 도착 여부 데이터는 재확인 필요(전원 생존 보너스는 삭제). 외줄 이름·공연장 폴더 |
| 실패 후 묘기 재시작 | 수정(공연장 소유) | `MainStage/Tightrope/Rope/TightropeRun.cs:315`(`RestartDelay`) | 기획: 재시작 없이 다음 시간대로. 외줄 기획서 갱신(공연장 담당) 후 공연 종료 신호로 교체 |
| 보상 규칙 틀 | 수정 후 재사용 | `Assets/Scripts/Systems/Settings/RewardTable.cs`, `TrickSettings.cs` | 빈 abstract 클래스. 난이도 단계별 최대 보상·환호/야유 수치를 담을 자리로 쓸 수 있음 |
| 팀 호응 게이지·공동 자금·장부 | 신규 → 구현 | `Assets/Scripts/Systems/Reward/PerformanceRewards.cs`, `Systems/CheerGauge/` | 조사 당시 없음. 기획([보상 규칙](../../Harness/Project/Decisions/reward-rules.md), Confluence 27459631): 토큰 공동 자금, 단계별 1인 상한100/150/200, 생존 호응×2·사망 호응½×2(중간 버림 없음), 도착자만100/150/200 추가. 실패도 호응 보상 지급·전원 생존 보너스 삭제. 환호·야유·관심 감소·공용 환호 포인트 규칙 확정, 포인트 위치 배치 방식 미정. 공동 자금은 호스트 확정·공유 값 |
| 묘기별 클리어 단계·난이도 선택·세트리스트 | 신규(세트리스트 자리만) | `RunState.SetSetlist`(묘기 이름만) | 기획(M2): 반복 횟수 대신 묘기별 클리어 최고 단계, 세트리스트에서 다음 단계까지 고름, 1일차 1단계. 런 범위 데이터(CC-108과 공유) |
| 개인 플레이 기록 | 범위 밖 | `Assets/Scripts/Systems/Telemetry/` | 주석상 "게임 판정과 외부 전송은 하지 않는다". 장부·결과 저장에 쓰지 않는다 |

### 장소 전환·연결 (CC-111)

| 기능 | 분류 | 기존 파일 | 근거 → 기획과의 관계 |
|---|---|---|---|
| 공간 씬 4개 | 수정 후 재사용(각 담당) | `Assets/Scenes/Levels/*.unity` | 둘러보기용 `PlaySetup` 수준. Build Settings 미등록. 공연장은 한 씬에서 묘기 프리팹 교체(장소 기획 3-2-1). M2는 대기실·백스테이지 통합(씬은 아직 분리) |
| 씬 전환 | 신규 | 없음 | `LoadScene`·NGO 씬 전환 호출 없음. 기획 9-1 전환 계기 |
| 출발 위치 | 재사용 | `PlayerSync/PlayerSpawnPoints.cs` | 씬의 자리 번호 순 위치. 대기실 로비·무대 입구 등 공간마다 하나 |
| 무대 입구 통로·탈출 입구 집결 판정 | 신규 | 조사 당시 없음(이후 `WaitingRoom/Base/StageEntranceGather.cs`) | 쉬는 시간 종료·조기 시작·탈출 개시 계기. M2: 쉬는 시간 안전지대 없음, 무대 입구 통로 진입이 유일한 복귀 |
| 기물 소지(팔 개수 무관 기본 2칸) | 반영(Task-076, 2026-10-28) | `Systems/Settings/BaseGameSettings.cs`·`Survival/SurvivalRules.cs`·`WaitingRoom/ShopStorage/PropCarry.cs` | `ItemCarry.BaseSlots`로 통일, 배낭 판매·수령·소지/공유 상태·외형 제거. 기존 씬 배낭은 카탈로그에서 제외. 장부18건·W2 9건 통과, 구형 HUD/더미·전체 검사 실패는 Task log 참조 |
| 공통 조작 역할 추가 | 수정 후 재사용 | `Assets/Resources/Input/PlayerControls.inputactions`, `PlayerInputReader.cs` | 역할은 Move·Posture·Action·DirectionLeft/Right·Interact뿐. 달리기·앉기·기물 슬롯·카메라 휠·LM+RM 교차 없음(조작키 기획) |

## 결정 매칭 (1차 조사 Open → 기획 답)

| 1차 Open | 기획 답 | 출처 |
|---|---|---|
| 부활 방식·장소 | 비용 없이 다음 시간대(쉬는 시간·밤 정비) 시작 때 대기실(공연장 → 대기실 로비 자동 복귀 시)에서 외팔 상태(잃은 팔다리 3)로 부활, 그 공연 사망 전 호응도½×2토큰 지급(중간 버림 없음, 완주 보너스 없음) | 부상 4-3, 7-3 / 흐름 5-4, 9-1 |
| "공연 완료" 상태를 공통으로 둘지 | 공연 결과는 성공/실패 + 사망자 목록으로 넘기고 개인 판정은 생존/사망. 외줄 `Arrived` 같은 공연 안 상태는 공연장 내부 | 흐름 3-3, 9-3 |
| 보상 공식·공동 자금 | 생존 호응×2·사망 호응½×2(중간 버림 없음), 도착자만 단계별100/150/200 완주 보너스. 전원 사망 실패에도 호응 보상 지급, 별도 전원 생존 보너스 삭제. 토큰 공동 잔액 하나·장부, 런 범위 | [보상 규칙](../../Harness/Project/Decisions/reward-rules.md), Confluence 27459631(2026-10-27)이 옛 흐름·M2 보상/가격 대체 |
| 공통으로 옮길 대상 | 잃은 팔다리 수·몸 상태 전달·공통 부상 규칙은 시스템 담당 → 손상 효과 적용기·정책 틀·결과 명부의 공통 부분은 `Systems/`로 | 부상 9-4, 흐름 9장 |
| `ServerCanChangeState` 구조 | 기획에 직접 답 없음. 장소별 사망/리타이어 규칙이 달라(공연 사망·쉬는 시간 대기실 외팔/사망·탈출로 리타이어) 장소별 규칙이 필요하다는 근거만 있음 | 부상 2-2, 4-4 |

## PM 결정 (2026-10-14) — 후속 Task 반영 사항

| # | 결정 | 기록 | 후속 |
|---|---|---|---|
| 1 | 탐색 제한 시간 B안 기본·A안 세팅 전환 → **M2(2026-10-27)에서 A/B 삭제**, 안전지대 없이 무대 입구 통로 복귀 하나 | [run-rules.md](../../Harness/Project/Decisions/run-rules.md) #1 | CC-108 |
| 2 | 당시 2/1/+2 → **2026-10-28 대체**: 팔 개수 무관 기본 2칸, 배낭·외팔 슬롯 감소 삭제. 기본 개수는 공통 세팅 | [injury-rules.md](../../Harness/Project/Decisions/injury-rules.md) #8 | CC-109·111 |
| 3 | 세팅을 **공통 / 각 묘기 / 대기실 / 탐색 / 탈출로** 5묶음으로 분리 | [game-settings.md](../../Harness/Project/Decisions/game-settings.md) | CC-108 |
| 4 | 공통 폴더 이전은 **별도 Task**(refactor, 1단계) | [통합 계획](../../Integrations/Active/Integration-common-game-systems/plan.md) | 신규 Task |
| 5 | 공통 게임 시스템은 묘기·탐색·대기실·탈출로를 **더미 로직·씬**으로 먼저 구성해 시스템과 설정 반영을 검증, 실제 공간 로직·씬은 나중에 교체·연결 | run-rules.md #4 | CC-108~111 |
| 6 | **전원 사망 = 묘기 공통 실패**(2026-10-16, 생존 1명까지 계속) → 대기실 복귀, 생존자 부상 유지·사망자 외팔 부활 후 다음 쉬는 시간 | run-rules.md #2·#10 | CC-109·110 |
| 7 | **게임 시작 후 재접속 비허용**(방을 새로 만들어야 함), 로비 재입장은 허용 — 현재 구현과 같음 | run-rules.md #3 | CC-108 |
| 8 | **최소 인원 2명**, 1인 테스트는 개발자 모드, 협력 테스트 AI는 검토 후 별도 Task | run-rules.md #5 | 신규 Task |

- 1차 판단 3(상태 규칙 구조)·5(외줄 정리 시점)는 더미 우선(#5)에 따라 공통 시스템 구현 Task에서 정한다. 작업 순서·공용 파일 배정은 [통합 계획](../../Integrations/Active/Integration-common-game-systems/plan.md).

## 공통 폴더 이전

- **공통 폴더** = `Assets/Scripts/Systems/`(여러 공간이 함께 쓰는 코드, [space-workspaces.md](../../Harness/Project/Decisions/space-workspaces.md)). 공간 폴더 `MainStage/`·`WaitingRoom/`·`BackStage/`·`EscapeRoute/`는 각 담당 소유. 공간 코드가 공연장 폴더에 의존하면 소유권이 흐려지므로 공용 코드는 `.meta`째(GUID 유지) `Systems/`로 옮긴다. **결과 (Task-033, 2026-10-14)**: Damage 4개(`BodyDamageEffects`·`BodyPenaltyPolicy`·`LimbDismemberment`·`ModelLimbs`)를 `Assets/Scripts/Systems/Damage/`로 이전(GUID·namespace 유지). 외줄 기본 정책은 `TightropeBodyPenalty`가 `BodyDamageEffects.DefaultPolicyFactory`에 등록. 결과 명부 분리는 031로 넘김(경계안: 033 handoff).

## M2 미반영 구현 (2026-10-27 확인)

기획은 M2로 바뀌었지만 코드·UI는 예전 규칙인 것. 고칠 때는 각 담당 Task에서 한다.
- 쉬는 시간 A/B·B안 기본(대기실을 나가면 300초 타이머 시작, 대기실 안전): `BaseGameSettings.RestTimerMode`·`restDuration`, `RunState.NotifyLeftWaitingRoom`, `WaitingRoom/ExplorationTimer/WaitingRoomExitWatch.cs`, `DummyExploration.IsSafe`. 대기실 재진입 막기는 코드에 없음(`WaitingRoomExitWatch`는 재입장 허용).
- 일차별 전문가(의료 1·3일차, 시설 2일차)·"담당자 부재중"·서비스 밤 전용: `WaitingRoom/ExpertService/Logic/ExpertServiceBook.cs`(`Availability`·`NotNight`·`Absent`), `UI/SceneIntegration/WaitingRoomShopUI.cs`("밤 정비 때 이용 가능" 문구). 시설 서비스 삭제 미반영: `ExpertServiceBook.cs`(`ExpertKind.Facility`·`ObstacleAdjust`·`BookFacility`), `ExpertServicePhonePanel.DrawFacility`, `WaitingRoomShopUI.cs` "시설 서비스" 항목, `NightSchedule/ScheduleBoardView.cs` 전문가 열("시설 담당자").
- 신체 복구가 잃은 부위 전부를 한 번에, 부위당 30 고정(1~3부위 선택·100토큰/+25 상승·런 초기화 없음): `WaitingRoom/ExpertService/ExpertService.cs`(`medicalPrice`·`Restore`), `ExpertServiceBook.MedicalCost`. 공용 `NetworkPlayer.ServerRestorePart`는 역순 1개.
- 기물 구매 밤 제한은 2026-10-28 Task-076에서 코드 반영(호스트 `WaitingRoomShop.CanPurchase`·W2/임시 UI, PlayMode 상점5건 통과). 배송 수령·보관 규칙은 유지. 아직 미반영: 밤 정비 문 잠김(셔터로 무대 입구 통로 차단): `WaitingRoom/NightSchedule/NightDoorLock.cs`. M2는 전력 차단·로비와 이어진 탐색 방 입구 철창으로 일부 영역 외 탐색 제한.
- 밤 정비 종료가 준비 완료 버튼(호스트는 스케줄표 확정 뒤): `NightSchedule/ScheduleBoard.cs`(`CanReady`·`RequestReady`) → `CampaignRun.SetReady`·`RunState.SetReady`(`ReadyCount`), `UI/SceneIntegration/WaitingRoomScheduleUI.cs`("준비 완료" 버튼). M2: 각자 침대 상호작용 → 모두 누우면 수면 연출 → 다음 날(침대 상호작용 코드 없음). 런마다 방을 섞는 런타임 코드도 확인 안 됨(대기실 배치는 `WaitingRoom/Base/Editor/` 빌더의 고정 배치).
- 난이도 단계: 반복 횟수 자동 상승 코드는 없고 단계는 컴포넌트 고정값(`TightropeCampaignBridge`·`CampaignTightropeAdapter` 1, `HumanCannonHazardCourse.difficulty`). 세트리스트는 묘기 이름만(`RunState.SetSetlist`), 클리어 단계·단계 선택 없음. 단계별 1인 상한 연결은 확인 안 함.
- 인간 대포 추락 즉사: `MainStage/HumanCannon/HumanCannonSystem.Landing.cs`(안전 착지 실패 → `ServerKill`), `HumanCannonRouteSystem.cs`(낙하 높이 → `ServerKill`). 외줄 추락 즉사(7초 매달림·동료 F 2초 끌어올리기·목마 낙하 시 한 줄에 한 명 잡기 없음): `PlayerBalance.Fell` → `NetworkPlayer`의 `RequestFall`, `MainStage/Tightrope/Rope/TightropeCourse.cs`(줄 아래 낙하), 목마 연쇄 추락도 바로 낙하. 환호 포인트는 `MainStage/`에 코드 없음.
- 백스테이지 씬·HUD 분리: `Assets/Scenes/Levels/backstage.unity`·`waiting_room.unity`, `BackstageHud`([backstage-hud.md](../../Harness/Project/Facts/backstage-hud.md)), 잡힘 장소 규칙 `Systems/Survival/SurvivalRules.cs`(탐색 = 대기실·백스테이지).
- W1 시계는 남은 시간 m:ss + 채움 비율(`backstage-hud.md`, `UI/SceneIntegration/CommonWaitingRoomUI.cs`), 하루 원형 시계 없음. W3 스케줄표([schedule-ui.md](../../Harness/Project/Facts/schedule-ui.md))의 난이도 단계 표시, W2 상점([shop-ui.md](../../Harness/Project/Facts/shop-ui.md)) 데모 문구의 M2 차이는 확인 안 함.

## 보상 확정 규칙과 구현 확인 대상 (2026-10-27)

- [보상 규칙](../../Harness/Project/Decisions/reward-rules.md)(Confluence 27459631)이 옛 사망 박탈·실패 0·전원 생존 보너스·복구 10/+5를 대체한다. 위 조사 당시 구현 근거와 현재 기획을 구분한다.
- 신규 규칙 대조가 필요한 구현: `Systems/Reward/PerformanceRewards.cs`·보상 세팅·`Systems/CheerGauge/`·결과/난이도 UI·묘기별 환호/야유 신호 연결. **이번 문서 동기화에서 코드를 재검증하지 않았으므로, 실제 옛 규칙 잔존 여부와 구현 완료 여부는 미확인**이다.
- 대조 항목: 사망 절반 인정 표시·실패 보상·도착자만 완주 보너스·중간 버림 없음·전원 생존 보너스 제거; 협동각10/오브제최초5/근접회피5/절단개당10/포인트15; 추락−15 중복 방지·시간끌기15초마다−10(정상 대기 제외); 개인별20초 유예/초당−1·시간끌기 시 유예 초기화·완주/사망 고정; 공용 포인트 선착획득/소멸·인원×2/3/4; 복구100/+25·팀 이용1회·역순·런 초기화.
- 위 M2 미반영 목록의 복구 전부/부위당30 및 환호 포인트 미연결 기록은 이전 조사 근거이며 이번에 새로 검증한 사실이 아니다. 구현 수정 시 현재 상태를 다시 대조한다.
- 경제 최대 1인3,000토큰은 목표, 현6회 상한 완주 계산2,700토큰과의 차이 조정 미정. 기물 새제품 가격은 확정(토큰): 구조장갑100·끈끈이장갑100·광대벌룬바지125·스프링신발175·머리핀50·말뚝뽑개75·UV등75. 2026-10-28 [새제품 내구도·차감·소진 처리](../../Harness/Project/Decisions/item-durability.md)도 확정. 효과 수치·접두사·쿨타임·발견품 내구도 등의 기존 초안·미정은 유지하고, 변경 시 기물·보상 문서를 함께 수정한다. 상점 설정·UI 연결은 별도 구현 확인 대상이다. 포인트 위치 배치 방식·묘기별 성공/회피 판정·실제 수입은 후속 확인.

## 진입점

- 현재 공통 진입점: `NetworkSessionManager`(방·방 상태), `CampaignRun`(런 진행: 시간대 신호 메서드 + `StepChanged`·`RecallAlarm`·`RunEnded` 이벤트, 판정은 호스트), `NetworkPlayer`(상태·부위 손실·재시작), `PlayerCondition`(몸 상태), `CameraSpectator`(관전), `GameSettings.Base`·`Tightrope`·`WaitingRoom`·`Exploration`·`EscapeRoute`(세팅 5묶음). 묘기 공통 결과·호응·공동 자금은 `PerformanceRewards`, 외줄 명부는 `TightropeResults`.
- 부상: `BodyDamageEffects.SetPolicy(policy)`(null = 콘텐츠 불이익 없음), `IsCrawling`, `Current`. 시작 정책이 비면 `BodyDamageEffects.DefaultPolicyFactory`(지금은 외줄). 부상·생존(Task-030, 호스트): `NetworkPlayer.ServerCut(개수)`(공통 순서 절단), `ServerKill`, `ServerCatch`, `ServerSetOneArm`, `ServerRevive`·`ServerReviveAllDead`, `ServerRestorePart`(역순 1개), `ServerRestoreAll`·`ServerRestoreEveryone`, 이벤트 `Revived`. 장소 규칙 `SurvivalRules.ServerPlace`(공연/탐색/탈출로 → 사망·잡힘·리타이어), 기물 칸 `SurvivalRules.ItemSlots`. 시간대 시작 부활·세대 교체 복구는 `InjuryRevival`이 `CampaignRun.StepChanged`로 자동 처리.
- 조작(Task-037): `PlayerController.SetScheme(규칙)`(null = 기본 쪽으로 복귀), 씬 `SceneControlScheme`, 기본 이동 규칙 `ControlSettings.Default`. 입력 막기 `InputGate.SetBlocking(요청자, 열림)`·`IsBlocked`·`RequireRelease()`, UI 컴포넌트 `UIInputBlocker`.

## 공간에서 쓰는 법 (부상)

- 공간 Task는 `Systems/Damage/`를 **읽고 부르기만** 한다. 고칠 일이 있으면 PM에게 요청한다(`space-workspaces.md`). Player 프리팹에 이미 붙어 있어 어느 공간이든 Player만 두면 절단 연출·기어가기가 동작한다. 절단 요청은 `NetworkPlayer.RequestCutPart`(소유자)·`ServerCutPart`(호스트), 상태는 `PlayerCondition.LostParts`·`Changed`.
- 공간 전용 불이익은 내 공간 스크립트 폴더에 `BodyPenaltyPolicy`를 상속해 만들고, 공간에 들어올 때 `GetComponent<BodyDamageEffects>().SetPolicy(내 정책)`, 불이익이 없으면 `SetPolicy(null)`. 새 모델은 `ModelLimbs`로 팔·다리 위치를 지정(휴머노이드는 생략 가능).
- ⚠ 기본 정책은 여전히 외줄 불이익이다(외줄 동작 유지). 탐색 공간(대기실·백스테이지 씬·탈출로 묘기 밖)은 들어올 때 `SetPolicy(ExplorationBodyPenalty.Shared)`를 끼운다(Task-030, 수치는 공통 세팅 신체 손상 임시값). 점프 높이 감소는 정책과 무관하게 공통으로 적용된다.

## 공간에서 쓰는 법 (조작·입력 막기, Task-037)

- 코드: `Assets/Scripts/Systems/Control/`(`PlatformControlScheme`·`ControlSettings`·`SceneControlScheme`·`InputGate`·`UIInputBlocker`, 에디터 `Editor/ControlSettingsEditor`). 공용 조작 코드는 외줄을 모른다. 규칙 고르는 순서: 콘텐츠가 끼운 규칙(`SetScheme`) → 프리팹 시작 규칙(테스트용) → 씬 규칙(`SceneControlScheme`) → **기본 이동 규칙**. `PlayerController`가 매 프레임 다시 골라 플레이 중 변경이 바로 반영된다.
- 기본 이동 규칙: `Assets/Resources/Settings/Control/ControlSettings.asset`(메뉴 `Tools/CurtainCall/조작/기본 이동 규칙 선택`). 지금 값 = 플랫폼 이동(8방향·점프), 비워도 같다. 목록에 프로젝트의 규칙 에셋과 에셋 없는 규칙 종류("새로 만들기" → `Settings/Control/Schemes/`에 생성)가 자동으로 나온다. 공간 이동 규칙: 내 공간 스크립트 폴더에 `PlayerControlScheme`을 상속해 만들고, 씬에 `SceneControlScheme`을 두어 넣거나 들어갈 때 `SetScheme`. 공용 수정 없음.
- UI 입력 막기: 패널에 `UIInputBlocker`(보이는 동안 막음, 또는 `SetOpen`·입력칸 포커스). 요청자가 하나라도 남으면 막고, 모두 닫힌 뒤 게임 키를 다 뗀 다음 프레임에 다시 받는다. 디버그 키는 `InputGate.AddReleaseKeys`. 외줄 쪽: `TightropeControlScheme`은 `MainStage/Tightrope/Control/`(줄 위에서 `CourseControlSwitcher`가 끼움), `TightropeUIFocus`는 `InputGate`에 요청만 보내고, `TightropeInputGate`는 외줄 코드 호환 창구(개발 키 P·O·B·R·M·K·L 등록)로 남았다.
- 남은 일: 균형(`PlayerBalance`)은 외줄 전용 유지, 걷기 속도·점프 높이 공통 세팅 이전은 030에서 완료(`BaseGameSettings.Character.CourseMoveSpeed`·`JumpHeight`), 1인칭 기본 이동은 카메라 수평 방향 W/S로 사용자 확정(2026-10-15), 3인칭 유지. 연결은 fix Task-045(CC-131)로 별도 준비·미구현(통합 `first-person-movement-issue.md`).

## 공간에서 쓰는 법 (카메라, Task-038)

- `Assets/Scripts/Systems/Camera/`: `CameraViewDirector`가 요청을 판정·구도 적용. `CameraViewPreset`의 1/3인칭 수치·FOV·전환 시간을 플레이 중 조정한다. 설정 에셋은 `Assets/Resources/Settings/Camera/`. 씬 구역: BoxCollider trigger + kinematic Rigidbody + `CameraViewZone`, preset·priority 지정(preset이 비면 요청하지 않음). 로컬 소유자만 감지하며 겹침·teleport·disable은 다음 physics tick 정리한다.
- 코드 요청: `CameraViewDirector.Current.RequestView(this, preset, priority)` / `ReleaseView(this)`. 높은 priority·동률 최근 등록. 같은 source 갱신은 순서 유지, disable/destroy·despawn은 정리한다. `ViewForward`는 정규화된 월드 수평 방향(대상 없음·사망은 zero). 037의 시선 기준 이동 규칙·`InputGate` 연결은 후속 통합(PM 결정). `SetInputBlocked(bool)` 진입점은 마련했지만 게임 조작 완성을 의미하지 않는다.
- 관전은 기존 `CameraSpectator` 대상 선택 유지(사망 `Fallen`·리타이어 `Retired` 모두 관전, 대상은 `Normal`만), 1인칭 요청은 잠시 제외. 모델은 게임 카메라 render 동안만 `forceRenderingOff`로 숨기며 `Renderer.enabled`·부상 계산은 변경하지 않는다. 가림: `CameraOcclusionFade.RequestRules` / `RegisterExclusion` / `ReleaseSource`로 공간별 수치·예외 공급. 외줄 구도·코스/같은 목마 예외는 `MainStage/Tightrope/Camera/TightropeCameraFraming.cs`로 이동(GUID 유지).
- 테스트 씬 `Assets/Scenes/Tests/Systems/CameraViews/CameraViews.unity`: 기존 LAN UI, z=6~14의 1인칭 구역. Esc 해제·게임 클릭 잠금, 테스트 UI 클릭은 제외. 테스트: 요청 판정 EditMode `Camera/Tests/EditMode/`, 실제 씬·LAN 호스트 PlayMode `Camera/Tests/PlayMode/`(CameraViews·proto_0.0.2를 열어 확인, 게임 코드는 이름으로 호출).

## 의존 도메인

- [network-session.md](network-session.md) — 방 상태는 런 단계와 별개
- [player-sync.md](player-sync.md) — 플레이어 상태·부위 손실 동기화
- [tightrope-course.md](tightrope-course.md) — 현재 결과·재시작 경계의 유일한 사용처
- [player-control.md](player-control.md) — 입력 역할·조작 규칙·이동 부품

## 수정 주의점

- 공간 내부 기능은 각 담당 소유([space-workspaces.md](../../Harness/Project/Decisions/space-workspaces.md)). 런 상태·장소 전환·공간 간 규칙은 시스템 담당.
- [보상 규칙](../../Harness/Project/Decisions/reward-rules.md)의 보상·복구100/+25·환호/야유·관심 감소 수치는 확정(27459631, 옛 M2 10/+5 대체). 세팅 값으로 둔다. 기물7종 새제품 가격은 확정([보상 규칙](../../Harness/Project/Decisions/reward-rules.md)), 새제품 내구도·차감·소진 처리는 [확정 규칙](../../Harness/Project/Decisions/item-durability.md)(2026-10-28), 효과 수치·접두사·쿨타임·발견품 내구도 등의 기존 초안·미정은 유지. 포인트 위치 배치 방식·묘기별 환호 판정·최대 수입 목표 차이 조정·중고/판매 가격은 미정.
- `PlayerState`·`BodyPart`는 숫자로 전송한다. 기존 값을 바꾸지 말고 끝에만 추가한다. 접속 승인 콜백은 `NetworkSessionManager`만 등록한다.
- `Systems/Damage/`는 공연장 코드(`MainStage/`)를 부르지 않는다. 묘기 전용 정책은 묘기 쪽이 `SetPolicy` 또는 `DefaultPolicyFactory`로 끼운다.
