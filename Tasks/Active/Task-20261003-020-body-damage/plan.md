# Plan

## Context

2026-10-03 PM 규칙 변경(`Harness/Project/Decisions/tightrope-course-rules.md` #9): 톱날 = 부위 절단. 004가 신체 상태(`PlayerCondition`, `BodyPart` 비트 플래그, `PlayerControlContext.Condition`)를 준비해 두었고, 디메리트는 콘텐츠의 조작 규칙이 읽어 적용하는 구조다. 이 작업이 판정·공유·패널티·탈락을 붙인다.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.
5. 수치는 세팅 파일 칸으로(칸마다 한글 설명·단위).

세부(구현 전에 PM과 합의):

- 요청 진입점: 어떤 장애물이든 `부위 절단 요청(플레이어, BodyPart)`을 부른다(012 톱날이 첫 사용자). 호스트가 확정 — 도착 완료·사망 후·묘기 종료 후는 거절.
- 공유: 잃은 부위를 007 `NetworkPlayer`가 상태처럼 모두에게 공유하고 각 화면에서 `PlayerCondition.SetLostParts`. Player 프리팹에 `PlayerCondition` 부착.
- 패널티(외줄 조작 규칙이 `Condition`을 읽어 적용): 다리 1개당 이동 속도 배율, 팔 1개당 상호작용 패널티, 잃은 부위 1개당 균형 흔들림 배율. 값은 `TightropeSettings` 신체 손상 칸.
- 탈락: 팔 2개 또는 다리 2개 → 호스트가 `Fallen` 확정·래그돌(기존 추락 경로). 진행 판정은 기존 사망과 같다.
- 재시작(`RunRestarted`)·새 게임에서 `RestoreAll`.
- 임시 겉모습: 잃은 부위 숨김/색 표시(모델·애니메이션은 017). 디버그 키로 부위 절단 테스트.

## Files

- 수정 예정(공용, PM 배정): `Assets/Scripts/Player/`(`PlayerCondition`·균형·이동 패널티), `Assets/Scripts/Network/PlayerSync/NetworkPlayer.cs`(공유), `Assets/Resources/Prefabs/Characters/Players/`, 세팅 `TightropeSettings`
- 새로: `Assets/Scripts/Tightrope/Damage/`(제안)

## Risks

- Player 파일은 갈래 B(011·006)도 쓴다. 같은 시기에 배정되지 않게 PM이 순서를 정한다(020 → 011 권장).
- 다리 패널티와 0.5m/s 기본 이동이 겹치면 도착이 불가능해질 수 있다. 임시값은 완만하게.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트:
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
불필요하면 `불필요 — 이유`. 아래는 후보(PM 합의 전).

1. (디버그 또는 톱날로) 다리 하나가 잘리면 모두에게 잘린 모습이 보이고, 그 사람은 느려지고 균형이 더 흔들린다.
2. 팔 하나가 잘리면 상호작용 패널티가 적용된다.
3. 다리 2개(또는 팔 2개)를 잃으면 탈락·래그돌이 모두에게 보이고, 묘기 재시작 때 모든 부위가 돌아온다.
