# Plan

## Context

지금 접속·진행 표시는 테스트용 IMGUI다(003 `NetworkSessionTestUI`, 005 `TightropeRunDebug`). 균형 게이지(004)는 이미 UI 프리팹.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.
5. 수치는 코드·프리팹에 두지 않고 세팅 파일 칸으로(칸마다 한글 설명·단위).

세부(구현 전에 PM과 합의):

- 접속 UI → 진행 HUD → 결과 화면 순서. 모두 공개 진입점(세션 상태, `TightropeRun`, 015 결과)만 읽는다.
- UI 프리팹은 `Assets/Resources/Prefabs/UIs/`(공용, PM 배정). 폰트는 `Assets/Resources/Fonts/`(TMP 함정 주의).
- 표시 시간·경고 시점(예: 남은 30초) 등은 세팅.

## Files

- 수정 예정: `Assets/Scripts/UI/`(제안), `Assets/Resources/Prefabs/UIs/`(PM 배정)

## Risks

- TMP 동적 폰트가 저장 때 바뀔 수 있다(`Harness/Engine/Unity/Pitfalls/tmp-dynamic-font-churn.md`).

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트:
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
불필요하면 `불필요 — 이유`. 아래는 후보(PM 합의 전).

1. 방 만들기부터 게임 시작까지 정식 UI로 할 수 있다.
2. 진행 중 남은 시간·인원·내 상태가 실제와 같게 보인다.
3. 결과 화면이 모두에게 같게 보인다.
