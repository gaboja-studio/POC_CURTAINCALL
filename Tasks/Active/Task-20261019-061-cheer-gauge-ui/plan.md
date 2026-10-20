# Plan
## Context / 현재 승인
- 최신 요청(2026-10-20): 컴파일55개 먼저 최소 수정 후 저장. shop9파일 namespace 격리·원본/meta/settings 불변, check-work/EditMode798/798·월드 라벨 PlayMode18/18 PASS. private9661aa85/병합6b4e2041 main push 완료. 공개 commit/push는 이번에도 실행 전 권한 차단, 월드 라벨 포함 명시 재승인이라는 AI 해석 정정·사용자 확인 대기. C11 신규 구현 제외.
- UI 키트 확보·조합·독립 테스트. Task/폴더 ID는 이력 유지, 수정 구역/권한은 `setup.md` 기준. 기존 공연 씬·보상/네트워크·게임 메뉴 변경 없음.
- C6·C15 공개 b16c328 push/원격 SHA 일치·private73fd48aa origin/main 조상 확인 완료. 최초 C6 포함 저장 해석 철회/권한 차단은 이후 PM 명시 재승인으로 해소된 이력이다.
- PM 직접 재요청 후 C7 공개9216489 commit/push·원격 SHA 일치 확인 완료, private 개별8b49cc29/main38001b8e. 앞선 차단 이력은 보존한다.
- 이어 C9 제작 승인: 제공 자막/화자/방송 명세를 독립 프리팹·확인 씬·표시 API로 조립·검증·기록. C9 추가 저장·삭제·실게임 연결은 제외한다.
- 후속 PM C9 commit/push 요청으로 C9 private/public 선택 저장 승인. 기존 공유 폰트·무관 변경 제외, 삭제/실게임/PR 제외는 유지한다.
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
