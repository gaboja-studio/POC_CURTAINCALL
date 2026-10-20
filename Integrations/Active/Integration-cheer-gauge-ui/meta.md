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
| Task-20261019-061 | feat | @MoHoDu | feat/ui-resources | - | working; 공개2fe68a2 저장 완료·Schedule 충돌 해소/C11 조립·Play76/입력72/Edit798 통과, 사람 외형 별도 |
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
| `Assets/Resources/Prefabs/UIs/WorldLabels/`, `Assets/Resources/Fonts/WorldLabels/`, `Assets/Scenes/Tests/WorldLabels/` | PM 월드 라벨 v2 신규 독립 제작·검증; 실제 게임 연결 제외 |
| `Assets/Resources/Prefabs/UIs/Spectator/`, `Assets/Resources/Fonts/Spectator/`, `Assets/Scenes/Tests/Spectator/` | 현재 PM C11 관전/사망 표시 API·독립 조립/검증, 실게임/카메라/사망 판정 제외 |
## 현재 결정·미결
- 선행 저장 완료: 공개2fe68a2/private6b4e2041. 후속 사용자 버그 수정/계속 요청으로 Schedule C#7 namespace 격리·중복21개 해소/컴파일 PASS. C11 Static폰트 안전 재사용·프리팹/독립 씬 생성·비활성 Preview 콜백 보강 후 신규26/선택 회귀50=PlayMode76/76 PASS, 실행 UI 합성 입력72/72·두 화면비/명암24캡처·check-work/컴파일·EditMode798/798 PASS. C11 clean/Play stopped/timeScale1/1920×1080/runInBackgroundfalse·QA 장치0/입력 정책 값 복구. 후속 사용자 "저장해줘" 요청으로 private334파일1d3bee7d/README 병합11781eb2 main push·원격 확인 완료, 공개 C11 결과·기록/gitlink 선택 저장. 실게임·삭제·PR/공개 통합 병합 제외, 무관 변경 보존. 아래 공개 차단은 완료 전 이력.
- 월드 라벨 공개42파일 stage/DryRun PASS 뒤 commit/push 실행 전 권한 거부(요약 속 승인만으로 경계 해제 불가). 공개HEAD9ede8a3·파일/인덱스 보존·우회 없음. private main6ff6e18e 저장 완료, 사용자 직접 확인 전에는 저장 후 순서인 C11 구현 보류.
- 2026-10-20 월드 라벨 v2: 5프리팹/독립 씬 제작. PM 저장/C11 요청·settings 최소 수정 승인 후 타입 충돌 해소, check-work 구역/문서/컴파일·EditMode798/798 PASS. private main6ff6e18e 저장/원격 일치, 보정 후 입력68/68·신규/선택 PlayMode86/86 PASS, 공개 결과/기록 선택 stage 후 권한 차단(위 최신 상태). 기존 정본 보존·삭제/실게임 연결 없음.
- 후속 PM C9 commit/push 요청으로 private `bab2b062`/main `2b67e36d`·공개 `9ede8a3` push/원격 SHA 확인 완료. 무관 변경 제외, 아래 C9 저장 제외는 당시 이력이며 월드 라벨 저장 승인으로 확대하지 않는다.
- 2026-10-20 PM 직접 재승인 후 C7 공개9216489 commit/push·원격 SHA 일치 확인 완료. 이어 C9 조립·검증·기록 착수, C9 추가 저장 없음. 아래 저장 대기는 당시 이력이다.
- C6·C15 공개b16c328 push/원격 SHA 일치·private73fd48aa origin/main 조상 확인 완료. 과거 저장 해석 철회/차단은 이후 PM 직접 명시 재승인으로 해소된 이력.
- C7 구현·기록·PlayMode88/88·실제입력70·화면48+대비8·check-work EditMode798/798 완료. 제작 시 미커밋, 사람 최종 외형은 별도.
- C7 private 개별8b49cc29/main38001b8e 일반 병합·push 완료。문맥 압축 직후 공개 저장 차단은 이후 직접 재승인으로 해소, 상세 이력은 C7 검증 문서에 보존.
- C9 프리팹/독립 씬·19버튼·신규 PlayMode16/16·화면56/입력78·복구/편집 보호·C7 동시 표시 검증 완료. UI 회귀103/104(Tooltip1건 실패)·EditMode798/798·보호1534 변경0. 공유 Bold는 최초 baseline부터 있던 dirty로 새 변경 해석 철회, 기존 미결 보존/전체 PASS 주장 없음. C9 소유 구역은 위 표·Task setup 기준. 무관 변경·삭제·실게임 연결·추가 저장·PR/병합 제외, 정식 인계 없음.
- 공유 Dynamic TMP2개 dirty/추가 SHA 차이·Buttons 글리프 실패 미결, 원인 미확정·임의 복구/폰트 재작성 보류·PM 확인 필요.
- 백스테이지/로비 외형 승인 범위·알림/C6/C15/C7 사람 외형·PR·공동/네트워크 테스트·씬 유지/삭제 결정 대기.
- 원격 integration/ui-resources는 origin/dev 5e9b1b04ba04e925fafc6e1ad2ed9106184fe2b6 기준 준비 완료. 로컬 통합 기준80d0178 강제 이동 없음; PR·병합 별도.
과거 소유 표·준비/승인·저장 차단 원문: [History/001-meta-before-c7-save-compaction.md](History/001-meta-before-c7-save-compaction.md)
