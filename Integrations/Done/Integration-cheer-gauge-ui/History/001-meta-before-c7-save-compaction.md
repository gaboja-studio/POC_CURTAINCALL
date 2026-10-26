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
| Task-20261019-061 | feat | @MoHoDu | feat/ui-resources | - | working (C6·C15 b16c328 push 완료·C7 구현/자동 검증 완료·미커밋) |

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
| `Assets/Resources/Prefabs/UIs/CommonIcons/`, `Assets/Resources/Fonts/CommonIcons/` | Task-20261019-061 | 아이콘25종 catalog·비교 프리팹·필요 시 폰트 |
| `Assets/Scenes/Tests/CommonIcons/` | Task-20261019-061 | 아이콘 독립 확인 씬 |
| `Assets/Resources/Prefabs/UIs/PerformanceHud/`, `Assets/Resources/Fonts/PerformanceHud/` | Task-20261019-061 | 공연 HUD C2/C3/P1·필요 시 폰트 |
| `Assets/Scenes/Tests/PerformanceHud/` | Task-20261019-061 | 공연 HUD 독립 확인 씬 |

| `Assets/Resources/Prefabs/UIs/BackstageHud/`, `Assets/Resources/Fonts/BackstageHud/` | Task-20261019-061 | 백스테이지·로비 HUD3종·필요 시 폰트 |
| `Assets/Scenes/Tests/BackstageHud/` | Task-20261019-061 | 백스테이지·로비 독립 확인 씬 |

| `Assets/Resources/Prefabs/UIs/Notifications/`, `Assets/Resources/Fonts/Notifications/` | Task-20261019-061 | 알림3종·필요 시 Static 폰트 |
| `Assets/Scenes/Tests/Notifications/` | Task-20261019-061 | C8 배너·토스트 독립 확인 씬 |

| `Assets/Resources/Prefabs/UIs/Interaction/`, `Assets/Resources/Fonts/Interaction/` | Task-20261019-061 | C6 안내판·내부 진행 링·필요 시 Static 폰트 |
| `Assets/Scenes/Tests/Interaction/` | Task-20261019-061 | C6 독립 합성 확인 씬 |

| `Assets/Resources/Prefabs/UIs/Tooltips/`, `Assets/Resources/Fonts/Tooltips/` | Task-20261019-061 | C15 근접·패널 품목 설명·Static 폰트 |
| `Assets/Scenes/Tests/Tooltips/` | Task-20261019-061 | 툴팁 독립 합성 확인 씬 |

| `Assets/Resources/Prefabs/UIs/Guides/`, `Assets/Resources/Fonts/Guides/` | Task-20261019-061 | C7 가이드·키캡·마우스 표시 |
| `Assets/Scenes/Tests/Guides/` | Task-20261019-061 | C7 독립 확인 씬 |

## Decisions

- Open: None
- Progress: 2026-10-20 PM 직접 재승인 후 공개b16c328 push/원격 SHA 일치 확인, private73fd48aa origin/main 조상 확인. 선행 저장 후 C7 구현/자동 검증(PlayMode88/88·EditMode798/798)·기록 완료, 미커밋/사람 외형 별도. 아래 저장 차단은 과거 이력.
- Decided: 2026-10-20 PM이 차단 보고 후 지금까지 변경 commit/push 명시 요청. C6·C15 저장에 한해 이전 금지 대체, private73fd48aa main push 완료·공개 결과/하네스/gitlink는 이번 저장. 무관 변경/삭제/PR·병합 제외, C7 guides는 저장 확인 뒤 별도 구현.
- Decided: 2026-10-20 PM C15 근접·패널 품목 설명 묶음 구현·검증 및 완료 후 저장 요청. Tooltips 전용 구역·제공 디자인/API 유지, 전체 원본·정본 보존. C6 포함 승인이라는 AI 해석은 철회하고 C6 저장 금지 유지. 결합 저장은 권한 검사에서 실행 전 차단, 신규 commit/push0·파일/스테이징 보존·확인 대기. 무관 변경·삭제·실게임 연결·PR/병합 제외.
- Decided: 2026-10-20 PM 승인 C6 구현·검증·기록은 Task-061의 Interaction 전용 구역에서 진행. 이전 UI 정본과 전체 원본 보존, 삭제·커밋·push·실게임 연결·PR/병합 없음. 사람 최종 외형 확인은 별도다.
- Decided: 2026-10-20 PM이 원격 통합 준비·저장 승인. origin/dev 5e9b1b04ba04e925fafc6e1ad2ed9106184fe2b6에서 원격 integration/ui-resources 초기화. dirty 작업 이동·폐기 없이 진행, 로컬 통합 기준80d0178은 강제 이동하지 않음. 준비 기록은 이번 feat 저장에 포함하며 PR·병합은 미실행.
- Decided: 2026-10-19 PM 승인. 미리보기 디자인 유지, 기존 공연 씬 수정 없음. Task/통합 폴더 ID는 이력 보존.
