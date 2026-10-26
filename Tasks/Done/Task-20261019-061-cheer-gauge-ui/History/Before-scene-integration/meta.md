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
- 승인 키트 확보·조합·독립 테스트: C1·버튼4·패널2·아이콘25·공연C2/C3/P1·백스테이지W1/C4/C5/W6·로비L1·C8 배너/토스트·C6 안내판/링·C15 툴팁·C7 가이드/키캡/마우스·C9 자막/화자/방송.
- C1 미리보기의 상단 중앙 배치·환호/야유/사망 실제 합계·단원별 원호, 상한100/150/200·1~4명·공연 내 색 고정 유지. 기존 공연 씬 배치 제외.
- 수정권한/공용 파일/금지 경계는 `setup.md` 기준. 전용 worktree 구역 내 생성·승인 private 조립기/namespace 보정만, 메인 폴더/공용씬/보상 코드 변경 금지.
## 현재 결정·결과
- 후속 PM C9 commit/push 요청으로 해당 결과 선택 저장 승인. private `bab2b062`/README 일반 병합 `2b67e36d` main push 완료, 공개는 C9 출력·코드·기록/gitlink만 포함. 아래 미커밋/저장 제외는 구현 당시 이력이다.
- C7 구현·자동 검증·기록 완료(제작 시 미커밋): PlayMode88/88·실제입력70·화면48+대비8·check-work EditMode798/798. 사람 최종 외형 확인은 별도다.
- C7 private 개별8b49cc29/main38001b8e 일반 병합·push 완료. 문맥 압축 직후 공개 저장 차단은 당시 이력이며 아래 직접 재승인 후 해소됐다.
- 2026-10-20 PM 직접 재승인 후 C7 공개9216489 push/원격 SHA 일치 확인 완료. 이어 C9 독립 프리팹/씬·표시 API·19버튼 구현, 신규 PlayMode16/16·화면56/입력78·시간 복구/동시 표시 검증 완료. 위 공개 대기는 당시 이력이며 C9 추가 저장 없음. 무관 변경·삭제·실게임 연결·PR/병합 제외.
- C6 저장 포함이라는 최초 AI 해석 철회/권한 차단은 과거 이력. 이후 PM 직접 명시 재승인으로 C6·C15 저장 금지 해소 및 위 SHA 저장 완료; 과거 금지를 현재 승인으로 오인하지 않는다.
## 미결
- 공유 CheerGauge Bold Dynamic은 HEAD와 다르지만 최초 C9 baseline SHA와 현재 SHA 일치. 새 변경이라는 초기 해석은 철회, 기존 미결 파일 보존/임의 복구 금지. Tooltip 회귀20건 중1건 좌표 정밀 비교 실패 재현, C9 통과와 구분 (`captions-verification.md`).
- 공유 Dynamic TMP2개 시작부터 dirty·백스테이지 baseline 대비 추가 SHA 차이, Buttons 전체 글리프 실패 재현. 원인 미확정·dirty 백업 없음; 임의 복구/공유 폰트 재작성 보류, PM 확인 필요 (`backstage-hud-verification.md`).
- 백스테이지/로비 외형 승인 범위·알림/C6/C15/C7 사람 최종 외형, PR·공동/네트워크 테스트·씬 유지/삭제 결정은 별도 미완료.
과거 승인·저장·검증 원문: [History/001-meta-before-c7-save-compaction.md](History/001-meta-before-c7-save-compaction.md)
