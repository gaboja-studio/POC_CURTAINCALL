---
summary: 기획 조정값은 Assets/Resources/GameSettings/ 한 폴더 — GameSettings(시작점) + BaseGameSettings(게임 기본) + Tricks/<묘기>Settings + 교체형 보상(RewardTable). 칸마다 한글 설명·단위, 플레이 중 수정 반영, 판정 값은 호스트 기준
status: active
updated: 2026-10-03
source: human (2026-10-03 PM @MoHoDu)
---

# 기획 조정값 세팅 파일

## 언제 읽나

기획자가 바꿀 수 있는 수치(속도·크기·시간·확률·보상 등)를 새로 만들거나 옮길 때. 지금 어떤 값이 어디 있는지는 `Docs/Domains/tightrope-course.md` "조정값" 표.

## 결정

- 2026-10-03 / PM(@MoHoDu): 조정값은 코드·프리팹 칸이 아니라 세팅 파일(ScriptableObject) 한 폴더에서 관리한다.
  1. 위치: 에셋 `Assets/Resources/GameSettings/`, 클래스 `Assets/Scripts/Settings/`. (`Assets/Settings/`는 URP 설정이라 쓰지 않는다)
  2. 구성: `GameSettings.asset`(시작점, 아래를 연결만) / `BaseGameSettings.asset`(세션·캐릭터·입력·균형 공통·신체 손상 기본·연출·소리) / `Tricks/<묘기>Settings.asset`(묘기별, 예: `TightropeSettings` = 코스·진행·줄 위 동작·손잡기·목마·톱날·불·보상 연결) / `Tricks/Rewards/*.asset`(보상).
  3. 보상은 나중에 바뀌므로 **교체형**: 추상 `RewardTable`을 구현한 파일을 묘기 세팅의 보상 칸에 끼운다. 규칙을 바꾸려면 새 파일을 만들어 갈아 끼운다(코드 수정 없이).
  4. 칸마다 한글 `Tooltip`(단위 포함)과 `Header` 묶음. 기획 값이 없는 칸은 임시값 + Tooltip에 "기획 미정".
  5. 값을 넣는 곳은 한 곳: 세팅으로 옮긴 값은 컴포넌트·프리팹 칸에서 지운다. 디버그 키·테스트 표시·임시 색은 세팅에 넣지 않는다.
  6. 플레이 중 수정은 바로 반영(사용 시점에 읽는다). 코스 모양처럼 시작 때 만드는 값은 재빌드·재시작 때.
  7. 온라인: 판정에 쓰는 값은 호스트가 판정하므로 호스트 기준. 코스 모양은 `CourseShapeSync`로 공유. 소유자가 계산하는 이동·균형 값은 같은 빌드 = 같은 세팅으로 본다.
  8. 새 기능 Task는 자기 칸을 해당 세팅 파일에 추가한다. 같은 세팅 파일을 동시에 두 Task가 고치지 않게 PM이 순서를 배정한다.

## 이유

기획자가 수치를 바꾸려면 Player·코스·NetworkManager 프리팹 여러 곳을 열어야 했고, 외줄 값이 Player 프리팹에 섞여 있었다. 묘기가 늘어나도 "게임 기본 1 + 묘기별 1"로 찾기 쉽게 한다.

## 바꾸려면

PM 결정이 필요하다. 이 파일, `Docs/Domains/tightrope-course.md` 조정값 표, Integration `plan.md`를 함께 고친다.
