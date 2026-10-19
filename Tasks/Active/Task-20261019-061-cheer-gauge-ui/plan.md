# Plan
## Context
2026-10-19 PM 지시로 이 Task를 UI 리소스 확보·조합·테스트로 확장한다. C1 디자인/기능을 보존하며 제공 버튼 미리보기의 확인·취소·설정·일반 프리팹과 독립 확인 씬을 추가한다. 폴더/Task ID는 이력 보존을 위해 유지한다. 기존 보상/네트워크·게임 메뉴는 변경하지 않는다.
## C8 시스템 배너·오른쪽 토스트 (2026-10-20 PM 승인)
- 후속 저장 승인: PM 재요청으로 private0a705e6c/main1c0f6f0c push 완료, 공개 결과·gitlink·하네스는 이 문서 포함 save-work 실행. 조립 당시 Git 저장 제외 조건을 이번 저장 요청에 한해 대체하며 삭제·실게임 연결은 계속 제외.
- 승인 원문: `C:/Users/imvag/.claude/plans/rustling-foraging-llama.md`. 기존 worktree/Task working 유지, setup 허용 구역만 사용. 삭제·Git 저장·실게임/네트워크/구매 연결·큐/중복 억제/우선순위/사운드/딤 제외.
- 전수58실파일/PNG22 전체 보존(Source/Tools/Docs/Previews/TTF/라이선스 포함). 표시15=신규 배너5+재사용10(PerformanceHud smallplate5/BasicPanels warning1/CommonIcons4). CSV에 크기/SHA/GUID/사용처/미래 후보 기록, 누적96종과 혼동 금지.
- 재사용10 PNG/rect/PPU/border/플랫폼 override/GUID를 읽기 전용 대조하고 불일치 시 생성 전 중단. 신규 배너5 importer만 설정. private Runtime/Builder는 원위치 재사용, NotificationsV1 namespace 격리·입력 주입형 BuildProject로 전역 저장/삭제/Selection·전역 PlayReset 훅 부작용 제거.
- 공개 builder는 Play/컴파일/import/PrefabStage/dirty 씬/기존 출력 충돌을 쓰기 전 차단. additive 임시 조립·소유 출력만 저장, 부분 실패 자동 삭제/재생성 금지. 인스턴스 Awake/OnDisable/ResetForRuntime 초기화, 선행 Show 보호·기존 정본 보존.
- SystemNotifications/SystemBanner/Toast 프리팹3종과 독립 Notifications 씬. Overlay1920×1080/Match0.5/Canvas PPU100/Sprite PPU200, 배너640×104·상단중앙(0,-244), 토스트384×56·우상단(-48,-160). 배너border(48,24,48,24)/smallplate24/icon0, Body0.92/Shadow0.32 한 번만 적용.
- Bold32/Medium18/토스트Medium24·제공 색 유지. glyph/fallback 안전성을 확인한 Static만 재사용, 부족 시 전용 Static Medium/Bold 생성. 공유 Dynamic2개는 렌더링 참조에서도 제외. 실제 sorting/sibling은 HUD 다음·Dialog/Curtain 이전 계약, 기존 HUD는 읽기 전용 인스턴스만 조절·Apply 금지.
- 결합형1개만 활성화, 단독2종 별도 확인. HUD raycast/CanvasGroup 입력 차단 해제, EventSystem/InputSystemUIInputModule1개·Controls만 raycaster. Backstage 문맥 Lobby·입력 제외.
- 제공 Banner.Show/Toast.Show API와 상태·아이콘·None·부제 없음 유지. hold는 샘플 명시, unscaled 배너180/160ms·위8UI/토스트160/140ms·오른쪽16UI·EaseOutCubic, 등장 완료 후 hold. 슬롯 즉시 교체·동문구 재시작·서로 독립, DOTween 추가 없음.
- Controls Show/교체/재시작/Hide/즉시숨김/동시/Reset/timeScale0 합성 확인. 예외·비활성·종료 시 timeScale 복구, 게임 타이머/관리기 없음. 실제 Update/시간 오차·큰delta/짧은hold·중간Hide/Show·초기화/단일구독·Play연속2회 검사.
- 무효hold/state/icon·편집/비활성 Show false/내용 보존, 빈 문구 우선 숨김·공백부제·CR/LF/tab/U+2028/U+2029/rich text/긴문자/… 검사. HasRuntimePresentation과 첫프레임 alpha0 IsVisible, 자연만료/Hide 잔류와 HideImmediate/Reset 초기화 구분. reload 설정 변경/both OFF 지원 주장 금지.
- Mouse/Enter 각1회, 1920×1080/1600×1000 Play·밝고 어두운 배경·상태/단독/동시/긴값/아이콘없음 캡처. 목업·C1/W1/C3 실제 rect 간격, PNG15 crop/알파·Sprite/플랫폼·참조/글리프/Missing Script/전체숨김/raycast/sorting 확인. 정적PNG와 실행 캡처 구분.
- 신규 PlayMode9·기존 Performance/Backstage/C1/Buttons/Panels/Icons 회귀·최종check-work, 종료 Play stopped/씬clean/1920/timeScale 복구. 실패/0건/생략 그대로 기록, Buttons 미결을 PASS로 덮지 않는다. CSV/notifications-verification.md/하네스 사실·index 기록과 사람 외형/공동·실게임 검증은 별개.
- 현재: 프리팹3종/독립 씬·전수58파일·하네스 기록 완료. 컴파일 errors[]·강화PlayMode9/9 두 번·44상태 화면·독립Play2회·기존 UI 개별 회귀 통과. 입력 최초 실패/수집 중단 이력은 notifications-verification.md에 보존, 최종check-work 구역/문서/컴파일·EditMode798/798 PASS. 삭제·Git 저장·실게임 연결 없음.
## 백스테이지·로비 HUD 추가 (2026-10-20 PM 승인)
- 제공 목업의 W1/C4/C5/W6/L1, BackstageHud/LobbyHud/InventorySlotTemplate 컨테이너3종과 독립 합성 확인 씬. 실제 게임·클립보드·자동 타이머·경제·집결 계산 없음.
- private 제공 코드 고유 namespace·입력 주입형 안전 조립 API, 신규14 importer만 설정. 재사용18 정본의 바이트/규격 대조·읽기 전용, 전체 원본/미래 후보 보존.
- Play 초기화·상태/경계·슬롯 증감·로비 가림/복사 단일 콜백·입력/글리프/1920·16:10·기존 UI 회귀·표준 검사·하네스 기록. 삭제·Git 저장 없음.
- 완료: 프리팹3종·확인 씬·원본88실파일/PNG45·신규14/재사용18 전수 기록, 강화PlayMode7/7·입력·1920/16:10·기존 회귀(Buttons 제외)·check-work 통과. 상세와 최초 실패는 [backstage-hud-verification.md](backstage-hud-verification.md), 사람 최종 외형 확인은 대기.
- baseline95항목 중 공유 Dynamic폰트2개 추가 SHA 차이·Buttons 전체2건 실패 미결. 시작부터 dirty, 동적 캐시 원인 미확정. 임의 복구/폰트 재작성 금지·PM 확인 필요. 이전 HUD 공개4a39ef2e73a2acd9b29897de7b4c6f90a836814d/private38442263306d87cfcd761f990bbaed31fe6e9fc2 원격 저장 완료; 현재 Notifications 저장 승인과 구분.
## P1 표시 누락 보정 (2026-10-20 PM 승인)
- 최신 외줄의 안전/주황 경고/위험5구간을 표시. 안전·경고 경계와 합산 위험 비율을 외부 전달받으며 게임 계산은 하지 않는다.
- private Runtime/조립기·편집 샘플, 공개 증분 보정/Play 샘플/테스트, 소유 prefab/씬만 수정. 기존 출력 재생성 없이 자식·식별자·버튼8개 연결 보존.
- 기존 경고 버튼은 경고/위험 순환, 합성 경계±0.3/±0.5. 경계·무효·폭0·위험도·회귀·1920/16:10 검사 후 하네스 갱신. 기존 외줄/Settings 수정·실게임 연결·삭제·Git 저장 없음.
## 공연 HUD 추가 (2026-10-20)
- C2 남은시간160×56/C3 진행408×76/P1 균형544×80을 제공 미리보기대로 조립. C1 별도 정본 인스턴스와 독립 씬에서 동시 확인.
- private 신규20종만 importer 보정, clock/fire/flag는 기존 아이콘 해시 대조 후 정본 읽기 전용. 제공 Runtime/조립기 namespace·실제 경로·안전 진입점만 보정.
- PerformanceHud 전용 prefab/씬/샘플 제어/테스트, 필요할 때만 Static TMP. 평소/경고/겹침/미수신·컨텍스트 검증, 게임 연결·시간 감소/체류 계산·애니메이션 추가 없음.
- 전수 목록·경계값·입력·Overlay1920/16:10·회귀·표준 check-work 및 하네스 기록. 원본 전체 보존, 삭제·Git 저장 제외.
## 아이콘 추가 (2026-10-20)
- 신규22+기존3=25종, 원본64px/표시32px. 전파일 점검 CSV와 미래 사용 후보 기록, Source/Tools/Docs/미리보기/폰트·라이선스 전부 보존.
- 제공 CommonIconBuilder의 경로·고유 namespace·안전 진입점 보정. 기존3종은 해시 대조 후 정본 참조, 신규22종만 importer 설정.
- CommonIcons 직접 참조 catalog·비교 Canvas 프리팹·독립 씬, dark/light/받침75 Image·제공 tint/16·24·32·48px 확인. 기존 버튼은 별도 인스턴스만 사용.
- 원본·알파·여백·시트·플랫폼 설정·전체 참조·입력·실제 Overlay·회귀 검사, Harness 사실/Task 상세 기록. 삭제·commit/push·실게임 연결 없음.
## 패널 추가 (2026-10-20)
- private panels_v1의 조립기·Runtime 재사용, 이번 키트 namespace만 PanelsV1/동봉 버튼 전용으로 격리. 기존 버튼 프리팹·SemiBold는 읽기 전용 참조.
- 신규 패널 PNG7종만 import 설정. 메뉴384×400/확인창512×304·Y=-64, 제공 레이어/문구/160·120ms 연출과 안전한 첫 선택 유지.
- BasicPanels 전용 Canvas 조립 프리팹 하나와 연결된 독립 확인 씬. Medium 폰트는 기존 글리프 충족 시 재사용, 부족 시 전용 생성.
- public wrapper 안전 진입점, 기존 출력/dirty 씬 덮어쓰기 거부. 메뉴·확인창/Cancel/중복 신호·실제 Overlay·화면비·회귀 검사, 제공 아트 및 코드 private 유지.
- 최초 조립에서는 삭제·저장 제외. 이후 2026-10-20 PM 정상 동작 확인·정리/저장 승인으로 검토 PNG4개·중복 README와 meta만 제거, 다른 패널 제작 원본·Tools·SVG·명세·라이선스 보존 및 private/public 저장. 실게임 연결·전역ESC/pause/커서 변경은 계속 제외.
## 공용 버튼 추가
- private CurtainCallButton과 CommonButtonBuilder를 재사용한다. builder의 실제 입력/출력 경로·실행 진입점·연결 저장만 수정하고 제공 코드는 공개 복제하지 않는다.
- CommonButtons 전용 Resources 프리팹4개/SemiBold TMP, Tests 확인 씬 생성. Canvas1920×1080/Match0.5, 192×56, 취소X/설정톱니, 9-slice/PPU200/Body alpha0.92를 준수한다.
- 프리팹 인스턴스를 씬에 가로 배치하고 4상태/Focus/클릭/Submit/비활성 무반응을 확인한다. 전체 키트 삭제는 이번 제작에서 하지 않는다.
- 구역·문서·컴파일 및 실제 Overlay/입력 경로 검증. 합성 입력과 사람 확인을 구분한다. 기존 C1 해시를 보존한다.
## Approach
1. 전용 worktree, 로컬 Task/브랜치에서만 작업. 커밋·push·PR 없음.
2. private PNG 8장 참조, 정확한 레이어/색/크기/상단 중앙 배치, Pretendard TMP.
3. 프로젝트 View와 읽기 전용 Binder. 실제 long 합계와 Cap100/150/200, 1~4인. 사망/이탈 시 명부·색 유지.
4. 순수 표시 데이터 변환과 자동 테스트, 프리팹/전용 테스트씬 생성. 기존 공연씬은 수정하지 않는다.
## Files
- 수정 예정: setup.md 작업 구역의 신규 코드/프리팹/폰트/테스트씬, Task 문서.
- 읽기 전용 참고: private C1 키트, PerformanceRewards.State/Changed, RewardSnapshot.Segments/Rules.
## Risks
- 초기 원본 키트 미추적/다른 PC 재현 제약은 이전 HUD까지 private 원격 저장으로 갱신됨. 새 Notifications는 전체 원본 보존·private 저장 완료, 공개 결과는 이번 save-work 대상이다.
- 시작 doctor FAIL(private C1 미추적) 사전 보고·PM 복사본 import/.meta 승인 이력 유지. 최근 doctor의 보존할 private 변경 FAIL도 보고 완료, 이번 문서 작업에서는 반복 실행하지 않는다.
- Unity CLI beta.6에는 문서의 --until-ready가 없다. 실제 help로 명령 확인.
- 추락 판정은 제공되지 않으므로 점수 감소를 추락으로 간주하지 않는다.
## Auto Verification
- 구역 검사: verify-scope
- 1 문서/규칙: verify-fast
- 2 컴파일: verify-unity -Compile
- 3 테스트: EditMode 변환/예외, PlayMode 표시/바인딩
- 4 실행 로그: 전용 에디터 Game View/Console/스크린샷
## 공동 테스트 항목
1. 1920×1080에서 승인 미리보기와 같은 외형·상단 중앙 배치·합계155 확인.
2. 실제 환호/야유와 100 초과 점수도 중앙 합계/원호 비율이 일치.
3. 사망 시 해당 단원 구간만 제거, 다른 단원색 유지. 가능하면 호스트/참가자 동일 표시 확인.
