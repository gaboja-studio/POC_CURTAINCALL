# Integration-common-game-systems

- **Feature:** 공통 게임 시스템
- **Status:** preparing
- **Branch:** integration/common-game-systems
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 미정 (첫 Task 완료 후 결정)
- **Updated:** 2026-10-14

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 런(1~3일차 + 4일차 아침)의 공연·쉬는 시간·밤 정비 진행, 부상·생존, 공연 결과·공동 자금, 장소 전환을 공통 시스템으로 만들고 더미 공간으로 런 한 바퀴를 검증한다. 실제 공간 교체는 이후.
- 작업 순서·병렬 구성: `plan.md` (Task-029에서 작성, 029 병합 후 이 폴더에 들어옴)
## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261013-028 | feat | @MoHoDu | feat/campaign-flow | - | assigned |
| Task-20261013-029 | feat | @MoHoDu | feat/feature-reuse-audit | - | assigned |
| Task-20261013-030 | feat | @MoHoDu | feat/injury-survival-update | - | assigned |
| Task-20261013-031 | feat | @MoHoDu | feat/performance-reward-funds | - | assigned |
| Task-20261013-032 | feat | @MoHoDu | feat/space-integration-check | - | scaffolded |
| Task-20261014-033 | refactor | @MoHoDu | refactor/common-code-move | - | assigned |
| Task-20261014-034 | feat | @MoHoDu | feat/dev-mode | - | assigned |
| Task-20261014-035 | feat | @MoHoDu | feat/dummy-spaces | - | assigned |

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Scripts/Systems/Settings/`, `Assets/Resources/Settings/GameSettings/` | Task-20261013-028 (1단계) | 세팅 5묶음 틀. 2단계 추가 칸은 PM이 순서 배정 |
| `Assets/Scripts/MainStage/Tightrope/Damage/`, `Assets/Scripts/MainStage/Tightrope/Results/` | Task-20261014-033 (1단계) | 공용 코드 공통 폴더 이전(공간 작업 시작 전, PM) |

## Decisions

- Open: 2단계 시작 전 연결 약속(시간대 알림·공연 결과 형식·부활 함수), 2단계 공용 파일 배정(plan.md 제안표)
- Decided: 2026-10-14 PM — 쉬는 시간 B안 기본·A/B 전환, 기물 소지 2/1/+2 세팅값, 세팅 5묶음, 공통 폴더 이전 별도 Task(033), 더미 공간 우선(035), 생존 2명 미만 묘기 실패, 게임 시작 후 재접속 불가(로비 허용), 최소 인원 2명·개발자 모드(034) 먼저·테스트 AI는 이후(run-rules.md)
