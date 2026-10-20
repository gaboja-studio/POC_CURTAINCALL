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
- 승인 디자인·C1 보존, 버튼/패널/아이콘·공연/백스테이지/로비 HUD·C8·C6·C15·C7 조립/독립 테스트. 기존 폴더 ID 유지.
| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261019-061 | feat | @MoHoDu | feat/ui-resources | - | working; C7 구현/자동 검증 완료·공개 저장 대기 |
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
## 현재 결정·미결
- C6·C15 공개b16c328 push/원격 SHA 일치·private73fd48aa origin/main 조상 확인 완료. 과거 저장 해석 철회/차단은 이후 PM 직접 명시 재승인으로 해소된 이력.
- C7 구현·기록·PlayMode88/88·실제입력70·화면48+대비8·check-work EditMode798/798 완료. 제작 시 미커밋, 사람 최종 외형은 별도.
- 최신 사용자 요청은 C7 저장 후 C9. C7 저장만 승인 반영; 담당자 보고상 private 개별8b49cc29/main38001b8e 일반 병합·push 완료, 공개 저장은 압축 후 예정(결과 SHA 없음). C9 구역 추가 없음.
- 이번 압축은 지정 문서5개+History만, 커밋/push·Unity 없음. 무관 변경·삭제·실게임 연결·PR/병합 제외, 정식 인계 없음.
- 공유 Dynamic TMP2개 dirty/추가 SHA 차이·Buttons 글리프 실패 미결, 원인 미확정·임의 복구/폰트 재작성 보류·PM 확인 필요.
- 백스테이지/로비 외형 승인 범위·알림/C6/C15/C7 사람 외형·PR·공동/네트워크 테스트·씬 유지/삭제 결정 대기.
- 원격 integration/ui-resources는 origin/dev 5e9b1b04ba04e925fafc6e1ad2ed9106184fe2b6 기준 준비 완료. 로컬 통합 기준80d0178 강제 이동 없음; PR·병합 별도.
과거 소유 표·준비/승인·저장 차단 원문: [History/001-meta-before-c7-save-compaction.md](History/001-meta-before-c7-save-compaction.md)
