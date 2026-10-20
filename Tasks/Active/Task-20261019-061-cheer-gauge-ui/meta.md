# Task-20261019-061
- **Title:** UI 리소스 확보·조합·테스트
- **Type:** feat
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** Unity Assets 구조·코드 작업 구역
- **Current Skill:** integrate-editor
- **Updated:** 2026-10-20
## Workspace
- **Integration:** integration/ui-resources
- **Branch:** feat/ui-resources
- PM 자체 로컬 작업; 정식 인계·PR·병합 없음. C6·C15 공개 b16c328 push/원격 SHA 일치·private73fd48aa origin/main 조상 확인 완료.
## Goal / Scope
- 승인 키트 확보·조합·독립 테스트: C1·버튼4·패널2·아이콘25·공연C2/C3/P1·백스테이지W1/C4/C5/W6·로비L1·C8 배너/토스트·C6 안내판/링·C15 툴팁·C7 가이드/키캡/마우스·C9 자막/화자/방송·월드 라벨 v2 소품/근접 정보.
- C1 미리보기의 상단 중앙 배치·환호/야유/사망 실제 합계·단원별 원호, 상한100/150/200·1~4명·공연 내 색 고정 유지. 기존 공연 씬 배치 제외.
- 수정권한/공용 파일/금지 경계는 `setup.md` 기준. 전용 worktree 구역 내 생성·승인 private 조립기/namespace 보정만, 메인 폴더/공용씬/보상 코드 변경 금지.
## 현재 결정·결과
- W3 후속 저장(2026-10-21): 사용자 직접 커밋·푸시 요청으로 private12파일23b9d0a7 main push·원격 일치 확인 완료, 공개 W3 산출물/기록/gitlink 선택 저장·feat/ui-resources push 진행. 아래 W3 저장 제외만 대체하며 무관 results/title_logo·삭제·실게임 연결·PR/통합 병합 제외. W3 제작(2026-10-20): ScheduleUi 전용 프리팹/독립 씬·Preview21버튼 제작. 기존 Medium/SemiBold 읽기 전용·말줄임표 포함 전용 Bold Static 생성, 일차28pt 렌더 높이40 보정 후 Play20/20·두 화면비/명암28캡처·합성 입력·선택 회귀74/74 PASS. 보호1132 누락/PNG/예상 밖 변경0·참조2301 누락0·clean 종료 복구 확인, 최종 check-work 구역/문서/Compile·EditMode798/798 PASS(`schedule-ui-verification.md`). 실게임 일정/보상/경제/입력 소유 연결·삭제·사람 외형/물리 입력·이번 commit/push 제외. 선행 W2 공개3b2a3a4/private42477d6f push·원격 확인 완료; 아래 W2 저장 제외/진행은 이전 이력이다.
- W2(2026-10-20): ShopUi Canvas 프리팹/독립 씬·Preview24버튼·표시 API 조립, 행 글자 영역/16:10 Preview 보정 후 PlayMode35/35 통과. 기존 폰트 보존·전용 Static3자산 생성, 보호1205 중 승인 코드3/importer9 외 변경0. 최종 검증은 `shop-ui-verification.md` 참조. 실제 거래/경제/배송/서비스·입력 소유·사람 외형/물리 입력·commit/push 제외. C14 공개6e5fb38/privatee1ddf391 저장 완료.
- C14 후속 저장 요청(2026-10-20): 사용자 직접 "커밋 푸시해줘" 승인. private21파일 e1ddf391/main push·원격 SHA 확인 완료, 공개 C14 산출물/기록/gitlink·전용 SemiBold 한 파일 LFS 설정을 이번 선택 커밋으로 feat/ui-resources에 저장·push한다. 무관 results/title_logo 보존·저장 제외, PR/공개 통합 병합 없음. 아래 제작 당시 C14 저장 제외만 대체한다.
- C14(2026-10-20): SettingsUi 전용 설정/공용3 프리팹·독립 씬 조립 완료, 기존 Systems/Settings 미수정. 기존 Medium/Bold 읽기 전용·전용 SemiBold Static8192(한글+ASCII11325) 생성. 보정 전 Compile/PlayMode23/23·두 화면비24캡처·합성 입력·참조2484 누락0·baseline626 보존 확인. Preview 국소 배경/하단 고정으로 대비·16:10 겹침 보정, 실제 프리팹 불변. 최종24캡처/비중첩·합성 입력 재통과, Compile/C14 Play23/23·C16 회귀16/16·check-work/EditMode798/798 PASS. 종료 clean/Play stopped·1920×1080·시간/백그라운드 복구·Console오류0/예상 C16 테스트 경고2([settings-ui-verification.md](settings-ui-verification.md)). 물리 입력/사람 외형·실기기/공동·실옵션/저장/Audio/Screen/PlayerPrefs·입력 소유/서비스 연결·C14 commit/push는 별도. C16 공개f9e8666/private76a7e908 push 완료와 구분한다.
- 최신 사용자 직접 요청 "버그를 먼저 수정하고 계속 진행하자."에 따라 Schedule C#7 namespace만 최소 격리, 중복21개 해소·재컴파일 PASS. C11 Static폰트 재사용 안전복구 후 Canvas 전체 프리팹/독립 씬 생성, 신규 PlayMode26/26 및 선택 회귀50/50 PASS. 실행 UI 합성 입력72/72·두 화면비/명암24캡처 확인, 최종 check-work/컴파일·EditMode798/798 PASS, C11 clean/Play stopped·timeScale1·1920×1080·QA 입력 정책 값 복구/장치0(`spectator-verification.md`). 선행 공개2fe68a2 commit/push 완료와 구분하며 후속 사용자 "저장해줘" 요청으로 C11/Schedule private334파일1d3bee7d/README 병합11781eb2 main push·원격 확인 완료, 공개 C11 결과·기록/gitlink 선택 저장. 무관 변경·실게임 연결·삭제·PR/공개 통합 병합 제외. 아래 공개 차단은 완료 전 이력.
- 최신 월드 라벨 공개 저장은 권한 검사에서 실행 전 거부(요약 속 승인만으로 저장 경계 해제 불가). 공개 HEAD9ede8a3·선택 stage42파일 보존, private main6ff6e18e 저장 완료. 우회하지 않았고 저장 후 순서인 C11 구현은 보류했다. 사용자 직접 확인이 필요하다.
- 후속 PM C9 commit/push 요청으로 private `bab2b062`/README 일반 병합 `2b67e36d` main, 공개 `9ede8a3` feat/ui-resources push·원격 SHA 일치 확인 완료. 아래 C9 미커밋/저장 제외는 구현 당시 이력이며 월드 라벨 저장 승인으로 확대하지 않는다.
- 2026-10-20 월드 라벨 v2: 5프리팹·Static Medium/Bold·독립 씬 제작. 후속 PM 저장/C11 요청·settings 최소 수정 승인으로 중복 타입 해소, check-work 컴파일/문서/구역·EditMode798/798 PASS. private main6ff6e18e push/원격 일치 확인, 보정 후 입력68/68·신규/선택 PlayMode86/86 PASS, 공개 결과/기록 선택 stage 후 권한 차단(위 최신 상태). 상세 `worldlabels-verification.md`; 실게임 연결 없음.
- C7 구현·자동 검증·기록 완료(제작 시 미커밋): PlayMode88/88·실제입력70·화면48+대비8·check-work EditMode798/798. 사람 최종 외형 확인은 별도다.
- C7 private 개별8b49cc29/main38001b8e 일반 병합·push 완료. 문맥 압축 직후 공개 저장 차단은 당시 이력이며 아래 직접 재승인 후 해소됐다.
- 2026-10-20 PM 직접 재승인 후 C7 공개9216489 push/원격 SHA 일치 확인 완료. 이어 C9 독립 프리팹/씬·표시 API·19버튼 구현, 신규 PlayMode16/16·화면56/입력78·시간 복구/동시 표시 검증 완료. 위 공개 대기는 당시 이력이며 C9 추가 저장 없음. 무관 변경·삭제·실게임 연결·PR/병합 제외.
- C6 저장 포함이라는 최초 AI 해석 철회/권한 차단은 과거 이력. 이후 PM 직접 명시 재승인으로 C6·C15 저장 금지 해소 및 위 SHA 저장 완료; 과거 금지를 현재 승인으로 오인하지 않는다.
## C16 현재 상태 (2026-10-20)
- PM 요청으로 C16 독립 프리팹/씬·표시 API·Preview9버튼·PlayMode16건 생성. 전체 원본 보존·기존 Static Medium/별 읽기 전용 재사용. 실제 게임 연결/삭제 없음. 제작 당시 저장 제외 후 후속 요청으로 공개f9e8666/private76a7e908 저장·push 완료.
- results 버튼 중복21개로 컴파일 차단됐으나 아래 PM 승인3파일 격리 후 정식 Compile·C16 PlayMode16/16 PASS. 긴문구 검사 두 번15/16은 TMP 플래그·측정 순서 보정 후 해소. Preview 합성 PointerClick13건/비활성 콜백1건 PASS(물리 입력 아님). Overlay/클립 경계 보정 후4화면비 명암16캡처·차폐 배경 차이0px·raycast72/합성 클릭104/비활성8 PASS. C11 회귀26/26·참조1070 누락0·C16 clean/종료 복구 확인. 최종 check-work 구역/문서/컴파일·EditMode798/798 PASS, 현재 Console오류0/경고0(`curtain-verification.md`).
## Open Decisions
- PM이 `최소 수정 후 계속`을 선택(2026-10-20). results의 CommonButtons namespace와 ResultsPanelView/Builder 타입 참조3파일만 격리. results 기능 제작/PNG·GUID 변경·삭제·commit/push 제외는 유지한다. 격리 후 정식 Compile·C16 PlayMode16/16 통과. 별도 캡처에서 발견한 Preview Overlay/clip 경계 보정 후에도 C16 PlayMode16/16 재통과.
## 미결
- 공유 CheerGauge Bold Dynamic은 HEAD와 다르지만 최초 C9 baseline SHA와 현재 SHA 일치. 새 변경이라는 초기 해석은 철회, 기존 미결 파일 보존/임의 복구 금지. Tooltip 회귀20건 중1건 좌표 정밀 비교 실패 재현, C9 통과와 구분 (`captions-verification.md`).
- 공유 Dynamic TMP2개 시작부터 dirty·백스테이지 baseline 대비 추가 SHA 차이, Buttons 전체 글리프 실패 재현. 원인 미확정·dirty 백업 없음; 임의 복구/공유 폰트 재작성 보류, PM 확인 필요 (`backstage-hud-verification.md`).
- 백스테이지/로비 외형 승인 범위·알림/C6/C15/C7 사람 최종 외형, PR·공동/네트워크 테스트·씬 유지/삭제 결정은 별도 미완료.
과거 승인·저장·검증 원문: [History/001-meta-before-c7-save-compaction.md](History/001-meta-before-c7-save-compaction.md)
