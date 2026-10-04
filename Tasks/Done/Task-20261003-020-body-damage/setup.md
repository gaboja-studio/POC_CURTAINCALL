# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-65

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeDamage/` (TightropeDamage.unity — 005 외줄 테스트 씬 복사본, 2026-10-04. 톱날은 012 병합 전이라 디버그 절단 요청으로 시험)
- 스크립트 폴더: `Assets/Scripts/Tightrope/Damage/` (절단 요청·호스트 확정·패널티 규칙, 디버그)
- 에셋 폴더: 없음(잘린 부위 겉모습은 임시로 숨김, 모델은 017)
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다. (2026-10-04 PM 배정)

- `Assets/Scripts/Player/PlayerCondition.cs`, `Assets/Scripts/Player/BodyPart.cs` — 004가 준비만 해 둔 신체 상태(잃은 부위). 값 공유·탈락 판정 연결
- `Assets/Scripts/Player/PlayerMover.cs` — 다리 손실 이동 배율만. **011도 배정 중**(뒤쪽 착지) → 먼저 병합되는 쪽 뒤에 최신 통합을 받는다
- `Assets/Scripts/Player/PlayerBalance.cs` — 부위 손실 흔들림 배율만
- `Assets/Scripts/Player/PlayerInteraction.cs` — 팔 손실 상호작용 패널티만
- `Assets/Resources/Prefabs/Characters/Players/` — Player 프리팹에 `PlayerCondition` 부착·연결, 손상 표현(임시 부위 숨김)·디버프 연결 컴포넌트 부착(2026-10-04 PM 승인)
- `Assets/Scripts/Network/PlayerSync/NetworkPlayer.cs` — 잃은 부위 공유(호스트 확정)·사망 후 새 몸 복구만
- `Assets/Scripts/Settings/BaseGameSettings.cs`, `Assets/Scripts/Settings/TightropeSettings.cs`, `Assets/Resources/GameSettings/` — 신체 손상 칸(공통 기본값·외줄 조정)만 추가. **011·012도 TightropeSettings 칸 추가** → 먼저 병합되는 쪽 뒤에 최신 통합을 받는다

## 수정 금지

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
