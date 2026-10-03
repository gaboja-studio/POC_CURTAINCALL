# 외줄타기 프로토타입 작업 계획

목표: 4명이 온라인으로 모여 **외줄 묘기 플로우**(대기 → 묘기 시작 → 톱날·불을 피해 100m 코스 → 도착 클리어/실패 재시작 → 결과·보상)를 해 볼 수 있게 만든다.
원문: [브리프](../../../Docs/References/tightrope-prototype-brief.md) · 결정: [멀티플레이 구성](../../../Harness/Project/Decisions/multiplayer-stack.md), [배치 위치](../../../Harness/Project/Decisions/asset-placement.md), [검증 방식](../../../Harness/Project/Decisions/prototype-verification.md), [조정값 세팅](../../../Harness/Project/Decisions/game-settings.md) · 묘기 전체 설명: [외줄 묘기 도메인](../../../Docs/Domains/tightrope-course.md)

## 정해진 것 (PM)

1. 멀티플레이는 **NGO + Unity Multiplayer Services**, 호스트가 방을 여는 방식. 호스트가 나가면 게임 끝.
2. **내 캐릭터 움직임은 내 컴퓨터가**, 죽음·목마·게임 진행 판정은 **호스트가** 정한다.
3. 패키지는 PM이 이 브랜치에 먼저 설치한다. 불러와 쓰는 에셋은 `Assets/Resources/` 아래.
4. AI는 **컴파일만** 검사한다. 플레이 테스트는 **작업자가 하고 결과를 알려 준다.** 음성 채팅·런 기록 저장은 하지 않는다. 4명 기준.
5. 2026-10-02: 007 이후 기능 작업은 **기능과 온라인 동기화를 같이** 만든다([결정](../../../Harness/Project/Decisions/feature-with-network.md)).
6. 2026-10-03: 외줄 규칙은 [코스·진행 결정](../../../Harness/Project/Decisions/tightrope-course-rules.md)(4명·5분·100m·4줄·1명 도착 즉시 클리어·톱날)과 [조작·목마 결정](../../../Harness/Project/Decisions/tightrope-rules.md)을 따른다.
7. **2026-10-03 작업 재편**: 남은 일을 "게임 규칙 먼저, 꾸미기 나중"으로 단계를 나누고, 단계마다 두 갈래(A 방해물·결과 / B 사람 움직임·겉모습)로 동시에 진행한다. 008 도구 동기화는 **보류**(외줄 규칙에 도구 없음). 011은 남은 4개로 축소.
8. **2026-10-03 조정값**: 기획자가 바꿀 수치는 모두 `Assets/Resources/GameSettings/`의 세팅 파일(게임 기본 1개 + 묘기별 1개 + 교체형 보상)에 둔다. 기획 값이 없는 것(불 등)은 임시값을 넣고 에디터에서 바꾼다.
9. 아트·소리 파일은 2026-10-03 기준 없다. 모델·맵·음향 작업(017·018·019)은 **에셋 대기**.

## 단계와 순서 (쉬운 설명)

```
끝남   003 방 만들기 · 004 플레이어 조작 · 007 플레이어 동기화 · 005 외줄 코스·진행
0단계  013 조정값 세팅 정리 ← 다음 작업. 두 갈래가 같은 코드를 쓰므로 먼저 끝낸다
1단계  A: 012 톱날 → 014 불(추격 불·불타는 구간)
       B: 011 옆줄 판정·손잡기 → 006 목마 → 010 목마 예외
       ★ 합치기 ①  게임 규칙 완성 (톱날·불·목마를 한 코스에서 시험, 수직 톱날 버튼 + 목마 확인)
2단계  A: 015 결과·보상(임시, 교체형) → 016 UI 고도화
       B: 017 캐릭터 모델·애니메이션 (에셋 대기)
3단계  A: 018 맵 에셋 (에셋 대기)     B: 019 음향·VFX (에셋 대기)
       ★ 합치기 ②  보이고 들리는 것 완성
4단계  009 외줄 묘기 플로우 최종 씬 → 4명 최종 테스트 → dev 반영
보류   008 도구 동기화
```

- 갈래끼리 작업 폴더가 겹치지 않는다. 공용 파일(아래 표)은 한 번에 한 작업에만 PM이 배정한다.
- 갈래 B가 작업 수가 많아 1단계 일정을 정한다. 에셋이 늦으면 2·3단계 B는 A 뒤로 미루고 4단계는 준비된 만큼 넣는다.
- 병합 순서: 단계 안에서는 끝난 순서대로, 다음 단계는 앞 단계 합치기가 끝난 뒤 시작. 남은 PR은 병합마다 최신 통합 브랜치를 merge로 받는다(rebase·force 금지).

## 남은 작업과 작업 공간

PM이 Unity에서 폴더·테스트 씬을 만들고 각 Task `setup.md`에 적는다. 스크립트 폴더는 제안이며 인계 때 확정.

| 단계 | Task | 할 일 | 스크립트 (제안) | 테스트 씬 |
|---|---|---|---|---|
| 0 | 013 조정값 세팅 | 수치를 세팅 파일로 모으기, 컴포넌트가 세팅을 읽게 | `Assets/Scripts/Settings/` + 이어받기: Player, Tightrope/Rope, Network 일부 | 기존 `Tests/Tightrope/` 사용 |
| 1A | 012 톱날 | 가로 22개·수직 톱날·버튼, 닿으면 사망 | `Assets/Scripts/Tightrope/Saws/` | `Assets/Scenes/Tests/TightropeSaws/` |
| 1A | 014 불 | 묘기 시작 후 추격 불, 시간에 따라 불타는 줄 구간 | `Assets/Scripts/Tightrope/Fire/` | `Assets/Scenes/Tests/TightropeFire/` |
| 1B | 011 외줄 동작 마무리 | 옆줄 점프 판정 연결, 뒤쪽 착지, 손잡기, 테스트 부품 정리 | `Assets/Scripts/Tightrope/Rider/` | `Assets/Scenes/Tests/TightropeRider/` |
| 1B | 006 목마 | 올라타기·내리기·목마 점프·합체 착지 (+호스트 판정) | `Assets/Scripts/Tightrope/Piggyback/` | `Assets/Scenes/Tests/Piggyback/` |
| 1B | 010 목마 예외 | 동시 탑승, 목마 중 추락·연쇄 추락, 호스트 이탈 | `Assets/Scripts/Connect/PiggybackNetwork/` | `Assets/Scenes/Tests/PiggybackNetwork/` |
| 2A | 015 결과·보상 | 결과 확정·공유, 교체형 보상(`RewardTable`) | `Assets/Scripts/Tightrope/Result/` | `Assets/Scenes/Tests/TightropeResult/` |
| 2A | 016 UI | 접속·진행 HUD·결과 화면 정식 UI | `Assets/Scripts/UI/` | `Assets/Scenes/Tests/TightropeUI/` |
| 2B | 017 모델·애니메이션 | 모델 교체, 동작 애니메이션 | `Assets/Scripts/Player/` 일부(PM 배정) | `Assets/Scenes/Tests/CharacterVisual/` |
| 3A | 018 맵 | 판정과 분리된 맵 아트 | 필요할 때만 | `Assets/Scenes/Tests/TightropeMap/` |
| 3B | 019 음향·VFX | 사건 신호에 소리·효과 | `Assets/Scripts/Presentation/` | `Assets/Scenes/Tests/TightropeAudioVfx/` |
| 4 | 009 최종 씬 | 묘기 플로우 전체를 한 씬으로, 남은 연결·버그 | `Assets/Scripts/Connect/TightropeNetwork/` (필요할 때만) | `Assets/Scenes/Tests/TightropeNetwork/` |

- 코드는 모두 `Assets/Scripts/`, 씬은 모두 `Assets/Scenes/`. 테스트 전용 프리팹은 테스트 씬 옆 `Prefabs/`.
- 모든 작업 수정 금지: `Assets/Scenes/`(자기 `Tests/<작업>/` 제외), `ProjectSettings/`, `Packages/`, `Assets/InputSystem_Actions.inputactions`, `Assets/Settings/`(URP 설정. 게임 세팅은 `Assets/Resources/GameSettings/`)

## 함께 쓰는 파일 (한 파일 = 한 작업만 수정)

| 파일 | 담당 |
|---|---|
| `Assets/Resources/GameSettings/`, `Assets/Scripts/Settings/` | 013이 만든다 → 이후 각 Task가 **자기 칸만 추가**(012 톱날, 014 불, 015 보상 등). 같은 세팅 파일을 두 Task가 동시에 고치지 않게 PM이 순서 배정 |
| `Assets/Resources/Prefabs/Controllers/Network/` | 003 → 007 → 013(값 이동) → PM 배정 |
| `Assets/Resources/Input/`, `Assets/Resources/Prefabs/Characters/Players/` | 004 → 007 → 013 → 갈래 B 순서대로(011·006·010·017) PM 배정 |
| `Assets/Resources/Prefabs/UIs/`, `Assets/Resources/Fonts/` | 004 → 007 → 015·016 PM 배정 (`Assets/TextMesh Pro/` 수정 금지) |
| `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | 005 → 013(값 이동) → 018(맵). 다른 작업은 씬에 놓기만 |
| `Assets/Resources/Prefabs/Controllers/Camera/` | 007 → 이후 PM 배정 |
| `Assets/Scripts/Network/PlayerSync/` | 007. 이후 공개 기능만 사용, 고칠 때 PM 배정(`TestLaneLanding.cs`는 011이 삭제) |
| `Assets/Resources/Prefabs/Objects/Tools/` | 008 (보류) |

## 확인 방법

- 작업자: "검사해줘" → 컴파일 확인. 플레이 확인 항목은 AI가 목록으로 주고, **작업자가 직접 해 보고 결과를 답한다.**
- 합치기 ①: 가로 톱날을 점프로, 수직 톱날을 옆줄 이동 또는 목마 2층 + 버튼으로 피할 수 있는가 / 불에 닿거나 불타는 구간 때문에 옆줄 이동이 막히는가 / 목마가 모두에게 같게 보이는가.
- 최종(4명): 방에 모여 시작 → 묘기 시작 후 톱날·불 → 한 명 도착 시 클리어·결과·보상 / 전원 사망·시간 종료 시 실패·재시작 / 호스트가 나가면 끝 — 모두에게 같게 보이는가. 세팅 파일 값 변경이 반영되는가.

## PM이 할 일

1. 013 인계: `Assets/Scripts/Settings/`·`Assets/Resources/GameSettings/` 폴더를 Unity에서 만들고 `setup.md`·공동 테스트 항목 확정.
2. 1단계 인계: 012·014·011의 폴더·테스트 씬 생성, 012 버튼 형태·014 불타는 구간 처리 같은 Open 결정.
3. 아트·소리 출처와 일정 정하기(017·018·019 시작 조건).
