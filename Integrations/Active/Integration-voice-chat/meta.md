# Integration-voice-chat

- **Feature:** 음성 채팅(생존자 거리 감쇠·사망자 분리)
- **Status:** preparing
- **Branch:** integration/voice-chat
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:**
- **Updated:** 2026-10-20

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 온라인 세션(묘기·대기실·백스테이지·탈출로 전 공간)에서 상시 마이크 음성 채팅을 쓴다. 생존자끼리는 거리에 따라(2m 이내 최대, 20m 밖 무음) 들리고, 사망자(추락·리타이어)끼리는 거리와 무관하게 같은 크기로 들린다. 생존자와 사망자는 서로 들리지 않는다.
- 음소거 키로 내 마이크를 끄고 켤 수 있다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261020-063 | feat | @MoHoDu | feat/voice-chat | - | assigned |

## 준비 브랜치

선택 사항. 준비 branch를 재사용할 때만 기록한다. 두 SHA는 부모 통합 이력의 전체 40자리 커밋 SHA이며 승인 SHA는 준비 기준 SHA를 포함한다. `Active Task`가 없거나 비어 있으면 기존 일반 통합 흐름이다. 값이 있으면 해당 Task 하나만 배정·착수한다. Task meta의 선택 `Previous Task`에는 실제 Task ID만 적는다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Packages/manifest.json`, `Packages/packages-lock.json` | PM(통합 준비) | Vivox(`com.unity.services.vivox` 16.12.1) 설치 — 통합 준비 커밋에서만 |
| `Assets/Scripts/Systems/Settings/BaseGameSettings.cs` | Task-20261020-063 | 공통 세팅 끝에 `음성 채팅` 묶음 추가 |
| `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset` | Task-20261020-063 | 음성 채팅 기본값(2m/20m, 음소거 키) |
| `Docs/Domains/index.md` | Task-20261020-063 | 음성 채팅 Domain Map 등록 |

## Decisions

- Open: 음소거 키 기본값(임시 `M`) — `control-keys.md` 목록 밖 키라 디스코드 `소집` 합의 필요. 세팅 값이라 코드 수정 없이 바꿀 수 있다.
- Decided (2026-10-20 PM @MoHoDu): 솔루션 Vivox(UGS Authentication 재사용) / 상시 마이크 + 음소거 키 / 생존자·사망자 완전 분리(사망자는 생존자 목소리도 못 들음) / 생존자 거리 2m(최대 음량)~20m(무음) / 사망 = `Fallen`·`Retired`(관전 규칙과 같음) / 사용 공간 제한 없음.
