---
summary: 외줄이 아닌 씬에서 플레이어 균형이 시작부터 켜져 있어 가만히 서 있어도 흔들리다 추락(사망)함
status: active
updated: 2026-10-14
source: Task-20261013-030
---

# 외줄 밖에서 서 있기만 해도 추락함

## 언제 읽나

외줄이 아닌 테스트 씬·공간 씬에 Player를 두었는데 아무것도 안 했는데 몇 초 뒤 래그돌로 쓰러지거나, 상태가 `Fallen`이 되어 절단·판정 테스트가 거절될 때.

## 증상

- 접속하고 10초쯤 지나면 내 캐릭터가 추락(`PlayerState.Fallen`)하고 조작이 잠긴다.
- `PlayerBalance.IsActive`·`HasFallen`이 true다.

## 원인

- 균형(`PlayerBalance`)은 외줄 전용인데, Player 프리팹의 `activeOnStart`가 켜져 있어(외줄 테스트용) 어느 씬이든 시작부터 균형이 돈다.
- 외줄에서는 `CourseControlSwitcher`가 줄 위·밖에 따라 켜고 끄지만, 외줄이 아닌 씬에는 끄는 쪽이 없다. 서 있어도 자연 흔들림이 쌓여 빨강 2초 → 추락한다.
- 037(공용 기본 이동 규칙)은 이동만 플랫폼 이동으로 바꿨고 균형은 건드리지 않았다(균형은 외줄 전용 유지 결정).

## 대처

- 외줄이 아닌 씬·공간은 내 캐릭터가 생성될 때(`NetworkPlayer.LocalSpawned`) `PlayerBalance.SetBalanceActive(false)`를 부른다. 예: `Systems/Survival/InjurySurvivalTestUI`.
- 공간 연결(032)에서 공간에 들어갈 때 균형을 끄는 공통 처리를 둘지, 프리팹 `activeOnStart`를 끌지(외줄 테스트 씬 영향 확인 필요)는 PM 결정.

## 확인 방법

Play → 호스트 접속 → 15초 대기 후 `NetworkPlayer.Local.State`가 `Normal`이고 `PlayerBalance.IsActive`가 false인지 본다.
