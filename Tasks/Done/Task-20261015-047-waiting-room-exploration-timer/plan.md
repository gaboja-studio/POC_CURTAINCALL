# Plan

## Context

CC-132 개발 순서의 이 단계만 구현한다. 2026-10-15 PM 결정으로 앞 단계 완료를 기다리지 않고 착수한다. 단계 전용 씬에서 독립 검증하고, 앞 단계 기능이 필요하면 통합 병합분을 받아 쓴다.

## Approach

1. setup 구역(단계 전용 스크립트 폴더·테스트 씬) 안에서 구현한다. 1단계 코드(`Assets/Scripts/WaitingRoom/Base/`)는 수정하지 않는다.
2. 아래 공동 테스트 항목을 완료 기준으로 삼고, 범위 밖 기능은 추가하지 않는다.
3. 공용 인터페이스가 부족하면 직접 고치지 않고 PM에게 별도 소유 Task를 요청한다.

### 1차: 첫 퇴장 300초·우측 하단 표시·경보/방송 (2026-10-16 초안, 합의 전)

확인된 근거(코드, 2026-10-16):
- 공용 `CampaignRun`(`Assets/Scripts/Systems/Campaign/`)이 이미 판정·동기화를 한다. B안(`RestTimerState.WaitingForExit`) → `NotifyLeftWaitingRoom()`(호스트) → `Running`, 길이 `BaseGameSettings.Run.RestDuration`(300)·가이드 쉬는 시간 `GuideRestDuration`(300), 남은 시간을 호스트가 1초마다 보내고 받는 쪽은 그 사이를 자체 감산한다.
- 60초 소집 경보: `RecallAlarmLeadTime`(60) → 모든 화면에 `CampaignRun.RecallAlarm` 이벤트가 이미 있다.
- 없는 것: 퇴장 감지(누가 `NotifyLeftWaitingRoom()`을 부를지), 30·10초 방송 신호, 우측 하단 표시, 종소리·방송 연출. → 이 Task 구역에서 만든다. `Systems/`는 고치지 않는다.
- 대기실 문 위치: 1단계 그레이박스(`WaitingRoomGreyboxBuilder`, 루트 (0,0,70)) 기준 로비 ↔ ⑥ 문은 로컬 x=+10. ⑥ 통로(x 10~37)부터 백스테이지.

만들 것(`Assets/Scripts/WaitingRoom/ExplorationTimer/`):
1. `WaitingRoomExitWatch` — 판정 화면(호스트·오프라인 런)만 동작. 대기실 영역(BoxCollider) 안에서 **보였던** 살아 있는 플레이어가 영역 밖(백스테이지)으로 나가면 쉬는 시간마다 한 번 `NotifyLeftWaitingRoom()`. 1단계 `StageEntranceGather`와 같은 위치 판정 방식(원격 캐릭터는 동기화된 위치). 나간 플레이어 목록은 다음 항목(재진입 불가)에 쓰도록 공개 읽기 값으로 둔다. A안이면 공용 쪽이 무시한다.
2. `Logic/ExplorationTimerCues`(순수 C#, 전용 asmdef) — 직전·현재 남은 시간과 타이머 상태로 "30초 방송·10초 방송" 발생을 판정. 쉬는 시간마다 한 번, 받는 쪽 보정으로 남은 시간이 살짝 늘어나도 중복 발생하지 않게 한다. 60초는 공용 `RecallAlarm`을 그대로 쓴다.
3. `ExplorationTimerAnnouncer` — 모든 화면. `RecallAlarm` → 소집 경보(종소리+방송), Cues → 30·10초 방송. 연출 화면이 구독할 로컬 이벤트로 내보낸다.
4. `ExplorationTimerView` — uGUI 우측 하단 시계 표시(mm:ss)·방송 문구 띠·종소리 AudioSource. 쉬는 시간이 아니면 숨김.
5. EditMode 테스트(`Tests/EditMode/`, 전용 asmdef): Cues 판정(문턱 통과·중복 방지·쉬는 시간 바뀜 초기화).

테스트 씬 구성(이 Task의 배정 씬만):
- 지금 씬은 맵 확인용 복사본(Player·TempFloor·PlaySetup·카메라·조명)뿐이라 4인 검증이 안 된다. 1단계 메뉴 `Tools/CurtainCall/대기실/그레이박스 다시 만들기`를 이 씬에서 실행해 같은 배치를 만들고(1단계 코드는 수정하지 않고 실행만), 세션 테스트 UI·`CampaignFlowTestUI`·카메라(1단계와 같은 구성)를 놓는다. 장소 전환 없이 이 씬에서 방 만들기 → 런 시작 → 디버그 버튼으로 쉬는 시간까지 넘긴다.

연출 결정(2026-10-16 대기실 담당 @gyumin9048-ctrl, 임시 연출):
- 시간 표시: 우측 하단 원형 시계 아이콘(남은 비율 채움) + mm:ss. 아무도 안 나가 멈춘 동안에도 5:00으로 보인다.
- 방송: 우측 하단 시계 옆 짧은 문구(60·30·10초). 음성 에셋 없음 → 글자만.
- 종소리: 코드로 만든 임시 '딩동' AudioClip. 진짜 에셋으로 갈아 끼울 AudioClip 칸을 둔다.
- 테스트 씬: 1단계 그레이박스 메뉴로 이 씬에 재생성.

나중 항목(이번 1차 아님): 재진입 불가·전원 ⑥ 조기 시작(1단계 `StageEntranceGather` 재사용)·0초 추격.

## Files

- 수정 예정: `Assets/Scripts/WaitingRoom/ExplorationTimer/`, `Assets/Scenes/Tests/WaitingRoom/ExplorationTimer/`(WaitingRoomExplorationTimer.unity 포함), 이 Task 문서.
- 읽기 전용: `Harness/Project/Decisions/Waiting-room/exploration-time-limit.md`, `Harness/Project/Facts/game-flow.md`, 공용 Systems.

## Risks

- 미정 항목을 AI가 임의로 확정하지 않는다(meta Open).
- 4인 동기화 결과가 화면마다 다를 위험.
- 단계 동시 진행으로 통합 병합 때 다른 단계·1단계 루프와 연결·충돌이 생길 수 있다. 병합 순서는 PM이 정한다.
- 공용 `RunState.Tick`은 0초가 되면 바로 공연으로 넘긴다. 3번째 항목(0초 추격·정리 뒤 시작)과 충돌하므로 그때 공용 담당(시스템 @MoHoDu)에게 "0초 뒤 정리 대기" 신호 추가를 별도 소유 Task로 요청해야 한다. 1차에는 영향 없음.
- 30·10초 문턱은 공용 세팅에 칸이 없다. 1차는 컴포넌트 값으로 두고, 세팅 이전은 공용 담당에게 요청한다.
- 받는 쪽 남은 시간은 호스트 1초 전송 사이 자체 감산이라 화면 간 표시가 최대 지연 시간만큼 어긋날 수 있다.

## Auto Verification

- 구역 → 문서 → 컴파일 → 자동 테스트: `pwsh -File Scripts/check-work.ps1 -Mode PlayMode`.
- 1차 EditMode 테스트: `ExplorationTimerCues` 판정.
- PlayMode 테스트가 없으면 NO_PROJECT_TESTS. 생략·환경 차단은 handoff에 기록한다.

## 공동 테스트 항목

CC-132 단계 기준. 실행 씬: `Assets/Scenes/Tests/WaitingRoom/ExplorationTimer/WaitingRoomExplorationTimer.unity`. 4인(호스트 1·참가자 3). handoff에 일시·참여자·씬·재현·예상·실제를 기록한다. 현재 전부 미실행이다.

1. 조기 시작: 전원 ⑥ 진입. 예상: 남은 시간과 무관하게 네 화면 동시에 공연 시작.
2. 늦은 인원 정리: 0초까지 1명 미도착 → 추격자 방출 → 도착 또는 잡힘. 예상: 정리 뒤에만 공연 시작, 잡힌 인원은 외팔로 합류.
3. 표시·경보: 첫 퇴장 순간 300초 시작, 60초 전 소집 경보(종소리·방송), 30·10초 전 방송, 나간 인원 대기실 재진입 불가. 예상: 네 화면 시간·경보·방송 일치.
