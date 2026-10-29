---
summary: 기획 조정값은 Assets/Resources/Settings/GameSettings/ 한 폴더 — GameSettings(시작점) + 공통·묘기별·대기실·탐색·탈출로 5묶음(2026-10-14) + Tricks/<묘기>Settings + 교체형 보상(RewardTable). 칸마다 한글 설명·단위, 플레이 중 수정 반영, 판정 값은 호스트 기준. 값 변경은 모든 작업자, 연결 기능 확장은 현재 Task 관련만(2026-10-16)
status: active
updated: 2026-10-28
source: human (2026-10-03·2026-10-14·2026-10-16 PM @MoHoDu; Confluence 27721807 M2 변경점(2026-10-27), Task-20261027-076); 2026-10-28 사용자 배낭·외팔 슬롯 감소 삭제 결정
---

# 기획 조정값 세팅 파일

## 언제 읽나

기획자가 바꿀 수 있는 수치(속도·크기·시간·확률·보상 등)를 새로 만들거나 옮길 때. 지금 어떤 값이 어디 있는지는 `Docs/Domains/tightrope-course.md` "조정값" 표.

## 결정

- 2026-10-03 / PM(@MoHoDu): 조정값은 코드·프리팹 칸이 아니라 세팅 파일(ScriptableObject) 한 폴더에서 관리한다.
  1. 위치: 에셋 `Assets/Resources/Settings/GameSettings/`, 클래스 `Assets/Scripts/Systems/Settings/`. (`Assets/Settings/`는 URP 설정이라 쓰지 않는다)
  2. 구성: `GameSettings.asset`(시작점, 아래를 연결만) / `BaseGameSettings.asset`(세션·캐릭터·입력·균형 공통·신체 손상 기본·연출·소리) / `Tricks/<묘기>Settings.asset`(묘기별, 예: `TightropeSettings` = 코스·진행·줄 위 동작·손잡기·목마·톱날·불·보상 연결) / `Tricks/Rewards/*.asset`(보상).
  3. 보상은 나중에 바뀌므로 **교체형**: 추상 `RewardTable`을 구현한 파일을 묘기 세팅의 보상 칸에 끼운다. 규칙을 바꾸려면 새 파일을 만들어 갈아 끼운다(코드 수정 없이).
  4. 칸마다 한글 `Tooltip`(단위 포함)과 `Header` 묶음. 기획 값이 없는 칸은 임시값 + Tooltip에 "기획 미정".
  5. 값을 넣는 곳은 한 곳: 세팅으로 옮긴 값은 컴포넌트·프리팹 칸에서 지운다. 디버그 키·테스트 표시·임시 색은 세팅에 넣지 않는다.
  6. 플레이 중 수정은 바로 반영(사용 시점에 읽는다). 코스 모양처럼 시작 때 만드는 값은 재빌드·재시작 때.
  7. 온라인: 판정에 쓰는 값은 호스트가 판정하므로 호스트 기준. 코스 모양은 `CourseShapeSync`로 공유. 소유자가 계산하는 이동·균형 값은 같은 빌드 = 같은 세팅으로 본다.
  8. 새 기능 Task는 자기 칸을 해당 세팅 파일에 추가한다. 같은 세팅 파일을 동시에 두 Task가 고치지 않게 PM이 순서를 배정한다.

- 2026-10-14 / PM(@MoHoDu): 세팅을 **공통 | 각 묘기 | 대기실 | 탐색 | 탈출로** 5묶음으로 나누고 각각 따로 설정한다. 지금의 `BaseGameSettings`는 공통, `Tricks/<묘기>Settings`는 묘기별로 이어진다. 대기실·탐색·탈출로 세팅 파일은 공통 게임 시스템 Task(CC-108~111)가 만든다.
  - 공통에 둘 값(PM 지정): 기물 소지 개수 — 팔 개수와 관계없이 기본 2칸(2026-10-28 사용자 결정으로 배낭과 외팔 슬롯 감소 삭제, [injury-rules.md](injury-rules.md) #8). 나머지 값의 묶음 배치는 구현 Task가 제안하고 PM이 확인한다. M2(2026-10-27)에서 탐색 제한 시간 A/B 선택은 삭제됐다(쉬는 시간 1회 5분은 세팅 값 유지, [run-rules.md](run-rules.md) #1). 구현은 아직 공통 세팅 `restTimerMode` A/B 칸이 있다(기본 B, M2 미반영).

- 2026-10-16 / PM(@MoHoDu): 모든 작업자가 `Assets/Resources/Settings/GameSettings/`의 **값**을 바꿀 수 있다(`setup.md`와 무관). 세팅과 연결된 칸·기능을 추가·고도화하는 것(`Assets/Scripts/Systems/Settings/` 포함)은 **현재 Task 내용과 관련된 것**이면 허용한다. 관련 없는 세팅 확장은 PM에게 요청한다. 같은 세팅 파일을 다른 Task가 고치고 있으면 병합 충돌이 날 수 있으니 담당자에게 알린다.

## 이유

기획자가 수치를 바꾸려면 Player·코스·NetworkManager 프리팹 여러 곳을 열어야 했고, 외줄 값이 Player 프리팹에 섞여 있었다. 묘기가 늘어나도 "게임 기본 1 + 묘기별 1"로 찾기 쉽게 한다.

## 바꾸려면

PM 결정이 필요하다. 이 파일, `Docs/Domains/tightrope-course.md` 조정값 표, Integration `plan.md`를 함께 고친다.
