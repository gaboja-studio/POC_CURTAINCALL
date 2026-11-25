---
summary: 대기실인데 목마 침대 해제·앉은 목마 같은 대기실 전용 규칙이 안 걸릴 때 — 연습실 자유 연습 외줄(TightropeCourse)이 낮·밤 내내 켜져 InWaitingRoom이 거짓
status: active
updated: 2026-11-26
source: Task-20261126-204-swing-piggyback-fixes
---

# 연습실 외줄 때문에 대기실 판정이 거짓

## 언제 읽나

대기실에서만 적용해야 할 목마·바닥 규칙이 첫 리허설 이후 갑자기 안 걸릴 때.
`TightropeCourse.Current.IsGameplayActive`로 "외줄 위인지"를 판단하는 코드를 고칠 때.

## 증상

- 목마 상태에서 침대에 누워도 목마가 풀리지 않는다(위 사람이 누운 경우·아래 사람이 누워 위 사람이 공중에 뜬 경우).
- 첫 외줄 리허설 전에는 정상이라 초기 교육만 확인하면 놓친다.

## 원인

`PracticeRoomFreePlay`의 외줄 코스는 `TightropeCampaignBridge`가 없어 켜져 있으면 `IsGameplayActive`가 항상 참이다.
첫 리허설 이후 런이 진행 중이면 낮·밤 모두 켜진다. `PiggybackSystem.InWaitingRoom`은 씬 이름과 이 값을 함께 봐서 대기실 전체에서 거짓이 된다.

## 대처

- 외줄 위에서 일어날 수 없는 상태(침대·포획)는 씬 이름(`WaitingRoomScenes.IsWaitingRoom`)만으로 판단한다(`PiggybackSystem.LeavesStackNow`).
- 외줄 규칙과 겹칠 수 있는 상태(사망 → 연쇄 추락)만 기존 `InWaitingRoom`을 쓴다.
- 플레이어 단위 판정이 필요하면 `TightropeCourse.UsesRopeRules(player)`처럼 그 사람이 줄 위인지 본다.

## 확인 방법

EditMode `PiggybackDismountAndBedTests.LyingDownReleasesEvenWhileThePracticeRopeCourseIsOpen` — 공연 연결 없는 코스를 켠 상태에서 `InWaitingRoom`이 거짓임을 먼저 확인하고 침대 해제를 검사한다.
