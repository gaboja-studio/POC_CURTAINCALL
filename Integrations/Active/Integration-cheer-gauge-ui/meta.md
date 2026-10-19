# Integration-cheer-gauge-ui

- **Feature:** UI 리소스 확보·조합·테스트
- **Status:** preparing
- **Branch:** integration/ui-resources
- **Base:** dev
- **Active Task:** Task-20261019-061 (로컬 PM 자체 작업)
- **PM:** @MoHoDu
- **Merge Time:** 미정
- **Updated:** 2026-10-20

## 목표

- PM 지시로 UI 리소스 확보·조합·테스트로 확장. C1을 보존하고 공용 버튼4종·확인씬을 추가한다. 기존 폴더 ID는 유지한다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261019-061 | feat | @MoHoDu | feat/ui-resources | - | working (로컬 구현) |

## 준비 브랜치

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab` | Task-20261019-061 | 신규 호응 HUD |
| `Assets/Resources/Prefabs/UIs/CommonButtons/` | Task-20261019-061 | 승인된 공용 버튼4종 |
| `Assets/Resources/Fonts/CommonButtons/` | Task-20261019-061 | 버튼 SemiBold TMP |
| `Assets/Resources/Prefabs/UIs/BasicPanels/`, `Assets/Resources/Fonts/BasicPanels/` | Task-20261019-061 | 패널 조립·전용 폰트 |
| `Assets/Scenes/Tests/BasicPanels/` | Task-20261019-061 | 패널 독립 확인 씬 |

## Decisions

- Open: None
- Decided: 2026-10-20 PM이 원격 통합 준비·저장 승인. origin/dev 5e9b1b04ba04e925fafc6e1ad2ed9106184fe2b6에서 원격 integration/ui-resources 초기화. dirty 작업 이동·폐기 없이 진행, 로컬 통합 기준80d0178은 강제 이동하지 않음. 준비 기록은 이번 feat 저장에 포함하며 PR·병합은 미실행.
- Decided: 2026-10-19 PM 승인. 미리보기 디자인 유지, 기존 공연 씬 수정 없음. Task/통합 폴더 ID는 이력 보존.
