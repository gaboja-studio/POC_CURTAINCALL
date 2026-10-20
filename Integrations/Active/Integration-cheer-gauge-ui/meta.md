# Integration-cheer-gauge-ui
- **Feature:** UI 리소스 확보·조합·테스트
- **Status:** preparing
- **Branch:** integration/ui-resources
- **Base:** dev
- **Active Task:** Task-20261019-061 (로컬 PM 자체 작업)
- **PM:** @MoHoDu
- **Merge Time:** 미정
- **Updated:** 2026-10-20
## 목표 / Tasks
- 승인 디자인·C1 보존, 버튼/패널/아이콘·공연/백스테이지/로비 HUD·C8·C6·C15·C7·C9 조립/독립 테스트. 기존 폴더 ID 유지.
| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261019-061 | feat | @MoHoDu | feat/ui-resources | - | working; C7 저장 완료·C9 구현/독립 검증 완료, 회귀/보존 미결 |
## 공용 파일 소유 표
아래 소유 Task는 모두 Task-20261019-061. 권한·경계 상세는 해당 `setup.md` 기준.
| 파일 | 이유 |
|---|---|
| `Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab` | 신규 호응 HUD |
| `Assets/Resources/Prefabs/UIs/CommonButtons/`, `Assets/Resources/Fonts/CommonButtons/` | 버튼4종·SemiBold TMP |
| `Assets/Resources/Prefabs/UIs/BasicPanels/`, `Assets/Resources/Fonts/BasicPanels/`, `Assets/Scenes/Tests/BasicPanels/` | 패널 조립·폰트·독립 씬 |
| `Assets/Resources/Prefabs/UIs/CommonIcons/`, `Assets/Resources/Fonts/CommonIcons/`, `Assets/Scenes/Tests/CommonIcons/` | 아이콘25종 catalog·비교 프리팹·필요 시 폰트·독립 씬 |
| `Assets/Resources/Prefabs/UIs/PerformanceHud/`, `Assets/Resources/Fonts/PerformanceHud/`, `Assets/Scenes/Tests/PerformanceHud/` | C2/C3/P1·필요 시 폰트·독립 씬 |
| `Assets/Resources/Prefabs/UIs/BackstageHud/`, `Assets/Resources/Fonts/BackstageHud/`, `Assets/Scenes/Tests/BackstageHud/` | 백스테이지/로비3종·필요 시 폰트·합성 씬 |
| `Assets/Resources/Prefabs/UIs/Notifications/`, `Assets/Resources/Fonts/Notifications/`, `Assets/Scenes/Tests/Notifications/` | C8 배너/토스트3종·필요 시 Static·독립 씬 |
| `Assets/Resources/Prefabs/UIs/Interaction/`, `Assets/Resources/Fonts/Interaction/`, `Assets/Scenes/Tests/Interaction/` | C6 안내판/링·필요 시 Static·합성 씬 |
| `Assets/Resources/Prefabs/UIs/Tooltips/`, `Assets/Resources/Fonts/Tooltips/`, `Assets/Scenes/Tests/Tooltips/` | C15 근접/패널 품목 툴팁·Static·합성 씬 |
| `Assets/Resources/Prefabs/UIs/Guides/`, `Assets/Resources/Fonts/Guides/`, `Assets/Scenes/Tests/Guides/` | C7 가이드/키캡/마우스·독립 씬 |
| `Assets/Resources/Prefabs/UIs/Captions/`, `Assets/Resources/Fonts/Captions/`, `Assets/Scenes/Tests/Captions/` | C9 자막/화자/방송·독립 씬 |
## 현재 결정·미결
- 후속 PM C9 commit/push 요청으로 C9 결과 선택 저장 승인, private `bab2b062`/main `2b67e36d` push 완료. 공개 소유 출력·코드·기록/gitlink 저장 대상, 무관 변경 제외. 아래 C9 저장 제외는 당시 이력이다.
- 2026-10-20 PM 직접 재승인 후 C7 공개9216489 commit/push·원격 SHA 일치 확인 완료. 이어 C9 조립·검증·기록 착수, C9 추가 저장 없음. 아래 저장 대기는 당시 이력이다.
- C6·C15 공개b16c328 push/원격 SHA 일치·private73fd48aa origin/main 조상 확인 완료. 과거 저장 해석 철회/차단은 이후 PM 직접 명시 재승인으로 해소된 이력.
- C7 구현·기록·PlayMode88/88·실제입력70·화면48+대비8·check-work EditMode798/798 완료. 제작 시 미커밋, 사람 최종 외형은 별도.
- C7 private 개별8b49cc29/main38001b8e 일반 병합·push 완료。문맥 압축 직후 공개 저장 차단은 이후 직접 재승인으로 해소, 상세 이력은 C7 검증 문서에 보존.
- C9 프리팹/독립 씬·19버튼·신규 PlayMode16/16·화면56/입력78·복구/편집 보호·C7 동시 표시 검증 완료. UI 회귀103/104(Tooltip1건 실패)·EditMode798/798·보호1534 변경0. 공유 Bold는 최초 baseline부터 있던 dirty로 새 변경 해석 철회, 기존 미결 보존/전체 PASS 주장 없음. C9 소유 구역은 위 표·Task setup 기준. 무관 변경·삭제·실게임 연결·추가 저장·PR/병합 제외, 정식 인계 없음.
- 공유 Dynamic TMP2개 dirty/추가 SHA 차이·Buttons 글리프 실패 미결, 원인 미확정·임의 복구/폰트 재작성 보류·PM 확인 필요.
- 백스테이지/로비 외형 승인 범위·알림/C6/C15/C7 사람 외형·PR·공동/네트워크 테스트·씬 유지/삭제 결정 대기.
- 원격 integration/ui-resources는 origin/dev 5e9b1b04ba04e925fafc6e1ad2ed9106184fe2b6 기준 준비 완료. 로컬 통합 기준80d0178 강제 이동 없음; PR·병합 별도.
과거 소유 표·준비/승인·저장 차단 원문: [History/001-meta-before-c7-save-compaction.md](History/001-meta-before-c7-save-compaction.md)
