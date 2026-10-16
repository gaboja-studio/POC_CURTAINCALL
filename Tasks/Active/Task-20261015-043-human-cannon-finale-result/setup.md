# Setup

043은 working/Active Task이며 담당자는 @RagSpirin이다. 2026-10-15 PM의 [순차 실행 위임](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)과 2026-10-16 사용자 재개 요청에 따라 042 구역·씬을 반환받았다.
착수 HEAD0128623은 042 #111/75862ef·최신 부모204be8c·dev d8b6db2를 포함하며 로컬/원격 일치·부모 tracked tree 일치 확인. 아래 공용 파일은 사용자가 PM 수정 허용을 전달하고 전역 실패 기준 변경을 명시 요청한 범위다.

## JIRA

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-129
- 상위 범위/실행 기준: https://hrjoo122770.atlassian.net/browse/CC-124
- 2026-10-15 PM 제공 URL과 4단계 제목 확인. 인계·순차 실행 승인 완료; 구역 이관은 선행 구현 통합 후 기술 순번에 따라 수행한다.

## 작업 구역

- `Assets/Scripts/MainStage/HumanCannon/`
- `Assets/Scenes/Tests/MainStage/HumanCannon/` — 테스트 전용 Prefabs 포함.
- `Assets/Scripts/Systems/Reward/PerformanceRewards.cs` — 공용 자동 실패·정산의 최소 생존 인원.
- `Assets/Scripts/Systems/DevMode/DevMode.cs` — 변경된 생존 규칙 설명 일치.
- `Assets/Scripts/Systems/DevMode/DevModeTestUI.cs` — 테스트 표시·판정 일치.
- `Assets/Scripts/Systems/DummySpaces/Tests/PlayMode/DummyPerformancePlayTests.cs` — 실제 공용 API 회귀 검증.

## 예약 구역

- 없음 — 043 배정 완료, 044는 순번 도달 후 이관.

## 배정된 공용 파일

- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 043 단독 소유.

## 수정 금지

- `Assets/Scenes/` — 위 테스트 구역만 예외.
- `Assets/Scripts/Systems/` — 위에 명시한 공용 파일만 예외.
- `Assets/Resources/`
- `Assets/Scripts/MainStage/Tightrope/` — 원본 읽기/재사용만 허용, 수정 금지.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`
- main_stage.unity는 025 소유다. 공용 부상·관전·런 진행 코드와 게임 시작 최소 인원은 수정하지 않는다.
- 2026-10-16 사용자: PM의 공용 실패 규칙 수정 허용을 전달하고 모든 묘기의 실패 기준을 생존 2명 미만→1명 미만으로 변경하도록 지시. 이전 전역 변경 금지 문구는 이 범위에서 대체된다. 실제 목적지/공용 종료 연결 검증은 여전히 필요하다.

## 승인 및 단계 전환

- [x] @RagSpirin 담당·040~044 실제 인계·순차 실행 승인 — 2026-10-15 PM @MoHoDu
- [x] 실제 JIRA URL 확인 — CC-129 (4단계 제목 조회)
- [x] 042 #111/75862ef 통합 및 최신 부모204be8c·dev d8b6db2 이력 수신 확인 — HEAD0128623
- [x] 043에만 Active Task/Allowed/씬 소유권 이관 — 2026-10-16 기존 PM 위임
- [x] 현재 브랜치·로컬/원격·부모 tree 확인 후 implement-code 착수
- [x] 공용 실패 기준 변경 승인 기록 — 사용자 전달 PM 승인 및 명시적 전역 1명 기준 요청
- [x] 실제 목적지와 공용 결과·런 종료 연결 및 자동 검증 — 실제 LAN호스트1명 도착/전멸·정산·Night 전환, 실제4인 공동 검증 별도

순서 040→041→042→043→044. 단계 전환·소유권 이관은 이번 PM 승인 범위이며 추가 착수 승인을 기다리지 않는다.
앞 단계 실제 4인 PASS는 다음 단계 조건이 아니다. 공동 테스트는 전체 통합 후 실행하며 미실행을 PASS로 기록하지 않는다.
최신 부모 수신은 HEAD0128623에서 확인했다. start-work 스크립트는 옛 원격 scaffolded gate 때문에 실행하지 않고 승인된 전환을 로컬 문서에 기록한다. 커밋/push는 별도 요청 전까지 하지 않는다.

## 테스트 씬 처리

- 결정: 미정 — Task 종료 시 PM이 유지/삭제 결정.
- 결정자·날짜: 대기.
