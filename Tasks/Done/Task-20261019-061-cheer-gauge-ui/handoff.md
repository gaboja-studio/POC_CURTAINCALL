# Handoff
## 완료 기록 (2026-10-22)
- PM 요청으로 dev 반영 완료 상태를 done으로 마감. #200·#202·#205 및 외줄1일차 가이드 #206(`12267d77`) 병합 완료. 제작 branch tip `b5bee0e7`도 dev에 포함된다.
- 제작 worktree 제거 완료. 로컬 변경은 `D:/github/Worktree-backups/curtaincall-20261022-174457`에 백업·해시 검증 후 보존했다. 메인 dev 기존 폰트 변경은 유지.
- 이번 검사: 구역 SKIPPED(PM), 문서 PASS(verify-fast, WARN18/HARD0), 컴파일 ENV_BLOCKED(연결된 에디터 없음), 테스트 SKIPPED(문서 마감). 아래 과거 PASS와 구분한다.
- 실제 다인·입력·시각·완주/재접속 등 아래 미실시 확인과 기존 실패는 미해결로 남긴다. done은 사람 검증 PASS 의미가 아니다.
## 이전 구현·제출 기록 (당시 상태, 최신 병합 상태는 위 완료 기록 기준)
- 2026-10-21 dev 제출 준비: 사용자 요청으로 origin/dev123cf3e(접속 제한시간/취소 진단) 병합9609b03, 충돌 없음. 병합 후 Scope/Fast/Compile PASS(dirty 씬0, 문서 WARN14/HARD0). 컴파일 직후 Pipeline 일시 단절 후 연결 복구, 신규 TightropeGuideTests3/3 PASS로 기존 미실행 상태 해소. 실제 화면/다인 Play는 미실행·사용자 위임. 별도 에셋/submodule 보존. PR 범위는 외줄1일차 가이드/장치 조작 표시 및 관련 테스트·Task 문서만.
- 2026-10-21 사용자 승인 외줄 가이드: TightropeControlsPanel에서 실제1일차 외줄 공연만 노티 표시. 장애물 안내 종료 후 Q/E+SPACE1회, RelayGate 접근/매달림 최대 설정초, Trapeze 최상단/아래 단원/위·아래 매달림 문구, 일반 수직톱·협동관문 버튼 안내. 기존4초/8m 사용, 알림 덮어쓰기 금지·세대/공연/라운드/로컬별 이력 초기화. CommonInteractionUI는 실제 CanUse/CanGrab/CanAttach로 F 매달리기·F/F홀드 놓기·SPACE 점프/공중F 표시(이후 일차도 유지). 게임 입력/판정·씬·에셋 변경 없음. 컴파일·diff 공백 PASS, 신규 TightropeGuideTests3개 실행은 Pipeline 127.0.0.1:7800 연결 실패로 결과 없음·플레이/다인 사용자 위임. 사용자 저장 요청으로 이번 커밋에 포함. 저장 검사 Scope/Fast/Compile PASS, dirty 씬0, 테스트 반복 생략. 별도 폰트/재질/대기실 에셋/submodule 변경 제외. PR #205 병합 후 후속 변경으로 dev 미제출.
- 2026-10-21 Start 접속 수정(사용자 계획/네트워크 구역 승인): NetworkSessionManager의 LAN/코드 참가를 실제 Client 접속까지3초 제한, 지정 실패 문구와 NGO 종료 완료 후 복구. ServicesSessionConnector는 취소 토큰을 커스텀 INetworkHandler에 연결, 늦은 SDK 시작/새 연결 종료 차단 및 원격 세션 정리 직렬화(HTTP 자체 취소 불가). StartMenu 전체 Selectable 즉시 잠금·기존 상태 복원, 중복 호출/페이지 전환 차단, 성공 시 전환까지 유지. 씬/원본 에셋 변경 없음. b52c92a commit/push 완료, 공연 안내80713ac과 함께 PR #205 제출. 별도 폰트/재질/대기실 에셋/submodule 변경 제외를 사용자 승인받아 원격 HEAD 확인 후 gh로 생성. 저장 검사 Scope/Fast/Compile PASS, 핵심 테스트 결과 재사용. pre-push 훅은 실행 권한 없어 미실행, PR 병합은 하지 않음.
- 공연 상호작용 후속: CommonInteractionUI에 기존 목마 F의 XZ 최근접/최하단·설정 도달거리 판정을 표시용으로 투영(공개 대상선택 API 없어 UI 내 동일 조회, 게임 코드 변경 없음). 앞뒤 중 실제 최근접1명에 목마 타기, 상단 단원은 좌하단 목마 해제/F 길게·홀드 진행 표시, 중간층은 위 단원 선해제 제한 안내, 맨 아래 숨김. HumanCannonUiBinding은 발판 가장자리4m 내 리프트 툴팁 및 CanStart/쉼터 집결/자동출발 초/이동·도착·함정 안내 연결(F 조작 없음). 컴파일·diff 공백 PASS, 신규 자동 테스트·Play·다인/화면 확인 미실행(사용자 위임). 이번 저장 대상에 포함.
- 후속 문구: 공연1~4인 거리에서 '남은' 접두어만 제거(거리 계산 유지). 서비스 방 이용 불가 상호작용 문구를 '서비스를 구매한 고객만 이용 가능합니다'로 교체(입장 판정 유지). 기존 높이 어댑터를 C6에 확장, 매 Show의 고정32 높이 재설정을 보완하고 보조 문구/패널 높이·화면 하단 여백을 조정. 컴파일 PASS, 신규 C6 회귀 포함 테스트 실행은 Unity Pipeline 연결 단절로 결과 없음, Play 미실행. 이번 저장 대상에 포함.
- 최신 후속: NotificationTextLayout 런타임 어댑터가 로드된 배너/토스트에 자동 부착. 각 문구 최대2줄, 제목 확대 시 부제 아래 이동, 패널 상단 고정·아래 확장/짧은 문구 높이 복원. 원본·씬 수정 없음. 컴파일 PASS, 신규 EditMode2/2 PASS(초기2개 실패는 EditMode TMP 투영 초기화 보완 후 통과), Play 미실행. 기존 별도 에셋/submodule 변경 보존, 이번 저장 대상에 포함.
- 2026-10-21 후속 PR: 외줄 추락게이지/기물 슬롯 간격 및 대기실·외줄 안내·공연 사망 배너 Y-260 배치. dev #201(33bfe0e) fast-forward 후 UI 복원, 충돌 없음. 컴파일 PASS, 두 공연 씬 누락스크립트0, 추락 fill 높이15/P1 Y104/외줄 배너 Y-260 확인. Play·자동 테스트 생략(사용자 위임). 안전용 stash 유지.
- 2026-10-21 사용자 후속: Tightrope.unity의 P1 씬 인스턴스만 수정. 추락 트랙 Y-56→13/높이12→23, 빨간 fill Y-60→9/높이4→15(후속 요청으로10에서50% 추가 확대), P1 Y80→104. 원본 프리팹·게임 로직 유지. 에디터 값 확인/씬 저장, Play·컴파일·자동 테스트는 실행하지 않음(사용자 검증 위임).
- 이전 Start·팝업·Canvas는2be6f00 commit/push 완료.
- 로비 정본 HUD/순간이동 수정41bce04 commit/push 완료. 실제UI 누락 점검·대기실 임시UI 정리 완료, 이번 정리 미커밋. 상세 ui-audit.md.
- 게임 시작 순간이동: LobbyRoom이 단순 전환 플래그 종료를 복귀로 판단하던 문제 수정. 실제 공간 복귀에만 재배치.
- 파일·연결·사람 확인: [로비](../../../Docs/Guides/Ui-scene-integration/lobby-ui.md), [Start](../../../Docs/Guides/Ui-scene-integration/start-title.md).
- 기존 폰트2/재질3·ui-workspace.md는 별도 보존. 이번 작업 산출물 아님. 기존 stash 유지.
## Verification
- Start: 최종 컴파일 PASS, StartConnectionTests3/3 PASS(5.96초, 2026-10-21 11:13:23Z XML). 최초3개는 SetUp session null로 실패, EnsureExists/프레임 대기 보완 후 통과. MCP는 Play 전환 중 단절되어 저장 XML로 결과 확인. 없는 LAN 약3초/입력 복구, 실패·성공 잠금/기존 비활성 상태 보존, 이전 요청의 늦은 실패·취소된 Services handler의 시작 차단 검사. 실제 코드/정상 Relay 접속/다인은 미실행, 사용자 위임.
- 이번: 실제10개 씬 누락스크립트0, 컴파일 PASS, 대기실 알림·재등록 중복HUD 회귀1/1 PASS. 전체 실플레이는 미실행.
- 컴파일 PASS, 실제 LAN 로비 PlayMode1/1 PASS(9.49초). 코드 보기/복사·서명·레버 시작·위치 유지 확인.
- 미실행: 원격2~4인, 실제 F/포인터, 다중 화면비, 전체 완주 복귀, Mac/빌드.
- 이전 검증은 각 적용 가이드 유지. 과거 임시 씬 제외, 실제 진행5개 씬만 검증.
- 기존 확장 SpaceLink6개/카메라2개 실패는 이전 기록 유지, 이번 재실행 없음.
## 다음/PM 확인
- 로비 실제 F/눈/복사/ESC·공연 전환 시 HUD 숨김 확인 후 전체 UI 누락 점검.
- 다인 음성/원격 차폐ACK·재접속/지연, 상점 다인·의료 복구·3일밤→4일아침 실제 동선 확인 남음.
- [남은 계획](../../../Docs/Guides/Ui-scene-integration/next-plan.md)과 단계별 가이드의 미실행 항목 유지.
- 후속 hover/외줄 조작 안내 교체 완료. 배치용 컴파일 PASS, 실행 검증은 사용자 요청으로 미실행. Task Integration=dev로 PR 준비, 생성/병합 안 함. 상세 ui-audit.md.

## 최신 사용자 수정: 상황 안내와 C5
- 하단 외줄 안내 삭제 → 상단 상황별 Assets 배너. 공통 메뉴 씬에 Assets C5 슬롯 추가, 실제 대기실·두 공연에 숫자키 선택/선택 UID 동기화/손 표시/선택 기물 내려놓기·선반 연결.
- 기존 소지 한도 유지: 정상2+배낭2,5번째는 잠금. 기물별 미제공 아이콘은delivery, 손3D는 기존 임시 큐브1개.
- Unity 컴파일 PASS, 수정3개 씬 참조/누락스크립트0, diff 공백 PASS. Play·다인·시각 검증 미실행(사용자 위임). ui-audit.md 마지막 절에 확인 항목.
- 이번 변경 미커밋. 기존 폰트/재질/ProjectSettings.asset/ui-workspace.md 변경은 보존했으며 이번 작업 산출물 아님.
- 추가 사용자 수정: 외줄 배너 한글 폰트 교체(문구 누락 문자0), 같은 번호/빈 슬롯으로 집어넣기 및 재선택 꺼내기, 호스트 보관 상태 동기화. 빈손에서 자동 내려놓기/선반 올리기 방지. 실행 검증 미실행.
- 2026-10-21 사용자: 최신 수정 정상 동작 확인, commit/push 및 dev PR 준비 요청. Scope/Fast/Compile PASS(문서 길이 WARN), dirty 씬0, 테스트 반복 생략. PR 초안: Docs/Guides/Ui-scene-integration/dev-pr.md. 저장 스크립트의 add -A 대신 명시 파일 staging으로 별도 변경 보존.

- 2026-10-21 사용자 승인으로 잔여7개 정리. 원본 TTF 참조가 유효한 동적 폰트2개 캐시/재질3개 반올림/runInBackground 테스트 설정은 HEAD 복원. 오래된 미추적 ui-workspace.md 삭제. dev PR 준비 문서 반영, 전체 Play 검증 반복 없음.
