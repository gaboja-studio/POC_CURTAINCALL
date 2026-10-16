# Plan

## Context

공통 시스템(런 진행·부상/생존·공연 결과·공동 자금)과 더미 공간이 각자 테스트 씬에서 만들어진다. 이 연결 Task는 더미 공간 사이를 기획의 장소 전환대로 오가며 상태가 이어지고 런 한 바퀴가 도는지 확인한다. 실제 공간 연결은 이후(4단계)다.

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Facts/run-state.md`(장소 전환·넘기는 데이터), `Harness/Project/Decisions/space-workspaces.md`

## Approach

1. 선행 Task(028·030·031·033·034·035)가 통합 브랜치에 병합된 뒤 시작한다.
2. 장소 전환 방식(씬 전환)을 제안하고 PM 확인 뒤, 더미 공간 사이 전환과 전환 때 넘기는 데이터(공연 결과·몸 상태·지닌 기물·잡혀 온 인원·탈출 인원)를 연결한다.
   - 2026-10-16 PM 결정: NGO 통째 교체(Single)와 NGO 추가 로드(Additive) 두 방식을 공통 세팅 "장소 전환"으로 전환. 기본은 통째 교체. 한 씬 배치·NGO 씬 관리 끄기는 제외.
   - 시작 씬 `SpaceLink.unity`(접속·런 시작, 추가 로드에서는 계속 떠 있는 코어 씬). 호스트(또는 오프라인 런)가 `CampaignRun.StepChanged`를 받아 장소를 정하고, 접속 중이면 NGO `NetworkSceneManager`, 오프라인이면 Unity `SceneManager`로 바꾼다.
   - 장소 대응: 공연 → DummyPerformance, 쉬는 시간·밤 정비 → DummyWaitingRoom(백스테이지로 나가면 DummyExploration), 탈출로 → DummyEscapeRoute, 도입·엔딩 → SpaceLink. 통째 교체는 모두 함께 이동한다(사람마다 다른 공간은 추가 로드에서만 가능, 더미에서는 한 번에 한 공간).
   - 런·자금·부상 관리자는 이미 씬이 바뀌어도 유지된다(DontDestroyOnLoad). 032는 전환과 전환 뒤 상태 표시·확인만 붙이고 관리자 코드는 고치지 않는다.
3. 공간 담당자 소유 파일과 실제 공간 씬은 고치지 않는다.
4. 2명 + 개발자 모드 1명으로 런 한 바퀴(1일차 → 밤 → 다음 날 → 탈출 또는 기한 초과 → 세대 교체)를 돌린다.

## Files

- 수정 예정: `Assets/Scripts/Systems/SpaceLink/`, `Assets/Scenes/Tests/Systems/SpaceLink/SpaceLink.unity`
- 배정 공용(2026-10-16 PM): `BaseGameSettings.cs`·`.asset`("장소 전환" 묶음 추가), `ProjectSettings/EditorBuildSettings.asset`(SpaceLink·더미 4개 빌드 목록 추가)
- 읽기 전용 참고: 028·030·031·033·034·035 결과물, `Assets/Scenes/Levels/`(실제 공간, 참고만)

## Risks

- 씬 전환 중 플레이어 캐릭터·세션 유지 방식은 결정이 필요하다(NGO 씬 전환 등).
- 실패 항목은 이 Task에 붙이지 말고 후속 Task로 분리한다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 더미 공간으로 런 한 바퀴 실행, Console 오류 0 기록

## 공동 테스트 항목

2026-10-16 PM 확정. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 더미 공간을 기획의 장소 전환대로 오가며 1일차부터 세대 교체 또는 탈출 엔딩까지 한 바퀴 돌 수 있다.
2. 장소를 옮겨도 잃은 팔다리·공동 자금·일차가 그대로 이어진다.
3. 2명 접속 시 모두 같은 일차·시간대·자금을 보고, 개발자 모드 1명으로도 한 바퀴가 돈다.
4. 공통 세팅 "장소 전환"을 통째 교체/추가 로드로 바꾸면 각각 위 1·2가 성립한다(추가 로드는 SpaceLink 코어 씬이 남고 공간 씬만 바뀐다).
