# Plan
## Context / 현재 승인
- 최신 요청(2026-10-20): 선행 저장은 공개2fe68a2/private9661aa85·6b4e2041 완료. 이어 C11 제작 중 발생한 Schedule 중복21개는 사용자 "버그를 먼저 수정하고 계속 진행하자." 요청으로 해당 kit C#7개 namespace만 격리·재컴파일 PASS. C11 정본/씬 조립 후 자동/실제 입력·화면 검증을 완료한다. 후속 사용자 "저장해줘" 요청으로 이번 C11/Schedule private/public 선택 저장을 허용한다(private1d3bee7d/README 병합11781eb2 main push 확인 완료). 무관 변경·삭제·실게임 연결·PR/공개 통합 병합 제외, 과거 저장 차단은 이력으로 보존.
- UI 키트 확보·조합·독립 테스트. Task/폴더 ID는 이력 유지, 수정 구역/권한은 `setup.md` 기준. 기존 공연 씬·보상/네트워크·게임 메뉴 변경 없음.
- C6·C15 공개 b16c328 push/원격 SHA 일치·private73fd48aa origin/main 조상 확인 완료. 최초 C6 포함 저장 해석 철회/권한 차단은 이후 PM 명시 재승인으로 해소된 이력이다.
- PM 직접 재요청 후 C7 공개9216489 commit/push·원격 SHA 일치 확인 완료, private 개별8b49cc29/main38001b8e. 앞선 차단 이력은 보존한다.
- 이어 C9 제작 승인: 제공 자막/화자/방송 명세를 독립 프리팹·확인 씬·표시 API로 조립·검증·기록. C9 추가 저장·삭제·실게임 연결은 제외한다.
- 후속 PM C9 commit/push 요청으로 C9 private/public 선택 저장 승인. 기존 공유 폰트·무관 변경 제외, 삭제/실게임/PR 제외는 유지한다.
## 타이틀 로고 UI 프리팹 (2026-10-21 현재 요청)
- 후속 사용자 커밋·푸시 승인으로 로고 private 키트/meta·공개 산출물/기록/gitlink 저장·push 진행. 아래 제작 당시 저장 제외만 대체한다.
1. 완료: replacement_v2 명세/실제 Sprite와 기존 출력 없음 확인, 보호560파일 기준 확보. PNG/GUID 보존·신규 importer2만 설정.
2. 완료: TitleLogo.prefab1개·본체/그림자2레이어 조립, Canvas는 외부 호스트 제공. 메뉴/런타임/영구 씬/commit/push 제외.
3. 완료: check-work 구역/문서/Compile/Edit798/798·임시 Play Canvas 두 화면비/명암·3배치12캡처/mesh24/비차단24·참조39 누락0/원본560 보존/출력 보호/종료 복구 확인. 최초 문서 크기/QA API 실패와 보정은 title-logo-verification.md에 기록.
## C12 공연 결과 패널 UI 계획 (2026-10-21 당시 요청)
1. 완료: 제공 계약/코드·시트25/정본19 대조·보호 baseline1277+164 확보. 원본/기존 UI·폰트 보존.
2. 완료: 안전 조립 진입점·ResultsUi 프리팹/독립 씬·Preview28/테스트28·전용 Bold Static245문자 제작. 첫 Play25/28의 TMP 높이/테스트 null 보정 후 Compile·Play28/28 PASS. 실제 보상/진행/네트워크/입력 소유 연결 없음.
3. 완료: Compile·C12 Play28/28·두 화면비 명암48캡처·합성 입력·W3/W2 회귀55/55, 원본/참조1646 누락0·clean 종료 복구 확인.
4. 후속 저장 승인: TimeManager 포함 승인으로 구역 차단 해소·간격 기하1/1·check-work 구역/문서/Compile/Edit798/798 PASS. private233파일 f60ae144 main push/원격 확인, 공개 C12/기록/gitlink/TimeManager 저장·push 진행. 사람 외형/물리 입력·실기기/공동·PR/공개 통합 병합 별도.
## W3 공연 스케줄표 UI 계획 (2026-10-20 당시 요청)
1. 완료: 명세/Runtime/Builder·시트20/재사용 정본12 대조·보호 baseline1132 확보. 기존 ScheduleV1 격리를 유지한다.
2. 완료: 신규 importer8/코드4 최소 보정·공유 Static Medium/SemiBold 읽기 전용, 말줄임표 누락 때문에 전용 Bold Static2048/107 생성. 전용 프리팹/독립 씬·Preview21버튼 제작, 실게임 연결 없음.
3. 완료: 최초19/20의 일차28pt 세로 잘림을 라벨 높이40으로 보정 후 Play20/20 PASS. 1920×1080/1200·명암28캡처·화면비별 ray27/클릭22/휠3/Submit1/Cancel1·Preview21버튼 합성 검증 통과.
4. 완료: 선택 회귀74/74·check-work 구역/문서/Compile·Edit798/798 PASS, 원본 보존/참조2301 누락0·clean 종료 복구·실패 이력 기록. 사람 최종 외형/손맛·물리 입력·실기기/공동은 자동 검증과 구분하고 기능 완성 후 한 번에 확인한다. commit/push/PR/병합 없음.
## W2 수화기 구매·판매 UI 계획 (2026-10-20 당시 요청)
1. 완료: 보호 baseline1205·신규9 importer/승인40 정본 대조, 시트49/49 일치. 기존 폰트는 보존하고 W2 Medium 공유-atlas 변형+Static512 U+2212 보조·고정 제목용 Bold Static2048을 생성했다.
2. 완료: 제공 계약1056×552/(432,272), 3탭/동적 목록·설명·선택/포커스/거래 불가·전면 딤 없음 유지. 안전 조립 API·Canvas 프리팹/독립 씬/Preview24버튼 제작, 글자 영역·확인용16:10 잘림만 보정.
3. 완료: 불변 자료·busy/토큰/다중 View/갱신·재진입·닫기/disable·unscaled·긴문구·스크롤 Play35/35. 두 화면비 합성 입력·명암36캡처·미리보기24버튼 확인. 실제 거래/경제/배송·입력 소유는 연결하지 않는다.
4. 완료: W2 Play35/C14 회귀23/C16 회귀16·check-work/Edit798 통과, 참조2536 누락0·보호1205 예상 밖 변경0·종료 복구 및 실패 이력 기록. 사람 외형/실기기/공동·실거래 연결·commit/push는 별도다.
## C14 설정 UI 계획 (2026-10-20 현재 요청)
1. 완료: baseline626·신규20 importer/재사용16 대조, 승인 Builder1/신규 importer20 외 변경0·PNG변경0/누락0. SettingsUi 전용 코드이며 기존 Systems/Settings 미수정.
2. 완료: 안전 조립 API·설정/공용3 프리팹·독립 SettingsUi 씬/명시 데모 조립. Spectator Medium/WorldLabels Bold 읽기 전용 재사용, 전용 SemiBold Static8192(전체 한글+ASCII11325) 생성.
3. 보정 전 Compile/PlayMode23/23·두 화면비24캡처·합성 입력·참조2484 누락0·기존 출력 거절/종료 audit 확인. 첫 입력 실패는 렌더 등록 전 Graphic.depth=-1 검사로 QA220ms 대기 후 통과, 제품 Runtime 불변. 물리 Mouse/Keyboard·사람 승인과 구분한다.
4. 완료: Preview 국소 배경/하단 고정으로 대비·16:10 겹침 보정(실제 프리팹 불변). 최종24캡처/비중첩·합성 입력·Compile/C14 Play23/23·C16 회귀16/16·check-work/EditMode798/798·clean 종료 복구 PASS, Console오류0/예상 C16 테스트 경고2. [settings-ui-verification.md](settings-ui-verification.md)에 실패/보정/최종 결과를 구분해 기록했다. 실제 옵션 목록/저장/Audio/Screen/PlayerPrefs·입력 소유/서비스 연결·실기기/공동·삭제·C14 commit/push 제외.
- 사람 확인: 패널 점유/HUD 보호, 탭 선택/포커스 구분, 슬라이더·ON/OFF/disabled 가독성과 전환 손맛을 기능 완성 후 한 번에 확인한다. 공동/실게임 입력 소유·safe area는 별도다.
## C16 장소 전환 커튼 계획 (2026-10-20 현재 요청)
1. 원본/기존 출력 SHA baseline·6 PNG 규격/불투명 차폐 영역·기존 별 정본 대조, 제공 명세 유지.
2. 제공 Runtime/Builder 안전 보정·전용 조립 진입점, 기존 Static Medium 읽기 전용 재사용·Canvas 전체 프리팹/독립 확인 씬 제작.
3. 450ms/unscaled·반전/반복/렌더 프레임 장벽/콜백 재진입/무효 참조/비활성·alpha/시간정지·4화면비 차폐/천·술 정합/입력 비차단 검증.
4. check-work·PlayMode·실행 화면·Console/종료 복구·원본/보호 SHA와 제약 기록. 실제 로드/네트워크/입력 정책·삭제·commit/push 제외.
- 사람 확인 3항목: ① 16:9/16:10/21:9/32:9에서 닫힘 틈 없음·천/술 정합·밸런스 위치 ② 열림/반전의 움직임과 로딩 별/문구 가독성 ③ 명암 배경에서 닫힘 차폐와 완전 열림 잔상 없음. 자동 검증과 별도다.
## C11 관전·사망 UI 계획 (2026-10-20 현재 요청)
1. 선행 공개2fe68a2 저장/원격 확인 후 제공 키트129파일·재사용8 정본/보호2737 SHA·시트10조각/비네트 중앙60% 투명 확인.
2. 제공 Runtime/Builder SpectatorV1 격리·인스턴스 초기화·안전 조립 API, 신규 비네트2 importer만 보정. Static Medium·cross reference를 포함한 Canvas 전체 프리팹/독립 씬과 실제 버튼 Preview.
3. 상태/원문/말줄임/키 폭·비차단/slot 퇴장·unscaled 반복/반전·timeScale0/disable·invalid 상태/수치·편집/기존출력 보호를 자동 검증하고 두 화면비/명암/실제입력을 확인한다.
4. check-work·신규/선택 PlayMode·보호 SHA·참조/Console·clean 종료와 지식을 기록한다. 사람 최종 외형·공동/실게임·추가 저장은 제외.
- 사람 확인3항목: 두 화면비·명암에서 비네트 강도/중앙 투명, 상태판/긴이름/키 가독성, 상태 전환/퇴장 중 하단 점유를 한 번에 확인한다.
## 월드 라벨 v2 계획 (2026-10-20 PM 요청)
1. 원본161파일/PNG29·기존 출력838 SHA baseline 보존, 신규 소재12와 정본 재사용7을 대조한다. 제공 검토 제안을 최종 게임/외형 승인으로 간주하지 않는다.
2. 제공 Runtime/Builder 원위치 namespace/편집 보호·안전 조립 보정, 신규12 importer만 설정·정본7 읽기 전용 참조. 전용 Static Medium/Bold·5프리팹·독립 World Space 확인 씬 제작.
3. 5종 크기/레이어/글리프·실제 TMP overflow 보존·표면만 숨기기·근접 히스테리시스/명시 observer·명시 카메라 billboard/복원·시간정지·재활성화/입력 비차단·두 화면비/명암을 검사한다.
4. 컴파일/신규 PlayMode·선택 회귀·check-work 및 원본/보호 SHA 검사 결과와 실패/제약을 기록한다. 후속 PM 저장 요청으로 월드 라벨만 private/public commit·push 후 C11 제작으로 이어간다. 실제게임 연결·삭제·C11 추가 저장 없음.
- settings 최소 namespace/alias 수정 승인 후 컴파일 충돌 해소, check-work 구역/문서/컴파일·EditMode798/798 PASS. private main6ff6e18e push/원격 일치, Preview 보정 후 입력68/68·신규/선택 PlayMode86/86 PASS, 공개 결과/기록 선택 stage 후 commit/push 실행 전 권한 차단(요약 속 승인만으로 경계 해제 불가). 파일/인덱스 보존·우회 없음, C11 구현은 선행 저장 성공 전 보류. 설정 UI 구현/전체 키트 저장은 제외한다.
## C9 계획
1. 최초115행(실파일49/PNG13)·보호1534 SHA baseline 보존, 기존 PNG9 정본 바이트/규격 대조 및 읽기 전용 참조.
2. 제공 Caption Runtime/Builder 원위치 CaptionsV1 격리·안전 진입점·인스턴스 초기화, 전용 Static Medium·공개 안전 어댑터·실제 버튼 Preview 조립.
3. 1120×88/하단32·화자176×48·본문Medium24 두 줄64UI·화자18 한 줄·방송 기본 이름/아이콘·화자없음 확장·폭 독립 계약 유지. 긴 본문 실패 시 현재 cue/mesh 보존, 자동 분할/큐/오디오 동기 없음.
4. 실제 TMP 측정·글리프/참조·폭/무효값/문자/편집/재진입·unscaled120/100ms/반복·반전·timeScale0·입력/두 화면비·명암 배경·회귀/check-work 검증, 최초 실패도 보존·하네스 기록.
- C9 구현·신규 PlayMode16/16·화면56/입력78·복구/편집 보호·C7 동시16UI 검증 완료. 기존 Tooltip 회귀1건과 공유 Bold 기존 dirty는 별도 미결로 보존한다(최초 C9 baseline 대비 변화0). UI 회귀103/104, 표준 EditMode798/798·보호1534 변경0을 구분한다. 최종 상세는 `captions-verification.md`.
## C7 최종 계약·결과
- 제공6모드·544×80/하단176·키40/96/144·마우스32·Medium24·120/100ms 모션 유지.
- private 원위치 GuidesV1·안전 조립·인스턴스 초기화, 신규8 importer만 보정·기존6 정본 SHA/규격 대조 후 읽기 전용.원본54실파일/PNG19 전체 보존.
- 독립 프리팹/확인 씬·합성 Preview·Static 폰트·표시 API. 상위 HUD 차단은 외부 bool 수동 주입만, 게임 입력/바인딩/자동 노출 정책/실게임 연결/삭제 없음.
- 구현/기록 완료: 6모드·차단·문자·모션·무효값/초기화·실제 Mouse/Enter70건·화면48+대비8·PlayMode88/88·check-work EditMode798/798. 최종 외형 사람 확인은 별도.
## 기존 결과·유지 계약
- C1: private PNG8·제공 레이어/색/크기·상단 중앙·Pretendard TMP. 읽기 전용 Binder/보상 API, 실제 long 합계·상한100/150/200·1~4인·사망/이탈 명부·색 유지. 점수 감소를 추락 판정으로 간주하지 않는다.
- 버튼: 제공 Runtime/Builder·4프리팹·SemiBold·Canvas1920×1080/Match0.5·192×56·취소X/설정톱니·9-slice/PPU200/Body0.92. 4상태·Focus·클릭/Submit/Disabled 검증.
- 패널: PanelsV1/동봉 버튼 격리·신규PNG7·기존 버튼 정본 읽기 전용. 메뉴384×400/확인512×304·Y=-64·160/120ms·안전 첫 선택. 전역ESC/pause/커서 변경 없음. 승인 정리 후 제작 원본160파일 보존.
- 아이콘: 신규22+기존3=25·원본64/표시32px·catalog/75 Image 비교·제공 tint/16/24/32/48px. 전체 제작 자료/미래 후보 보존·기존3 정본 읽기 전용.
- 공연: C2시간160×56/C3진행408×76/P1균형544×80·C1 동시 확인. 신규20·아이콘3 정본 읽기 전용. P1 안전/주황/위험5구간·경계/합산 위험 API·증분 보정, 기존 자식/식별자/버튼8 연결 보존. 게임 계산 없음.
- 백스테이지/로비: W1/C4/C5/W6/L1·3프리팹·88실파일/PNG45·신규14/재사용18. PlayMode7/7·입력/24화면·Play2회·check-work 통과. 실게임/클립보드/타이머/경제/집결 계산 없음.
- C8: 원본58/PNG22·표시15=신규5+재사용10·3프리팹. Banner640×104 상단중앙(0,-244)/Toast384×56 우상단(-48,-160), 배너180/160ms·토스트160/140ms·unscaled·EaseOutCubic·등장 후 hold·즉시 교체/동문구 재시작/서로 독립.
- C8 강화PlayMode9/9 두 번·44화면·Play2회·EditMode798/798. 최초 입력 실패/수집 중단은 `notifications-verification.md`. 알림 정책/큐/중복 억제/우선순위/사운드/딤/구매 연결 없음.
- C6: 신규링2/재사용6·384×80·중앙(0,-144)·11레이어·unscaled120/100ms·반복 Show 비재시작·현재 위치 전환·진행률 외부 주입. 52/52·입력90·56상태/배치8화면·보호397 SHA 변경0·원본49 누락0·check-work 통과. 대상 탐색/누르기 시간/완료 판정 없음.
- C15: 신규PNG7·원본49/PNG14·1프리팹·Preview20·Static Medium24/SemiBold28. 폭256~480·측정 높이160~240/4단위·꼬리16×12/중심24..W−24·unscaled120/100ms. 72/72·입력82·56화면·보호613 변경0·EditMode798/798. 자동 감지/투영/화면 보정/구매 연결 없음.
## 공통 보존·안전
- 전체 원본/기존 정본·미래 후보/Source/Tools/Docs/Previews/TTF/라이선스 보존. 재사용 importer/GUID 읽기 전용, 신규만 보정. 제공 Runtime/Builder 원위치 재사용·namespace 격리·안전 API·인스턴스 초기화.
- Builder는 Play/컴파일/import/PrefabStage/dirty 씬/출력 충돌 쓰기 전 차단·additive 임시 조립·소유 출력만 저장, 부분 실패 자동 삭제/재생성 없음. 기존 HUD 인스턴스만 조절·Apply 금지.
- Static 글리프/fallback 확인·공유 Dynamic2개 렌더링 참조 제외. 입력/raycast/sorting·단일 EventSystem·Mouse/Enter·1920/16:10·명암 배경·무효값/긴문구/모션/초기화/회귀 확인. 합성 입력/실행 캡처/사람 확인 구분, reload both OFF 지원 주장 없음.
- 종료 Play stopped·소유 씬 clean·1920×1080·timeScale1 복구. 실패/생략/0건 그대로 기록, Buttons 미결은 PASS로 덮지 않는다. 삭제·실게임 연결·무관 private 변경 저장·PR/병합 제외.
## 미결·위험
- 공유 Dynamic TMP2개 시작부터 dirty·baseline 추가 SHA 차이, Buttons 전체 글리프 실패 원인 미확정. 기존 dirty 백업 없음·임의 복구/공유 폰트 재작성 보류·PM 확인 필요 (`backstage-hud-verification.md`).
- 백스테이지/로비 외형 승인 범위·알림/C6/C15/C7 사람 최종 외형·PR·공동/네트워크 검증·씬 유지/삭제 결정 대기.
- 시작 doctor private C1 미추적 FAIL 사전 보고/복사본 import 승인, 최근 private 변경 FAIL도 보고 완료. 이번 연속 문서 작업에서 doctor/Unity 재실행 안 함. CLI beta.6에 --until-ready 없음, 실제 help 확인.
## Auto Verification / 공동 테스트
- 구역 verify-scope → 문서 verify-fast → 컴파일 verify-unity -Compile → EditMode/PlayMode → 전용 Game View/Console/화면. 이번은 verify-context로 크기만 재검사.
1. 월드 라벨 확인 씬에서 소품3종/정보2종의 재질·한글/숫자·기본/최소크기를 두 화면비·명암 배경으로 한 번에 사람 확인한다.
2. Observer 근접 경계/재진입·표시 요청·소품 표면만 숨기기·카메라 회전/복원·시간 정지 중 동작을 확인한다.
3. 긴 제목/본문 거부 시 승인 내용·크기 유지 및 입력 비차단을 확인한다. 공동/실게임·로컬 Observer/네트워크 배정은 별도다.
과거 승인·완료 단계·검증 설계 원문: [History/004-plan-before-c7-save-compaction.md](History/004-plan-before-c7-save-compaction.md)
