# Task-20261019-061
- **Title:** UI 리소스 확보·조합·테스트
- **Type:** feat
- **Status:** blocked
- **Assignee:** @MoHoDu
- **Domain:** Unity Assets 구조·코드 작업 구역
- **Current Skill:** integrate-editor
- **Updated:** 2026-10-20
Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.
## Workspace
- **Integration:** integration/ui-resources
- **Branch:** feat/ui-resources (이전 HUD 공개4a39ef2/private38442263, 알림 공개d7f54e7/private0a705e6c 저장 이력. PM 명시 재요청으로 C6·C15 저장 진행, private73fd48aa main push 확인. 무관 변경/삭제/실게임 연결/PR·병합 제외, 정식 인계 없음)
- **Previous Task:**
## Goal
- 승인된 UI 키트 확보·조합·독립 테스트. C1·버튼4종·패널2종·아이콘25종·공연C2/C3/P1·백스테이지W1/C4/C5/W6·로비L1에 C8 배너·오른쪽 토스트, C6 안내판·진행 링, C15 근접·패널 품목 설명 툴팁을 추가한다. 다른 UI는 사용자 지시 후 추가한다.
- C1 원본 미리보기와 같은 상단 중앙 프리팹을 확인하고, 환호·야유·사망에 따른 실제 합계와 단원별 원호 변화를 확인한다.
- 난도 상한100/150/200, 1~4명과 공연 내 색 고정을 지원한다. 기존 공연 씬 배치는 제외한다.
## Scope
- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: 전용 worktree의 setup.md 작업 구역 내 폴더·프리팹·폰트·테스트 씬 생성. private C1·버튼 키트 import와 setup의 조립기/namespace 수정 허용. 기존 메인 폴더/공용씬/보상 코드 변경 금지.
## Decisions
- Decided: 2026-10-20 PM이 차단 보고 후 "지금까지 변경 사항을 commit, push" 명시 요청. C6·C15 private 키트/공개 결과·하네스/gitlink 저장 승인으로 이전 C6 저장 금지를 이번 저장에 한해 대체한다. 무관 private 변경·Interaction 1.meta는 보존/제외, 삭제·PR/병합 없음. 저장 확인 후 C7 guides 묶음을 조사·구현한다.
- Decided: 2026-10-20 PM C15 근접·패널 품목 설명 묶음 구현 및 완료 후 저장 요청. 실제 키트 curtaincall_tooltips_v1·제공 명세/디자인/API·전체 원본/기존 정본 보존, 삭제/실게임 연결 없음. C6까지 저장 승인이라는 AI 해석은 철회; 앞선 C6 커밋/push 금지 유지.
- Progress: C15 신규20+기존52=PlayMode72/72·화면56·실제입력82·표준check-work EditMode798/798 PASS. 결합 private 저장 시도는 권한 검사에서 실행 전 차단, commit/push0. private HEAD1c0f6f0c/공개d7f54e7 유지·파일/스테이징 보존, 승인 범위 확인 대기로 blocked.
- Decided: 2026-10-20 PM C6 승인 계획에 따른 구현·검증·하네스 기록 진행. Interaction 전용 안내판/내부 진행 링·독립 씬, 신규2/재사용6·전체 원본 보존. 삭제·커밋·push·실게임 연결 금지, 권한 서비스 장애 지속 시 보존·중단 지점 보고.
- Progress: C6 프리팹1종·독립 씬·Preview22버튼·Static Medium·안전 Builder·전수/하네스 기록 구현. 최종 PlayMode8종52/52·실제 입력90건·화면56상태와 최종 배치8화면 통과, 보호397 SHA 변경0·원본49 누락0. 표준 check-work 결과는 handoff Verification 참조. 사람 최종 외형·기존 공유 폰트/Buttons 미결은 별도이며 미커밋 유지.
- Decided: 2026-10-20 PM 지금까지 저장 재요청. 알림 private 키트·공개 결과/하네스/gitlink 커밋·push 승인, 무관 변경 제외. 다음 C6 interaction 키트는 저장 완료 후 조사·계획하며 이번 커밋에서 제외.
- Decided: 2026-10-20 PM 알림 계획 승인. C8 배너·오른쪽 토스트3프리팹·독립 씬·unscaled 모션·전수/회귀 검증. 원본58실파일/PNG22 보존, 표시15=신규5+재사용10 정본 읽기 전용. 삭제·Git 저장·실게임 연결·큐/우선순위 제외.
- Progress: Notifications 프리팹3종/독립 씬·안전 builder·전수 목록/하네스 기록 완료. 강화PlayMode9/9 두 번·실제44상태 화면·독립Play2회·참조/보호baseline111 변경0 및 기존 UI 개별 회귀 통과. 최초 입력 실패/수집 중단 이력과 기존 폰트 미결 보존, 최종check-work 구역/문서/컴파일·EditMode798/798 PASS. 상세 notifications-verification.md.
- Open: 공유 동적 TMP2개는 시작부터 dirty이며 백스테이지 baseline 대비 추가 SHA 차이. 공용 버튼 글리프 검사 실패 재현. 기존 dirty 백업이 없어 임의 복구/공유 폰트 재작성은 보류, PM 확인 필요. 상세는 backstage-hud-verification.md.
- Decided: 2026-10-20 PM 공연·백스테이지/로비 HUD까지 저장 요청. private 두 키트·공개 결과·하네스·gitlink 저장/push, 공유 폰트 현재 수정 상태도 미결 기록과 함께 보존. 새 notifications는 다음 조립 대상이며 이번 저장에서 제외.
- Decided: 2026-10-20 PM 백스테이지·로비 HUD 계획 승인. W1/C4/C5/W6/L1·프리팹3종·합성 확인 씬·전수 점검. 기존 정본 읽기 전용, 원본 전체 보존, 삭제·Git 저장·실게임 연결 제외.
- Decided: 2026-10-20 PM P1 경고 표시 누락 보정 계획 승인. 기존 Task에서 안전/주황/위험5구간·경계 API·합산 위험 표시·증분 prefab 보정·확인 씬/테스트를 수정한다. 게임 판정·실게임 연결·삭제·Git 저장 제외.
- Decided: 2026-10-20 PM 공연 HUD 조립 계획 승인. C2/C3/P1·C1 정본 동시 확인 씬·전수 목록·경계값 검증, 원본 전체 보존. 삭제·Git 저장·실게임 연결 제외.
- Decided: 2026-10-20 PM 아이콘 동작 확인 후 지금까지 내용 저장 요청. private 아이콘 전체와 공개 코드·참조 자산·씬·하네스·gitlink 커밋/push 승인. 무관 private 변경 제외, 삭제·PR·병합·실게임 연결 없음.
- Decided: 2026-10-20 PM 아이콘25종 조립·전수 목록·독립 테스트 씬 계획 승인. 미래 사용 후보·제작 자료 전체 보존, 기존 UI 읽기 전용. 아이콘 삭제·Git 저장·실게임 연결 없음.
- Decided: 2026-10-20 PM 패널 정상 동작 확인, 저장·미사용 자료 정리·하네스 이관 승인. 다른 패널 제작용 원본·공용 리소스·조립기는 보존하고 private 무관 변경은 제외한다.
- Decided: 2026-10-20 PM 패널 키트 조립 계획 승인. 현재 Task에 BasicPanels 조립 프리팹·독립 확인 씬·제공 UI 전환 검증 추가. 기존 UI 정본 보존, 키트 삭제·Git 저장·실게임 연결 제외.
- Decided: 2026-10-20 PM이 원격 통합 준비·저장/push 승인. origin/dev 5e9b1b04에서 원격 integration/ui-resources 생성, private 필수 키트는 main c15d9a11 저장. PR·병합은 별도 요청.
- Decided: 2026-10-20 PM이 버튼 정상 동작을 확인하고 저장·미사용 제작 파일 정리·하네스 기록을 승인. 필수 의존성과 동작을 보존하고 삭제 전후 회귀 검사한다.
- Decided: 2026-10-19 PM 지시. 현재 Task를 각종 UI 리소스 확보·조합·테스트로 변경, 로컬 브랜치 ui-resources로 개명. 버튼4종/확인씬 추가 승인, C1 보존. Task ID/폴더는 이력 유지.
- Decided: 2026-10-19 PM 정리 계획 승인. 프리팹·기능 재확인 후 전용 private 복사본 필수 의존성/라이선스만 유지, 지정 Harness에 정보 기록. 최신 디자인과 메인 원본 보존, 커밋·push 없음.
- Decided: 2026-10-19 PM @MoHoDu 승인. 미리보기 그대로 제작. PM 자체 로컬 Task 준비/구현, private 키트 별도 복사본 import. 원본 보존, 자동 커밋·push·PR 없음. 정식 원격 배정과 구분.
