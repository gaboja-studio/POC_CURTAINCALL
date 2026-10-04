# Task-20261003-020

- **Title:** 신체 손상(부위 절단·패널티·동기화)
- **Type:** feat
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/player-control.md`, `Docs/Domains/player-sync.md`, `Docs/Domains/tightrope-course.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-05

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/body-damage

## Goal

- 톱날(012) 등이 부위 절단을 요청하면 호스트가 확정해 모두에게 같은 잘린 부위가 보이고, 팔을 잃으면 상호작용 패널티·다리를 잃으면 이동이 느려지며 어느 쪽이든 균형이 더 흔들린다. 네 팔다리를 모두 잃으면 탈락(사망)·래그돌(팔 2개 또는 다리 2개만 결손이면 생존). 프로토타입 재시작은 전원 복구 또는 생존자 손상 유지·사망자 복구를 설정으로 선택한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 013(세팅) 병합 후 시작 — Player 스크립트·프리팹을 고치므로. 갈래 A에서 012와 함께, **012보다 먼저 병합**(012가 이 작업의 절단 요청을 부른다).
- 규칙: `Harness/Project/Decisions/body-damage.md`(게임 전체), `Harness/Project/Decisions/tightrope-course-rules.md` #9(외줄). 바탕 코드: 004 `PlayerCondition`·`BodyPart`(준비만 됨, 프리팹 미부착·공유 없음).
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Decided: 2026-10-04 @MoHoDu — 팔 손실은 목마 탑승 가능 거리 감소를 임시 규칙으로 사용. 최종 규칙·수치는 미정이며 교체 가능하게 만든다(실제 목마 판정은 006 연결 필요).
- Decided: 2026-10-04 @MoHoDu — 사망 조건을 같은 종류 2개 결손에서 네 팔다리 모두 결손으로 변경. 일반 부위 요청은 이미 잃은 부위면 같은 종류의 남은 쪽을 절단하고, 둘 다 없으면 변화 없음.
- Decided: 2026-10-04 @MoHoDu — 수평 톱은 한쪽 다리 → 남은 다리 → 팔 순서. 최초 좌우 선택·이후 팔 좌우 순서·기존 결손이 있을 때의 세부 선택은 미정.
- Decided: 2026-10-04 @MoHoDu — 공통 신체 상태와 콘텐츠별 탈부착 가능한 디버프를 분리. 정책 교체·해제 시 이전 효과 제거, 불이익은 손상 플레이어에게만 적용하고 모습·애니메이션은 다른 플레이어에게도 공유.
- Decided: 2026-10-04 @MoHoDu — 프로토타입 재시작 복구 모드는 전원 복구 / 생존자 손상 유지·사망자 복구. 기본값은 후자이며 정식 복구 규칙 변경이 아니다.
- Decided: 2026-10-04 @MoHoDu — 테스트 씬은 새 `TightropeDamage`(005 씬 복사), 012 병합 전에는 디버그 절단 요청으로 시험. 012는 `TightropeSaws.Hit`(부위·플레이어)로 절단을 요청한다
- Decided: 2026-10-04 @MoHoDu — 잘린 모습은 이 Task에서 최종 구현(017에서 가져옴): 휴머노이드 모델(BlockDoll 같은 모델)의 팔·다리가 떨어져 날아가고 떨어진 부위만 래그돌. 몸 전체 래그돌은 네 팔다리 모두 손실 또는 추락 때만. 다른 휴머노이드 모델로 교체 가능해야 함. Player 프리팹 모델을 BlockDoll로, 표현·디버프 연결 컴포넌트 추가 승인.
- Decided: 2026-10-04 @MoHoDu — 두 다리를 모두 잃으면 기어서 이동하고, 충돌 캡슐도 줄인다. 겉모습은 몸통 아래가 바닥에 닿게 내렸고 애니메이터에 LostArms·LostLegs(Int)를 넘긴다(기어가기 동작 클립은 모델 작업에서).
- Decided: 2026-10-04 @MoHoDu — 기어가기 임시값: 속도 배율 0.5(줄 위 0.25m/s), 캡슐 높이 0.9m. 점프·옆줄 이동은 가능(기획에 금지 규칙 없음, 대신 균형 불이익을 더 크게 — 외줄 디버프 정책에서). 게임 기본 세팅(신체 손상)에서 조절. 기획 공유: https://claude.ai/code/artifact/b0552187-8c6f-4c6f-9551-96541176c2cd
- Decided: 2026-10-04 @MoHoDu — 외줄 불이익 임시값: 다리 하나 이동 ×0.8, 흔들림 (1+0.25×잃은 다리)×(1+0.1×잃은 팔), 두 다리 착지 충격 ×1.5(제자리·옆줄), 팔 하나당 목마 거리 −25%. 외줄 세팅에서 조절. 규칙 문서(body-damage.md, tightrope-course-rules.md #9) 갱신.
- Open: 기어가기 최종값. 다리 없는 사람이 목마 아래층이 될 수 있는지·수직 톱날 통과 높이 — 006·012와 함께 결정(006은 쌓는 높이를 실제 캡슐 높이로 계산하도록 요청).
- Open: 좌우 선택 방식·임시 패널티 수치. 프로젝트 공통 결정 문서와 외줄 규칙 #9는 이전 사망 조건이 남아 있으므로 다음 작업에서 최신 합의를 반영한다.
- Decided: 2026-10-03 @MoHoDu — 부위 상태는 게임 전체·완전히 죽기 전까지 유지, 패널티 종류 공통·크기는 묘기마다, 이후 돈으로 부품 수리 예정(이번엔 구현 안 함, 확장 가능 구조만) — `Harness/Project/Decisions/body-damage.md`
- Decided: 2026-10-03 @MoHoDu — 톱날은 즉시 사망 대신 부위 절단(규칙 #9). 패널티 크기는 세팅 값(기획 미정)
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정
