# Plan

## Context

2026-10-03 PM 규칙 변경(`Harness/Project/Decisions/tightrope-course-rules.md` #9): 톱날 = 부위 절단. 004가 신체 상태(`PlayerCondition`, `BodyPart` 비트 플래그, `PlayerControlContext.Condition`)를 준비해 두었고, 디메리트는 콘텐츠의 조작 규칙이 읽어 적용하는 구조다. 이 작업이 판정·공유·패널티·탈락을 붙인다. 게임 전체 규칙: `Harness/Project/Decisions/body-damage.md` — 묘기 하나에 묶이지 않는 공통 신체 상태로 만든다.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.
5. 수치는 세팅 파일 칸으로(칸마다 한글 설명·단위).

세부(구현 전에 PM과 합의):

- 요청 진입점: 어떤 장애물이든 `부위 절단 요청(플레이어, BodyPart)`을 부른다(012 톱날이 첫 사용자). 호스트가 확정 — 도착 완료·사망 후·묘기 종료 후는 거절.
- 공유: 잃은 부위를 007 `NetworkPlayer`가 상태처럼 모두에게 공유하고 각 화면에서 `PlayerCondition.SetLostParts`. Player 프리팹에 `PlayerCondition` 부착.
- 패널티: 종류는 공통(팔 → 상호작용, 다리 → 이동 속도, 균형 있는 묘기는 흔들림). 기본값은 `BaseGameSettings` 신체 손상 칸, 외줄 조정값은 `TightropeSettings`. 적용은 각 묘기 조작 규칙이 `Condition`을 읽어서(외줄 = 이 작업).
- 탈락: 팔 2개 또는 다리 2개 → 호스트가 `Fallen` 확정·래그돌(기존 추락 경로). 진행 판정은 기존 사망과 같다.
- **유지·초기화**: 잃은 부위는 묘기 재시작(`RunRestarted`)에도 유지. 그 플레이어가 사망하면(같은 종류 2개 손실·추락 등) 다음 재시작 때 새 몸(`RestoreAll`).
- **확장 자리(구현 안 함)**: 이후 수리 = 돈으로 부품을 붙여 대체. 부위 상태를 "있음 / 잃음" 외에 "부품으로 대체(종류)"를 더할 수 있게 진입점 이름·공유 형식을 잡아 둔다(예: 잃은 부위 비트 + 대체 부위 비트를 따로).
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
불필요하면 `불필요 — 이유`. 2026-10-04 PM 합의(후보 3개 확정).

1. (디버그 또는 톱날로) 다리 하나가 잘리면 모두에게 잘린 모습이 보이고, 그 사람은 느려지고 균형이 더 흔들린다.
2. 팔 하나가 잘리면 상호작용 패널티가 적용된다.
3. 다리 2개(또는 팔 2개)를 잃으면 탈락·래그돌이 모두에게 보인다. 묘기 재시작 때 살아 있던 사람의 잃은 부위는 그대로, 사망한 사람은 온전한 몸으로 돌아온다.
