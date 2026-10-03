# Plan

## Context

기획자가 수치를 바꾸려면 지금은 Player·코스·NetworkManager 프리팹 여러 곳을 열어야 하고, 외줄 값이 Player 프리팹에 섞여 있다. 수치를 한 폴더로 모은다. 구성·원칙: `Harness/Project/Decisions/game-settings.md`, 수치 목록(2026-10-03 조사): `Docs/Domains/tightrope-course.md` "조정값" 표.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.
5. 수치는 코드·프리팹에 두지 않고 세팅 파일 칸으로(칸마다 한글 설명·단위).

세부(구현 전에 PM과 합의):

- 세팅 클래스(ScriptableObject)는 `Assets/Scripts/Settings/`(새 폴더, PM 생성), 에셋은 `Assets/Resources/GameSettings/`.
  - `GameSettings`(시작점, 아래 연결만) · `BaseGameSettings`(세션·캐릭터·입력·균형 공통·연출·동기화) · `TrickSettings`(묘기 공통 부모) · `TightropeSettings`(코스·진행·줄 위 동작·손잡기·목마) · `RewardTable`(추상, 015가 구현).
  - 칸마다 한글 `Tooltip`(단위 포함)과 `Header` 묶음. 톱날(012)·불(014) 칸은 해당 Task가 `TightropeSettings`에 추가한다.
- 컴포넌트는 세팅을 읽어 쓰고, 프리팹의 숫자 칸은 지운다(값을 넣는 곳은 한 곳). 디버그 키·테스트 표시·코스 임시 색은 제외.
- 플레이 중 수정이 바로 반영되게 매 프레임/사용 시점에 읽는다. 코스 모양은 재빌드 때 읽고, 온라인은 기존 `CourseShapeSync`로 호스트 값 공유.
- 옮기기 전 값 = 옮긴 후 값(위 표). 숨은 상수(줄 위 높이 허용 0.3)도 세팅으로.
- 동기화 원칙: 게임 판정에 쓰는 값은 호스트 기준(호스트가 판정하므로 자동). 소유자가 계산하는 이동·균형 값은 같은 빌드 = 같은 세팅이라 가정한다.

## Files

- 수정 예정(공용, PM 배정): `Assets/Scripts/Player/`, `Assets/Scripts/Tightrope/Rope/`, `Assets/Scripts/Network/Session/NetworkSessionManager.cs`, `Assets/Scripts/Network/PlayerSync/NetworkPlayer.cs`, Player·TightropeCourse·NetworkManager 프리팹
- 새로: `Assets/Scripts/Settings/`, `Assets/Resources/GameSettings/`

## Risks

- 여러 도메인 코드를 한 번에 고친다. 다른 Task 브랜치가 열려 있지 않을 때(지금) 해야 한다.
- 프리팹 값을 지우는 순간 값이 바뀌지 않게, 옮긴 값을 플레이로 비교한다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트:
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
불필요하면 `불필요 — 이유`. 아래는 후보(PM 합의 전).

1. 기존 값 그대로 줄 위 걷기·점프·옆줄 착지·균형 추락이 이전과 같게 느껴진다.
2. 플레이 중 세팅에서 줄 위 이동 속도·초록 범위를 바꾸면 바로 반영된다.
3. 대기 중 호스트가 줄 수·길이를 바꾸면 2명 접속 화면 모두 같은 코스로 바뀐다.
