# 공통 게임 시스템 작업 계획

목표: 런(1~3일차 + 4일차 아침) 진행, 부상·생존, 공연 결과·공동 자금, 장소 전환을 **더미 공간**으로 한 바퀴 돌려 보고, 설정을 바꾸면 그대로 반영되는지 확인한다. 실제 공간 로직·씬은 각 공간 담당이 만든 뒤 나중에 교체·연결한다.
근거: [기존 기능 조사](../../../Docs/Domains/common-game-systems.md)(Task-029) · [게임 흐름](../../../Harness/Project/Facts/game-flow.md) · [런 상태](../../../Harness/Project/Facts/run-state.md) · [부상 규칙](../../../Harness/Project/Decisions/injury-rules.md) · [런 공통 규칙](../../../Harness/Project/Decisions/run-rules.md) · [세팅](../../../Harness/Project/Decisions/game-settings.md) · [조작키](../../../Harness/Project/Decisions/control-keys.md)

## 정해진 것 (PM, 2026-10-14)

1. 쉬는 시간 타이머 **B안 기본**, A안도 만들어 세팅으로 전환(A/B 테스트).
2. 기물 소지 기본값은 부상 기획(팔 2개 = 2, 팔 1개 = 1, 배낭 +2), 세 숫자는 세팅에서 변경.
3. 세팅은 **공통 / 각 묘기 / 대기실 / 탐색 / 탈출로** 5묶음.
4. 공연장 폴더에 있는 공용 코드를 `Assets/Scripts/Systems/`로 옮기는 일은 **별도 Task**로 한다.
5. 공간은 **더미**로 먼저 만든다. 실제 로직·씬은 나중에 교체.
6. **생존 2명 미만 = 묘기 공통 실패** → 대기실 복귀, 생존자 부상 유지, 사망자만 외팔 부활.
7. **게임 시작 후 재접속 불가**, 로비 재입장은 허용.
8. **최소 인원 2명**. 1인 테스트는 **개발자 모드를 먼저** 만든다. 협력을 보는 테스트 AI는 개발자 모드 검토 뒤 이후에 고려한다.
9. 공간 작업자가 아직 작업 전이므로 **공통 폴더 이전은 PM이 먼저** 진행한다.

## 단계와 순서

```
0단계  029 기존 기능 조사 ← 지금. 병합 후 아래 Task plan을 이 문서 기준으로 갱신해 인계
1단계  A: 028 런 진행 + 세팅 5묶음 틀        B: [신규] 공통 폴더 이전 (짧음, 공연장 협의)
       C: 036 검사 스크립트 테스트 단계 수정 (하네스, 메인 폴더에서 진행 — 모든 Task의 "검사해줘"가 쓰므로 먼저)
       ★ 합치기 ①  세팅 틀·공용 코드 위치 확정 (2단계 Task들이 모두 이 위에서 시작)
2단계  A: 030 부상·생존   B: 031 공연 결과·공동 자금   C: [신규] 더미 공간   D: [신규] 개발자 모드
       E: 037 공용 조작 정리   F: 038 공용 카메라 시점 (2026-10-14 추가, 034와 병렬)
       ★ 합치기 ②  각 테스트 씬에서 기능별 확인
3단계  032 공간 연결 — 더미 공간으로 하루 → 밤 → 다음 날 → 런 실패/탈출까지 한 바퀴
4단계  (이후) 실제 공간 교체·연결(각 공간 통합과 협의), 테스트 AI 동료(개발자 모드 검토 뒤 결정)
```

- 1단계를 먼저 하는 이유: 세팅 파일(공통 세팅)과 옮길 파일(부상 효과·결과 명부)을 2단계 Task 넷이 모두 쓴다. 동시에 고치면 충돌한다.
- 2단계 넷은 작업 폴더가 겹치지 않게 나눴다. 공용 파일은 아래 표대로 한 Task만 고친다.
- 2단계 시작 전 **연결 약속을 먼저 정한다**(028의 시간대 시작·종료 알림, 031의 공연 결과 형식, 030의 부활·외팔 함수). 서로 기다리지 않고 각자 테스트 씬의 디버그 버튼으로 상대 쪽을 흉내 낸다.
- 병합 순서: 단계 안에서는 끝난 순서대로, 다음 단계는 앞 단계 합치기 뒤. 남은 PR은 병합마다 최신 통합 브랜치를 merge로 받는다(rebase·force 금지).

## Task 목록

| 단계 | Task | JIRA | 할 일 | 작업 구역 (제안) |
|---|---|---|---|---|
| 0 | 029 기존 기능 조사 | CC-107 | 재사용 목록·기획 반영·이 계획 | 문서만 |
| 1A | 028 런 진행 | CC-108 | 세대·일차·시간대(공연→쉬는 시간→밤) 전환, 쉬는 시간 타이머(A/B), 전원 준비 완료, 기한 초과 → 런 실패 → 세대 교체. 세팅 5묶음 클래스·에셋을 만들고 이미 정해진 공통 값(타이머 A/B, 기물 개수 2/1/+2, 최소 인원 2, 일수·공연 수·쉬는 시간 길이)을 넣는다 | `Systems/Campaign/`, `Tests/Systems/Campaign/` + 공용: 세팅 |
| 1B | 공통 폴더 이전 (refactor/common-code-move) | CC-117 | `MainStage/Tightrope/Damage/`의 `BodyDamageEffects`·`BodyPenaltyPolicy`·`LimbDismemberment`·`ModelLimbs`, (결과 명부 공통부 분리는 2026-10-14 PM 결정으로 031로 넘김) `Systems/`로 이동. **동작 변경 없음**, `.meta`째 이동, 외줄 테스트 씬 그대로 동작 확인 | 공용: 위 파일 + 받는 쪽 `Systems/` 폴더 |
| 1C | 036 검사 스크립트 테스트 단계 (fix/check-work-editor-tests) | CC-120 | 에디터가 열린 상태에서 테스트를 Pipeline `run_tests`로 실행, 테스트 0개는 `NO_PROJECT_TESTS`, 하네스 Task 작업 구역 경로 인식 | `Scripts/verify-unity.ps1`, `Scripts/check-work.ps1`, `Scripts/Lib/common.ps1` |
| 2A | 030 부상·생존 | CC-109 | 고정 절단 순서 통일, 외팔 상태 설정, 역순 신체 복구, 시간대 시작 때 사망자 외팔 부활, 장소별 사망/리타이어 규칙, 점프 높이 배율, 탐색 중 부상 패널티 정책(더미값), 기물 소지 칸 수 계산 | `Systems/Survival/`, `Tests/Systems/Survival/` + 공용: 플레이어 상태·몸 상태·이동 |
| 2B | 031 공연 결과·공동 자금 | CC-110 | 묘기 공통 공연 결과(성공/실패·사망자·부상 변화·정산액, 생존 2명 미만 실패), 팀 호응 게이지(플레이어별 구간·사망 시 제거), 공동 자금·장부(호스트 확정), 보상 규칙 파일 구현(임시값) | `Systems/Reward/`, `Systems/Results/`(새 공통 결과), `Tests/Systems/Reward/` (외줄 결과 코드는 참고만) |
| 2C | 더미 공간 (feat/dummy-spaces) | CC-119 | 더미 묘기(도착·사망·절단 버튼), 더미 대기실(로비·구매·의료 복구·준비 완료), 더미 탐색(백스테이지·안전지대·추격 시뮬), 더미 탈출로(도착·리타이어). 대기실·탐색·탈출로 세팅 파일 값 연결 | `Systems/DummySpaces/`, `Tests/Systems/DummySpaces/` + 공용: 대기실·탐색·탈출로 세팅 |
| 2D | 개발자 모드 (feat/dev-mode) | CC-118 | 최소 인원·생존 2명 미만 실패 같은 인원 규칙 예외 스위치(빌드 기본 꺼짐), 1인으로 공연·쉬는 시간 넘기기. 테스트 AI 동료 가능성 검토 결과를 handoff에 남김 | `Systems/DevMode/`, `Tests/Systems/DevMode/` + 공용: 세션 매니저(필요 시) |
| 2E | 037 공용 조작 정리 (feat/common-control-rules) | (대기) | 공용 조작의 외줄 의존 제거, 외줄 플랫폼 이동 = 공용 기본 이동 규칙(에디터에서 전환), 공간 규칙은 공용 수정 없이 연결, UI 입력 막기 공용화. 외줄 동작 동일 | `Systems/Control/`, `Tests/Systems/ControlRules/` + 공용: 조작 파일(meta 표) |
| 2F | 038 공용 카메라 시점 (feat/common-camera-views) | (대기) | 1인칭·3인칭 공용 시점, 시점 구역·요청 함수로 상황별 전환, 외줄 구도 공연장 이동·가림 흐리기 일반화. 외줄 화면 동일 | `Systems/Camera/`, `Tests/Systems/CameraViews/` + 공용: 카메라 리그 |
| 3 | 032 공간 연결 | CC-111 | 더미 공간 사이 장소 전환·데이터 전달(run-state.md 표), 2인 + 개발자 모드 1인으로 런 한 바퀴 | `Systems/SpaceLink/`, `Tests/Systems/SpaceLink/` |

## 공용 파일 배정 (제안, 인계 때 meta.md 소유 표에 확정)

| 파일 | 단계·소유 | 이유 |
|---|---|---|
| `Systems/Settings/GameSettings.cs`, `BaseGameSettings.cs`, 새 세팅 클래스, `Resources/Settings/GameSettings/` | 1A 028 → 2C 더미 공간(대기실·탐색·탈출로 세팅) | 세팅 틀은 한 Task가 만들고, 2단계 추가 칸은 PM이 순서를 정해 배정 |
| 옮길 Damage 파일 | 1B 033 (완료, #53) | Results는 옮기지 않음. 031이 `Systems/Results/`에 공통 결과를 새로 만들고 외줄 결과는 4단계에 교체 |
| `NetworkPlayer.cs`, `PlayerState.cs`, `PlayerCondition.cs`, `PlayerMover.cs`, `BodyDamageEffects.cs`(이전 후) | 2A 030 | 부상·생존의 중심. 다른 Task는 읽기만 |
| `NetworkSessionManager.cs` | 2D 개발자 모드 | 최소 인원 예외가 필요할 때만 |

## 위험·주의

- **1B는 공연장(@lakuase2) 파일**이다. 공간 작업자가 작업 전인 지금 PM이 먼저 옮긴다(2026-10-14). 옮긴 뒤 공간 통합 브랜치들이 dev를 받을 때 새 경로를 쓰도록 알린다.
- 외줄 실제 코드(톱날 절단 위치, 실패 후 재시작)는 4단계까지 그대로 둔다. 030이 공통 절단 함수를 바꿀 때 외줄이 쓰는 기존 함수는 남겨 깨지지 않게 한다.
- **테스트 AI 동료는 크다**: 호스트가 소유한 캐릭터가 `NetworkPlayer.Local`로 잡히는 구조(`NetworkPlayer.cs:215`)라 봇을 붙이려면 플레이어 동기화 수정이 먼저 필요하다. 2D에서 검토 결과만 남기고 구현은 이후에 고려한다.
- 이미 만들어 둔 028·030·031·032의 `plan.md`는 이전 기획(`Docs/References/game-flow.md`)과 "Day/자유시간" 용어 기준이다. 인계 전에 이 문서·최신 기획 용어(일차·쉬는 시간·밤 정비)로 갱신한다.
- 수치는 모두 임시값, 세팅 파일에 둔다. AI는 컴파일까지 검사하고 플레이 테스트는 작업자가 한다([검증 방식](../../../Harness/Project/Decisions/prototype-verification.md)).
