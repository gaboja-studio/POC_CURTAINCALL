# W3 공연 스케줄표 UI 검증

## 범위·계약
- 2026-10-20 사용자 제공 schedule 키트를 전용 ScheduleUi 코드/Canvas 프리팹/독립 확인 씬으로 조립한다. 기존 ScheduleV1 namespace 격리를 유지하며 실게임 일정/진행/보상/경제/네트워크/저장/입력 소유는 연결하지 않는다.
- Canvas1920×1080/Match.5/PPU100, 패널1056×552/(432,272), 상단240 HUD 보호·전면 딤 없음. 종이992×344/재질 Simple 고정 alpha.16, 열3개304×312/viewport304×248·카드304×112/보폭124, 닫기192×56 유지.
- 정확히3일의 불변 표시 자료를 외부에서 주입한다. current/confirmed 독립, 시간/정렬/현재 일차/보상 계산 없음. 동일 세션 갱신의 일차ID별 정규화 스크롤 유지, revision 순서 판단은 호출자 책임이다.
- Open160/Close120ms unscaled, 닫기/Submit/Cancel·열별 스크롤 어댑터. 카드 비선택형·전역ESC/커서/게임 입력 억제 없음. 실제 입력/합성 이벤트/사람 외형 승인을 구분한다.
- 전체 원본/PNG/GUID/자료·기존 정본/Static 폰트 보존. private 개별 경로·SHA 전수 자료는 scratch에만 보관하며 공개 문서에 만들지 않는다. 제작 요청이며 commit/push/PR/병합 제외.

## 계획
1. 생산20=신규8+정본12 시트/해시/규격 대조, 기존 Static Medium/SemiBold/Bold 재사용 확인.
2. 제공 Builder 안전 조립 진입점·Runtime 참조/Cancel 가드, 신규8 importer만 보정·기존 출력 거절/additive 격리/부분 실패 보존으로 전용 프리팹/씬 제작.
3. 표시/불변 자료/잘못된 입력·빈 열/1·2·다수 카드/current×confirmed/긴문구·scroll/갱신·Close/Hide/reentry/disable/unscaled를 자동 검사한다.
4. 구역→문서→컴파일→테스트, 두 화면비1920×1080/1200·명암 실행 캡처/합성 입력·참조/글리프/원본/종료 복구를 기록한다. 사람 외형/물리 입력/실기기/공동은 별도다.

## 실행 이력
- 연속 Task 최초 doctor 결과를 유지하고 중복 실행하지 않았다. 대상 Unity6000.3.25f1/pid28976/port7800 ready, ShopUi 씬 clean·Play stopped, 공개 변경은 무관 private 미추적 results/title_logo뿐임을 확인했다. 선행 W2 공개3b2a3a4/private42477d6f 저장 완료.
- 보호 baseline1132파일 확보. 제공 시트2032×1792의20조각과 개별 RGBA/치수20/20, 재사용12와 승인 정본의 PNG SHA12/12 일치. 제작 자료/미래 후보를 포함한 전체 원본 보존.
- 첫 eval은 unfocused Editor main-thread5000ms timeout으로 실패했다. 대상 pid의 창 포커스 복구 후 Play/compile/import 모두false 확인. 같은 수정 명령을 중복 실행하지 않았다.
- 기존 Spectator Medium/SettingsUi SemiBold는 Static11325문자, WorldLabels Bold는 Static267문자이며 '공연 일정표' 모두 포함했다. 이후 전체 계약 검사에서 Bold의 말줄임표 누락이 확인되어 기존 폰트는 보존하고 전용 ScheduleUi Bold Static2048/107문자를 생성했다.
- 제공 Builder에 주입 Canvas/폰트/Sprite만 조립하는 BuildProject 추가, importer/저장/자동삭제 없이 Hidden 반환. Open/SetPresentation은 모든 열/카드/scroll content 참조를 변이 전에 검사하며 Cancel relay는 Play/active/owner/버튼 interactable·null 이벤트를 검사한다.

## 후속 저장 승인 (2026-10-21)
- 사용자 직접 "커밋, 푸시해줘" 요청으로 이번 W3 private/public 저장을 승인했다. 아래 제작 시 저장 제외만 대체하며 삭제/실게임 연결/PR/통합 병합은 제외한다.
- 저장 전 구역/문서/Compile 재검사 PASS(테스트 재실행 생략, 기존 Play20/회귀74/Edit798 결과 유지). private 코드4/importer8만23b9d0a7b8f843848b7203a75ae30e8314228d56으로 main push·원격 SHA 일치 확인. 무관 results/title_logo 미추적 보존·저장 제외, 공개 W3 산출물/기록/gitlink는 이 저장으로 반영한다.

## 현재 결과·남은 확인
- 안전 조립 완료: SchedulePanel.prefab·ScheduleUi.unity, Preview21버튼·공개 표시 어댑터·PlayMode20건 생성. 최초 eval 조립 timeout 뒤 queued=false/importer100/출력 없음 확인 후 긴 제한 run_script로 재개했다. Bold 글리프 검사 실패는 전용 폰트로 해소했다.
- 최초 PlayMode19/20: 일차28pt/높이32에서 실제 TMP 렌더 문자0 재현. 글자 크기/헤더48을 유지하고 라벨만 y4/높이40으로 보정한 뒤 구역/문서/Compile·PlayMode20/20 PASS. 기존 프리팹은 재생성하지 않고 Editor API로 라벨3개만 보정했다.
- 실행 캡처 첫 시도는 초기 eval HTTP400으로 캡처 전에 중단. QA 초기 확인도 긴 제한 run_script로 변경했다. 재실행에서 두 화면비×명암×기본/빈열/12행/마지막행/긴문구/4조합/미확정=28실행 캡처 및 전체 비교·개별 이미지 확인 완료. 겹침/누락 글리프/빈 렌더0, 12행 최하단 scroll1228·마지막 카드 표시 확인.
- 화면비별 합성 raycast27/클릭22/열 휠3/Submit1/Cancel1·Preview21버튼/비활성 콜백1 통과. 물리 Mouse/Keyboard 입력이나 사람 외형 승인은 아니다. 최초 Hidden·닫고 재개·같은 세션 갱신/재정렬·다른 세션 거절·일시정지·명암·전역 상태 복구를 확인했다.
- 보호1132파일 누락0/PNG 변경0/예상 밖 변경0. 승인 private 코드4+신규 importer8만 변경. W2 회귀35/35·C14 회귀23/23·C16 회귀16/16 PASS. 기존 출력 거절·프리팹/씬 SHA 불변 확인.
- 정본 참조2301/누락0/Missing Script0, EventSystem·InputModule 각1. 종료 ScheduleUi clean/Play stopped/timeScale1/runInBackgroundfalse·GameView1920×1080 선택 확인. 편집 상태 Screen640×480은 실제 GameView 선택 크기와 구분했다. 현재 Console오류0/경고2는 C16 방어 회귀의 예상 경고(3433/3434)이며 과거 Pipeline 버퍼와 구분한다.
- 최종 표준 check-work 구역/문서/Compile·EditMode798/798 PASS. 기존 문서 길이 WARN11/hard0와 private 미저장 주의는 보존했다. commit/push/PR/병합은 수행하지 않았다.
- 사람 최종 외형/손맛·물리 입력·실기기/공동 확인과 실게임 연결은 별도다. 전체 PlayMode Tooltip/Buttons 기존 미결은 이번 선택 검증으로 닫지 않는다.
