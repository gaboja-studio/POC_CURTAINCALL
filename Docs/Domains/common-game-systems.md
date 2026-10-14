# 공통 게임 시스템

> 작성 중. Task-20261013-029(CC-107)에서 기존 기능 조사 결과로 채운다.

## 목적

캠페인 진행·부상/생존·공연 결과·보상·공동 자금·공간 연결처럼 여러 공간이 함께 쓰는 게임 시스템의 지도.

## 재사용 목록

| 기능 | 분류 | 기존 파일 | 근거 | 후속 Task |
|---|---|---|---|---|

분류: 재사용 / 수정 후 재사용 / 신규 / 범위 밖

## 담당 파일

- 존재: `Assets/Scripts/Systems/Damage/` (Task-20261014-033, 공연장에서 이전, 동작 동일)
  - `BodyDamageEffects.cs` — **부상 효과 진입점**. 두 다리 손실 → 기어가기(게임 공통) + 끼운 정책의 콘텐츠 불이익 배율 적용. Player 프리팹에 붙음.
  - `BodyPenaltyPolicy.cs` — 불이익 묶음(`BodyPenalty`)과 콘텐츠별 정책 틀(ScriptableObject).
  - `LimbDismemberment.cs` — 팔다리 절단 연출(모든 화면). `ModelLimbs.cs` — 모델의 팔·다리 위치 표시.
- planned: `Assets/Scripts/Systems/Campaign/` (Task-20261013-028)

## 진입점

- 부상: `effects.SetPolicy(policy)`(묘기가 정책 교체, null = 콘텐츠 불이익 없음), `effects.IsCrawling`, `effects.Current`. 시작 정책이 비면 `BodyDamageEffects.DefaultPolicyFactory`(콘텐츠가 게임 시작 전에 등록, 지금은 외줄 `TightropeBodyPenalty`).

## 공간에서 쓰는 법 (부상)

- 공간 Task는 `Systems/Damage/`를 **읽고 부르기만** 한다. 고칠 일이 있으면 PM에게 요청한다(`space-workspaces.md`).
- Player 프리팹에 이미 붙어 있어 어느 공간이든 Player만 두면 절단 연출·기어가기가 동작한다. 절단 요청은 `NetworkPlayer.RequestCutPart`(소유자)·`ServerCutPart`(호스트), 상태는 `PlayerCondition.LostParts`·`Changed`.
- 공간 전용 불이익은 내 공간 스크립트 폴더에 `BodyPenaltyPolicy`를 상속해 만들고, 공간에 들어올 때 `GetComponent<BodyDamageEffects>().SetPolicy(내 정책)`, 불이익이 없으면 `SetPolicy(null)`.
- ⚠ 지금은 어느 공간이든 기본 정책이 외줄 불이익이다(옮기기 전과 같음). 공간별 기본값은 030(부상·생존)에서 정한다.
- 새 모델은 `ModelLimbs`로 팔·다리 위치를 지정(휴머노이드는 생략 가능).

## 의존 도메인

- [network-session.md](network-session.md) — 방 상태(대기/진행/종료)는 캠페인 단계와 별개
- [player-sync.md](player-sync.md) — 플레이어 상태·부상 동기화

## 수정 주의점

- 공간(공연장·대기실·백스테이지·탈출로) 내부 기능은 각 담당자 소유. 연결은 연결 Task(CC-111)에서 다룬다.
- 보상 수치·호응 산정·하루 공연 수 등은 미확정 게임 규칙이다.
- `Systems/Damage/`는 공연장 코드(`MainStage/`)를 부르지 않는다. 묘기 전용 정책은 묘기 쪽이 `SetPolicy` 또는 `DefaultPolicyFactory`로 끼운다.
